using Shluz.Hubs;
using Shluz.Models;
using Shluz.Repositories;
using Shluz.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(InMemoryGatewayConfigurationFactory.CreateDefault());
builder.Services.AddSingleton<IRepository<GraphQLRequest, Guid>, InMemoryRepository<GraphQLRequest, Guid>>();
builder.Services.AddSingleton<IRepository<ExecutionPlan, Guid>, InMemoryRepository<ExecutionPlan, Guid>>();
builder.Services.AddSingleton<IRepository<GraphQLResponse, Guid>, InMemoryRepository<GraphQLResponse, Guid>>();
builder.Services.AddSingleton<MappingRegistry>();
builder.Services.AddSingleton<GraphQLParser>();
builder.Services.AddSingleton<ComplexityAnalyzer>();
builder.Services.AddSingleton<ExecutionPlanner>();
builder.Services.AddSingleton<GrpcClientInvoker>();
builder.Services.AddSingleton<ExecutionEngine>();
builder.Services.AddSingleton<ResponseAggregator>();
builder.Services.AddSingleton<GatewayOrchestrator>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<GatewayEventsHub>("/gateway-events");

app.MapGet("/api/configuration", (GatewayConfiguration configuration) => Results.Ok(configuration));
app.MapGet("/api/mappings", (MappingRegistry registry) => Results.Ok(registry.Mappings));
app.MapGet("/api/requests", (IRepository<GraphQLRequest, Guid> repository) => Results.Ok(repository.All));
app.MapPost("/api/graphql", async (GraphQLRequestDto dto, GatewayOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    var request = new GraphQLRequest
    {
        QueryText = dto.Query,
        OperationName = dto.OperationName,
        Variables = dto.Variables ?? new Dictionary<string, object?>()
    };

    var response = await orchestrator.HandleAsync(request, cancellationToken);
    return Results.Ok(response);
});

app.Run();

public sealed record GraphQLRequestDto(string Query, string? OperationName, Dictionary<string, object?>? Variables);
