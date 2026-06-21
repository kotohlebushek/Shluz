using HotChocolate;
using Shluz.Models;

namespace Shluz.Services;

/// <summary>
/// Hot Chocolate GraphQL facade that keeps the demo gateway orchestration in the request path.
/// </summary>
public sealed class HotChocolateGatewayQuery
{
    public async Task<HotChocolateUser?> GetUserAsync(string id, [Service] GatewayOrchestrator orchestrator, CancellationToken cancellationToken)
    {
        var escapedId = id.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        var request = new GraphQLRequest
        {
            QueryText = $$"""
                query GetUser {
                  user(id: "{{escapedId}}") {
                    id
                    name
                    email
                    orders {
                      id
                      total
                      status
                    }
                  }
                }
                """,
            OperationName = "GetUser"
        };

        var response = await orchestrator.HandleAsync(request, cancellationToken);
        if (response.Errors.Count > 0)
        {
            throw new GraphQLException(response.Errors.Select(error => ErrorBuilder.New()
                .SetMessage(error.Message)
                .SetCode(error.Code)
                .SetExtension("source", error.Source)
                .SetPath(global::HotChocolate.Path.Parse(error.Path))
                .Build()));
        }

        return new HotChocolateUser(
            id,
            $"Пользователь {id}",
            $"user-{id}@example.test",
            [
                new HotChocolateOrder($"order-{id}-1", 1250.50m, "PAID"),
                new HotChocolateOrder($"order-{id}-2", 460.00m, "PROCESSING")
            ]);
    }
}

public sealed record HotChocolateUser(string Id, string Name, string Email, IReadOnlyList<HotChocolateOrder> Orders);

public sealed record HotChocolateOrder(string Id, decimal Total, string Status);
