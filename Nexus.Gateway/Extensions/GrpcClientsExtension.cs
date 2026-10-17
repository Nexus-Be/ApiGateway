namespace Nexus.Gateway.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShoppingCart.Contracts.V1;

public static class GrpcClientsExtension
{
    public static IServiceCollection AddGatewayGrpcClients(this IServiceCollection services)
    {
        services.AddNamedGrpcClient<CatalogApi.CatalogApiClient>("catalog");
        services.AddNamedGrpcClient<CommerceApi.CommerceApiClient>("commerce");
        services.AddNamedGrpcClient<UsersApi.UsersApiClient>("users");
        services.AddNamedGrpcClient<LogApi.LogApiClient>("log");
        services.AddNamedGrpcClient<AuctionApi.AuctionApiClient>("auction");
        services.AddNamedGrpcClient<FulfillmentApi.FulfillmentApiClient>("fulfillment");
        services.AddNamedGrpcClient<NotificationApi.NotificationApiClient>("notification");
        services.AddNamedGrpcClient<WalletApi.WalletApiClient>("wallet");

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
