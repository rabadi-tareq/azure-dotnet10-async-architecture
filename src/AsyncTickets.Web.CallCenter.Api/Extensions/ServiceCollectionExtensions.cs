// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Microsoft.Identity.Web;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using AsyncTickets.Messaging.ServiceBus;
using AsyncTickets.Models.Services;
using AsyncTickets.Web.Api.Infrastructure;
using AsyncTickets.Web.Api.Services;
using AsyncTickets.Web.Api.Services.MockServices;
using AsyncTickets.Web.Api.Services.Search;
using AsyncTickets.Web.Api.Services.SqlDatabaseConcertRepository;
using AsyncTickets.Web.Api.Services.TicketManagementService;
using AsyncTickets.Web.CallCenter.Api.Services.TicketManagementService;
using AsyncTickets.Web.Models.Services;
using AsyncTickets.Web.Services.Search;
using StackExchange.Redis;

namespace AsyncTickets.Web.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCallCenterApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        var azureCredential = GetAzureCredential(configuration);

        // Adds Microsoft Identity platform support to protect this API.
        services.AddMicrosoftIdentityWebApiAuthentication(configuration, "Api:MicrosoftEntraId");

        services.AddControllers();

        services.AddAzureAppConfiguration();

        // Enable feature management for easily enabling or disabling
        // optional features like rendering tickets out-of-process.
        services.AddFeatureManagement();

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        if (configuration["App:ApplicationInsights:ConnectionString"] is string appInsightsConnectionString)
        {
            services.AddCallCenterApiOpenTelemetry(appInsightsConnectionString);
        }

        services.AddAzureServiceBusMessageBus("App:ServiceBus", azureCredential);

        services.AddAzureSearchService(configuration);
        services.AddConcertContextServices(configuration);
        services.AddDistributedSession(configuration, azureCredential);
        services.AddScoped<IPaymentGatewayService, MockPaymentGatewayService>();
        services.AddTicketManagementService(configuration);
        services.AddTicketImageService(configuration, azureCredential);

        // The ApplicationInitializer is resolved at startup with all its dependencies and will ensure
        // they are all properly initialized upon construction.
        services.AddScoped<ApplicationInitializer, ApplicationInitializer>();

        services.AddHealthChecks();

        return services;
    }

    private static void AddTicketManagementService(this IServiceCollection services, IConfiguration configuration)
    {
        var sqlDatabaseConnectionString = configuration["App:SqlDatabase:ConnectionString"];
        if (string.IsNullOrWhiteSpace(sqlDatabaseConnectionString))
        {
            services.AddScoped<ITicketManagementService, MockTicketManagementService>();
            services.AddScoped<ITicketRenderingService, MockTicketRenderingService>();
        }
        else
        {
            services.AddScoped<ITicketManagementService, TicketManagementService>();

            // Reading a feature flag is an asynchronous operation, so it's not possible
            // to register an ITicketRenderingService provider method directly. Instead,
            // use a factory pattern to retrieve the service asynchronously.
            services.AddScoped<ITicketRenderingServiceFactory, FeatureDependentTicketRenderingServiceFactory>();
            services.AddScoped<LocalTicketRenderingService>();
            services.AddScoped<DistributedTicketRenderingService>();
            services.AddHostedService<TicketRenderCompleteMessageHandler>();
        }
    }

    private static void AddAzureSearchService(this IServiceCollection services, IConfiguration configuration)
    {
        var azureSearchServiceName = configuration["App:AzureSearch:ServiceName"];
        var sqlDatabaseConnectionString = configuration["App:SqlDatabase:ConnectionString"];
        if (string.IsNullOrWhiteSpace(azureSearchServiceName) && string.IsNullOrWhiteSpace(sqlDatabaseConnectionString))
        {
            // Add a dummy concert search service in case the Azure Search service isn't provisioned and configured yet.
            services.AddScoped<IConcertSearchService, MockConcertSearchService>();
        }
        else if (string.IsNullOrWhiteSpace(azureSearchServiceName))
        {
            services.AddScoped<IConcertSearchService, SqlDatabaseConcertSearchService>();
        }
        else
        {
            services.AddScoped<IConcertSearchService>(x => new AzureSearchConcertSearchService(azureSearchServiceName, sqlDatabaseConnectionString));
        }
    }

    private static void AddConcertContextServices(this IServiceCollection services, IConfiguration configuration)
    {
        var sqlDatabaseConnectionString = configuration["App:SqlDatabase:ConnectionString"];

        if (string.IsNullOrWhiteSpace(sqlDatabaseConnectionString))
        {
            services.AddScoped<IConcertRepository, MockConcertRepository>();
        }
        else
        {
            // Add a concert repository based on Azure SQL Database.
            services.AddDbContextPool<ConcertDataContext>(options => options.UseSqlServer(sqlDatabaseConnectionString,
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(3),
                        errorNumbersToAdd: null);
                }));
            services.AddScoped<IConcertRepository, SqlDatabaseConcertRepository>();
        }
    }

    private static void AddDistributedSession(this IServiceCollection services, IConfiguration configuration, TokenCredential token)
    {
        var redisCacheConnectionString = configuration["App:RedisCache:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisCacheConnectionString))
        {
            // If we have a connection string to Redis, use that as the distributed cache.
            // If not, fall back to an in-memory cache.
            services.AddStackExchangeRedisCache(options =>
            {
                var configurationOptions = ConfigurationOptions.Parse(redisCacheConnectionString);

                configurationOptions.ConfigureForAzureWithTokenCredentialAsync(token).GetAwaiter().GetResult();

                options.ConfigurationOptions = configurationOptions;
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }
    }

    private static void AddTicketImageService(this IServiceCollection services, IConfiguration configuration, TokenCredential token)
    {
        // It is best practice to create Azure SDK clients once and reuse them.
        // https://learn.microsoft.com/azure/storage/blobs/storage-blob-client-management#manage-client-objects
        // https://devblogs.microsoft.com/azure-sdk/lifetime-management-and-thread-safety-guarantees-of-azure-sdk-net-clients/
        var storageAccountUri = configuration["App:StorageAccount:Uri"]
            ?? throw new InvalidOperationException("Required configuration missing. Could not find App:StorageAccount:Uri setting.");

        services.AddSingleton<ITicketImageService, TicketImageService>();
        services.AddSingleton(_ => new BlobServiceClient(new Uri(storageAccountUri), token));
    }

    private static void AddCallCenterApiOpenTelemetry(this IServiceCollection services, string appInsightsConnectionString)
    {
        services.AddOpenTelemetry()
            .UseAzureMonitor(o => o.ConnectionString = appInsightsConnectionString)
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation()
                       .AddRedisInstrumentation()
                       .AddSqlClientInstrumentation()
                       .AddSource("Azure.*");
            });
    }

    private static TokenCredential GetAzureCredential(IConfiguration configuration) =>
        configuration["App:AzureCredentialType"] switch
        {
            "AzureCLI" => new AzureCliCredential(),
            "Environment" => new EnvironmentCredential(),
            "ManagedIdentity" => configuration["AZURE_CLIENT_ID"] is { Length: > 0 } clientId
                ? new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId))
                : new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),
            "VisualStudio" => new VisualStudioCredential(),
            "VisualStudioCode" => new VisualStudioCodeCredential(),
            _ => new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = configuration["AZURE_CLIENT_ID"] }),
        };
}
