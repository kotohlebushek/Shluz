namespace Shluz.Models;

public sealed class GraphQLRequest
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public string QueryText { get; set; } = string.Empty;
    public string? OperationName { get; set; }
    public Dictionary<string, object?> Variables { get; set; } = new();
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
}

public sealed class ParsedGraphQLDocument
{
    public string OperationType { get; set; } = "query";
    public string? OperationName { get; set; }
    public int MaxDepth { get; set; }
    public List<FieldSelection> Fields { get; set; } = [];
}

public sealed class FieldSelection
{
    public string FieldName { get; set; } = string.Empty;
    public string ParentType { get; set; } = "Query";
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, object?> Arguments { get; set; } = new();
    public List<FieldSelection> Children { get; set; } = [];
    public FieldSelection? Parent { get; set; }
    public FieldServiceMapping? Mapping { get; set; }
}

public sealed class FieldServiceMapping
{
    public string GraphQLPath { get; set; } = string.Empty;
    public string? RequiredArgument { get; set; }
    public bool IsParallelAllowed { get; set; } = true;
    public ServiceDescriptor Service { get; set; } = new();
    public GrpcMethodDescriptor Method { get; set; } = new();
}

public sealed class ServiceDescriptor
{
    public string ServiceName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Protocol { get; set; } = "gRPC";
    public int TimeoutMs { get; set; } = 3000;
    public bool IsAvailable { get; set; } = true;
    public List<GrpcMethodDescriptor> Methods { get; set; } = [];
}

public sealed class GrpcMethodDescriptor
{
    public string MethodName { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public string ResponseType { get; set; } = string.Empty;
    public bool SupportsStreaming { get; set; }
}

public sealed class GrpcCall
{
    public Guid CallId { get; init; } = Guid.NewGuid();
    public object? RequestPayload { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = "Pending";
    public GrpcMethodDescriptor Method { get; set; } = new();
    public ServiceDescriptor TargetService { get; set; } = new();
    public GrpcResponse? Response { get; set; }
    public string GraphQLPath { get; set; } = string.Empty;
}

public sealed class GrpcResponse
{
    public object? Data { get; set; }
    public string Status { get; set; } = "Success";
    public int DurationMs { get; set; }
    public GrpcCall? Call { get; set; }
    public GatewayError? Error { get; set; }
}

public sealed class ExecutionPlan
{
    public Guid PlanId { get; init; } = Guid.NewGuid();
    public string Status { get; set; } = "Created";
    public List<ExecutionStep> Steps { get; set; } = [];
}

public sealed class ExecutionStep
{
    public Guid StepId { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool CanRunInParallel { get; set; }
    public string Status { get; set; } = "Pending";
    public GrpcMethodDescriptor Method { get; set; } = new();
    public ServiceDescriptor Service { get; set; } = new();
    public FieldSelection Field { get; set; } = new();
    public List<Guid> Dependencies { get; set; } = [];
}

public sealed class GraphQLResponse
{
    public Guid ResponseId { get; init; } = Guid.NewGuid();
    public object? Data { get; set; }
    public List<GatewayError> Errors { get; set; } = [];
    public Dictionary<string, object?> Extensions { get; set; } = new();
}

public sealed class GatewayError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public sealed class GatewayConfiguration
{
    public int MaxQueryDepth { get; set; } = 8;
    public int MaxQueryCost { get; set; } = 100;
    public int DefaultTimeoutMs { get; set; } = 3000;
    public List<ServiceDescriptor> Services { get; set; } = [];
    public List<FieldServiceMapping> Mappings { get; set; } = [];
}
