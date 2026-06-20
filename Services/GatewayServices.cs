using Microsoft.Extensions.Caching.Memory;
using Shluz.Models;
using Shluz.Repositories;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Shluz.Services;

public sealed class GraphQLParser
{
    private static readonly Regex TokenRegex = new(@"[A-Za-z_][A-Za-z0-9_]*|[{}():,]", RegexOptions.Compiled);

    public ParsedGraphQLDocument Parse(GraphQLRequest request)
    {
        var tokens = TokenRegex.Matches(request.QueryText).Select(match => match.Value).ToArray();
        var document = new ParsedGraphQLDocument { OperationName = request.OperationName };
        var index = 0;
        if (tokens.Length > 0 && new[] { "query", "mutation", "subscription" }.Contains(tokens[0]))
        {
            document.OperationType = tokens[index++];
            if (index < tokens.Length && tokens[index] != "{") document.OperationName ??= tokens[index++];
        }
        while (index < tokens.Length && tokens[index] != "{") index++;
        if (index >= tokens.Length) throw new InvalidOperationException("GraphQL-запрос не содержит блока выборки.");
        index++;
        document.Fields = ParseFields(tokens, ref index, "Query", null, 1);
        document.MaxDepth = CalculateDepth(document.Fields, 1);
        return document;
    }

    private static List<FieldSelection> ParseFields(string[] tokens, ref int index, string parentType, FieldSelection? parent, int depth)
    {
        var fields = new List<FieldSelection>();
        while (index < tokens.Length && tokens[index] != "}")
        {
            var name = tokens[index++];
            if (name is "," or ":" or "(" or ")") continue;
            var field = new FieldSelection { FieldName = name, Parent = parent, ParentType = parentType, Path = parent is null ? name : $"{parent.Path}.{name}" };
            if (index < tokens.Length && tokens[index] == "(") ReadArguments(tokens, ref index, field);
            if (index < tokens.Length && tokens[index] == "{")
            {
                index++;
                field.Children = ParseFields(tokens, ref index, name, field, depth + 1);
                if (index < tokens.Length && tokens[index] == "}") index++;
            }
            fields.Add(field);
        }
        return fields;
    }

    private static void ReadArguments(string[] tokens, ref int index, FieldSelection field)
    {
        index++;
        while (index < tokens.Length && tokens[index] != ")")
        {
            var key = tokens[index++];
            if (index < tokens.Length && tokens[index] == ":") index++;
            if (index < tokens.Length) field.Arguments[key] = tokens[index++];
            if (index < tokens.Length && tokens[index] == ",") index++;
        }
        if (index < tokens.Length && tokens[index] == ")") index++;
    }

    private static int CalculateDepth(IEnumerable<FieldSelection> fields, int level) => !fields.Any() ? level : fields.Max(field => Math.Max(level, CalculateDepth(field.Children, level + 1)));
}

public sealed class ComplexityAnalyzer(GatewayConfiguration configuration)
{
    public int CalculateCost(ParsedGraphQLDocument document) => Count(document.Fields) + document.MaxDepth * 2;
    public bool IsAllowed(ParsedGraphQLDocument document) => document.MaxDepth <= configuration.MaxQueryDepth && CalculateCost(document) <= configuration.MaxQueryCost;
    private static int Count(IEnumerable<FieldSelection> fields) => fields.Sum(field => 1 + Count(field.Children));
}

public sealed class MappingRegistry(GatewayConfiguration configuration)
{
    public IReadOnlyList<FieldServiceMapping> Mappings => configuration.Mappings;
    public FieldServiceMapping? FindMapping(FieldSelection field) => configuration.Mappings.FirstOrDefault(m => string.Equals(m.GraphQLPath, field.Path, StringComparison.OrdinalIgnoreCase));
    public List<FieldServiceMapping> FindMappings(ParsedGraphQLDocument document) => Flatten(document.Fields).Select(FindMapping).OfType<FieldServiceMapping>().DistinctBy(m => m.GraphQLPath).ToList();
    public static IEnumerable<FieldSelection> Flatten(IEnumerable<FieldSelection> fields) => fields.SelectMany(field => new[] { field }.Concat(Flatten(field.Children)));
}

public sealed class ExecutionPlanner(MappingRegistry registry)
{
    public ExecutionPlan BuildPlan(ParsedGraphQLDocument document)
    {
        var plan = new ExecutionPlan();
        var order = 1;
        foreach (var field in MappingRegistry.Flatten(document.Fields))
        {
            var mapping = registry.FindMapping(field);
            if (mapping is null) continue;
            field.Mapping = mapping;
            plan.Steps.Add(new ExecutionStep { Name = $"{mapping.Service.ServiceName}.{mapping.Method.MethodName}", Order = order++, CanRunInParallel = mapping.IsParallelAllowed, Method = mapping.Method, Service = mapping.Service, Field = field });
        }
        return plan;
    }
}

