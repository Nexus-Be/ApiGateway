namespace Nexus.Gateway.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShoppingCart.Contracts.V1;

public static class GrpcClientsExtension
{
    public static IServiceCollection AddGatewayGrpcClients(this IServiceCollection services)
    {
        // Core User/Auth Service Client
        services.AddNamedGrpcClient<UsersApi.UsersApiClient>("users");
        services.AddNamedGrpcClient<CatalogApi.CatalogApiClient>("catalog");

        // NOTE (Team): Khi phát triển các Microservices tiếp theo (Catalog, Commerce, etc.),
        // bổ sung contract tương ứng vào shop.proto và đăng ký AddNamedGrpcClient tại đây.

        return services;
    }

    private static void AddNamedGrpcClient<TClient>(this IServiceCollection services, string serviceName) where TClient : class
    {
        services.AddGrpcClient<TClient>((sp, o) =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<GatewayOptions>>().CurrentValue;
            var svc = opts.GetService(serviceName);
            if (svc != null && !string.IsNullOrEmpty(svc.Address))
            {
                o.Address = new Uri(svc.Address);
            }
        });
    }
}
