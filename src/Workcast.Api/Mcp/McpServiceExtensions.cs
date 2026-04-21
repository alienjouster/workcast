using ModelContextProtocol.AspNetCore;

namespace Workcast.Api.Mcp;

public static class McpServiceExtensions
{
    public static IServiceCollection AddWorkcastMcp(this IServiceCollection services)
    {
        services.AddMcpServer()
            .WithHttpTransport()
            .WithTools<WorkcastMcpTools>();
        return services;
    }
}
