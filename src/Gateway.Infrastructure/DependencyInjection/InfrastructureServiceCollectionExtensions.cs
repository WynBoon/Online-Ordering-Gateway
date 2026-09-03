using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using Gateway.Application.Ports;
using Gateway.Application.Repositories;
using Gateway.Infrastructure.Messaging;
using Gateway.Infrastructure.Persistence;
using Gateway.Infrastructure.Repositories;
using Gateway.Infrastructure.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<GatewayDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Gateway")));

        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<OutboxDispatcher>();

        var keyVaultUri = configuration["KeyVault:Uri"];
        if (!string.IsNullOrEmpty(keyVaultUri))
        {
            services.AddSingleton(new SecretClient(new Uri(keyVaultUri), new DefaultAzureCredential()));
            services.AddScoped<ISecretResolver, KeyVaultSecretResolver>();
        }
        else
        {
            // Local dev: resolve local:// refs from configuration / user secrets.
            services.AddSingleton<ISecretResolver, ConfigurationSecretResolver>();
        }

        var serviceBusConnection = configuration.GetConnectionString("ServiceBus");
        var serviceBusFullyQualifiedNamespace = configuration["ServiceBus:FullyQualifiedNamespace"];
        if (!string.IsNullOrEmpty(serviceBusFullyQualifiedNamespace))
        {
            // Managed identity in Azure — preferred over a connection string (ARCHITECTURE.md §13).
            services.AddSingleton(new ServiceBusClient(serviceBusFullyQualifiedNamespace, new DefaultAzureCredential()));
        }
        else if (!string.IsNullOrEmpty(serviceBusConnection))
        {
            // Local dev fallback only.
            services.AddSingleton(new ServiceBusClient(serviceBusConnection));
        }

        return services;
    }
}
