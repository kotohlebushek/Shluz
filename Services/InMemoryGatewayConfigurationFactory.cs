using Shluz.Models;
using GatewayServiceDescriptor = Shluz.Models.ServiceDescriptor;

namespace Shluz.Services;

public static class InMemoryGatewayConfigurationFactory
{
    public static GatewayConfiguration CreateDefault()
    {
        var userService = new GatewayServiceDescriptor { ServiceName = "UserService", Address = "https://localhost:7101", TimeoutMs = 2500 };
        var orderService = new GatewayServiceDescriptor { ServiceName = "OrderService", Address = "https://localhost:7102", TimeoutMs = 3000 };
        var getUser = new GrpcMethodDescriptor { MethodName = "GetUser", RequestType = "GetUserRequest", ResponseType = "UserReply" };
        var getOrders = new GrpcMethodDescriptor { MethodName = "GetOrdersByUser", RequestType = "OrdersByUserRequest", ResponseType = "OrdersReply" };
        userService.Methods.Add(getUser);
        orderService.Methods.Add(getOrders);

        return new GatewayConfiguration
        {
            MaxQueryDepth = 6,
            MaxQueryCost = 80,
            DefaultTimeoutMs = 3000,
            Services = [userService, orderService],
            Mappings =
            [
                new FieldServiceMapping { GraphQLPath = "user", RequiredArgument = "id", IsParallelAllowed = false, Service = userService, Method = getUser },
                new FieldServiceMapping { GraphQLPath = "user.orders", RequiredArgument = "userId", IsParallelAllowed = true, Service = orderService, Method = getOrders }
            ]
        };
    }
}