public sealed class GrpcClientInvoker(IMemoryCache cache)
{
    public async Task<GrpcResponse> InvokeAsync(GrpcCall call, CancellationToken cancellationToken)
    {
        var key = $"{call.TargetService.ServiceName}:{call.Method.MethodName}:{call.GraphQLPath}:{System.Text.Json.JsonSerializer.Serialize(call.RequestPayload)}";
        if (cache.TryGetValue(key, out object? cached)) return new GrpcResponse { Call = call, Data = cached, Status = "Cached", DurationMs = 0 };
        var sw = Stopwatch.StartNew();
        await Task.Delay(Random.Shared.Next(50, 180), cancellationToken);
        var data = new Dictionary<string, object?> { [call.GraphQLPath] = $"Ответ {call.Method.MethodName} от {call.TargetService.ServiceName}", ["payload"] = call.RequestPayload };
        cache.Set(key, data, TimeSpan.FromSeconds(30));
        return new GrpcResponse { Call = call, Data = data, Status = "Success", DurationMs = (int)sw.ElapsedMilliseconds };
    }
}

public sealed class ExecutionEngine(GrpcClientInvoker invoker)
{
    public async Task<List<GrpcResponse>> ExecuteAsync(ExecutionPlan plan, CancellationToken cancellationToken)
    {
        plan.Status = "Running";
        var responses = new List<GrpcResponse>();
        var orderedSteps = plan.Steps.OrderBy(step => step.Order).ToArray();
        for (var index = 0; index < orderedSteps.Length;)
        {
            var batch = orderedSteps[index].CanRunInParallel
                ? orderedSteps.Skip(index).TakeWhile(step => step.CanRunInParallel).ToArray()
                : [orderedSteps[index]];
            responses.AddRange(await Task.WhenAll(batch.Select(step => ExecuteStepAsync(step, cancellationToken))));
            index += batch.Length;
        }
        plan.Status = "Completed";
        return responses;
    }

    private async Task<GrpcResponse> ExecuteStepAsync(ExecutionStep step, CancellationToken cancellationToken)
    {
        step.Status = "Running";
        var call = new GrpcCall { StartedAt = DateTime.UtcNow, Status = "Running", Method = step.Method, TargetService = step.Service, GraphQLPath = step.Field.Path, RequestPayload = step.Field.Arguments };
        var response = await invoker.InvokeAsync(call, cancellationToken);
        call.FinishedAt = DateTime.UtcNow; call.Status = response.Status; call.Response = response; step.Status = response.Status;
        return response;
    }
}

public sealed class ResponseAggregator
{
    public GraphQLResponse Aggregate(IEnumerable<GrpcResponse> responses, Guid requestId, long elapsedMs) => new()
    {
        Data = responses.Where(r => r.Error is null).Select(r => r.Data).ToArray(),
        Errors = responses.Select(r => r.Error).OfType<GatewayError>().ToList(),
        Extensions = new Dictionary<string, object?> { ["requestId"] = requestId, ["elapsedMs"] = elapsedMs, ["generatedAt"] = DateTime.UtcNow }
    };
}

public sealed class GatewayOrchestrator(GraphQLParser parser, ComplexityAnalyzer analyzer, ExecutionPlanner planner, ExecutionEngine engine, ResponseAggregator aggregator, IRepository<GraphQLRequest, Guid> requests, IRepository<ExecutionPlan, Guid> plans, IRepository<GraphQLResponse, Guid> responses)
{
    public async Task<GraphQLResponse> HandleAsync(GraphQLRequest request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await requests.AddAsync(request, cancellationToken);
        var document = parser.Parse(request);
        if (!analyzer.IsAllowed(document)) return new GraphQLResponse { Errors = [new GatewayError { Code = "QUERY_TOO_COMPLEX", Message = "Запрос превышает ограничения сложности.", Source = nameof(ComplexityAnalyzer), Path = document.OperationName ?? document.OperationType }] };
        var plan = planner.BuildPlan(document);
        await plans.AddAsync(plan, cancellationToken);
        var grpcResponses = await engine.ExecuteAsync(plan, cancellationToken);
        var response = aggregator.Aggregate(grpcResponses, request.RequestId, sw.ElapsedMilliseconds);
        await responses.AddAsync(response, cancellationToken);
        return response;
    }
}
