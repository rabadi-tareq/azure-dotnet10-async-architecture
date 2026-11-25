// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using Azure.Core;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders.Distributed;
using Microsoft.Identity.Web.UI;
using Microsoft.Net.Http.Headers;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Polly;
using AsyncTickets.Models.ConcertContext;
using AsyncTickets.Models.Services;
using AsyncTickets.Web.CallCenter.Infrastructure;
using AsyncTickets.Web.CallCenter.Services;
using AsyncTickets.Web.CallCenter.Services.ApiConcertService;
using AsyncTickets.Web.CallCenter.Services.AsyncTicketsApiServices;
using AsyncTickets.Web.CallCenter.Services.MockServices;
using StackExchange.Redis;

namespace AsyncTickets.Web.Extensions;

public static class ServiceCollectionExtensions
{
    private const string UserAgent = "AsyncTickets.Web.CallCenter";

    public static IServiceCollection AddCallCenterWebServices(this IServiceCollection services, IConfiguration configuration, TokenCredential token)
    {
        services.AddHttpContextAccessor();
        services.Configure<AsyncTicketsApiOptions>(configuration.GetSection("App:AsyncTicketsApi"));
        services.AddMicrosoftEntraIdServices(configuration);
        services.AddControllersWithViews();

        if (configuration["App:ApplicationInsights:ConnectionString"] is string appInsightsConnectionString)
        {
            // Normally, AppInsights services wouldn't need to also be registered alongside OpenTelemetry services.
            // But this application uses custom AppInsights events which are currently not possible to send with the
            // Azure Monitor OpenTelemetry distro.
            services.AddApplicationInsightsTelemetry(o => o.ConnectionString = appInsightsConnectionString);
            services.AddCallCenterWebOpenTelemetry(appInsightsConnectionString);
        }

        var baseUri = configuration["App:AsyncTicketsApi:BaseUri"];
        services.AddApiService<IConcertContextService, AsyncTicketsApiConcertService, MockConcertContextService>(baseUri, "application/json", "AsyncTickets.Web");
        services.AddApiService<IConcertSearchService, AsyncTicketsApiConcertSearchService, MockConcertSearchService>(baseUri, "application/json", UserAgent);
        services.AddApiService<ITicketPurchaseService, AsyncTicketsApiTicketPurchaseService, MockTicketPurchaseService>(baseUri, "application/json", UserAgent);
        services.AddApiService<ITicketImageService, AsyncTicketsApiTicketImageService, MockTicketImageService>(baseUri, "application/octet-stream", UserAgent);

        services.AddAzureCacheForRedis(configuration, token);
        services.AddHealthChecks();

        // Add support for session state.
        // NOTE: If there is a distributed cache service (e.g. Redis) then this will be used to store session data.
        services.AddSession();

        return services;
    }

    private static void AddAzureCacheForRedis(this IServiceCollection services, IConfiguration configuration, TokenCredential token)
    {
        var redisCacheConnectionString = configuration["App:RedisCache:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(redisCacheConnectionString))
        {
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

    private static void AddApiService<TService, TApiImplementation, TMockImplementation>(
        this IServiceCollection services, string? baseUri, string acceptHeader, string userAgent)
        where TService : class
        where TApiImplementation : class, TService
        where TMockImplementation : class, TService
    {
        if (string.IsNullOrWhiteSpace(baseUri))
        {
            services.AddScoped<TService, TMockImplementation>();
            return;
        }

        services.AddHttpClient<TService, TApiImplementation>(httpClient =>
        {
            httpClient.BaseAddress = new Uri(baseUri);
            httpClient.DefaultRequestHeaders.Add(HeaderNames.Accept, acceptHeader);
            httpClient.DefaultRequestHeaders.Add(HeaderNames.UserAgent, userAgent);
        })
        .AddResilienceHandler("api-resilience", builder =>
        {
            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });

            builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = args => ValueTask.FromResult(
                    HttpClientResiliencePredicates.IsTransient(args.Outcome)
                    || args.Outcome.Result?.StatusCode == HttpStatusCode.NotFound)
            });
        });
    }

    private static void AddMicrosoftEntraIdServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRazorPages().AddMicrosoftIdentityUI();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Roles.Administrator, authBuilder =>
            {
                authBuilder.RequireRole(Roles.Administrator);
            });
        });

        var builder = services.AddMicrosoftIdentityWebAppAuthentication(configuration, "MicrosoftEntraId")
            .EnableTokenAcquisitionToCallDownstreamApi([])
            .AddDownstreamWebApi("asynctickets-api", configuration.GetSection("GraphBeta"));

        // when using Microsoft.Identity.Web to retrieve an access token on behalf of the authenticated user
        // you should use a shared session state provider.
        // https://learn.microsoft.com/en-us/azure/active-directory-b2c/configure-authentication-sample-web-app-with-api?tabs=visual-studio#token-cache-for-a-web-app
        if (string.IsNullOrEmpty(configuration["App:RedisCache:ConnectionString"]))
        {
            builder.AddInMemoryTokenCaches();
        }
        else
        {
            builder.AddDistributedTokenCaches();
            services.Configure<MsalDistributedTokenCacheAdapterOptions>(options =>
            {
                options.DisableL1Cache = true;
            });
        }

        services.Configure<OpenIdConnectOptions>(configuration.GetSection("MicrosoftEntraId"));
        if (!Debugger.IsAttached)
        {
            // this sample uses AFD for the URL registered with Microsoft Entra ID to make it easier to get started
            // but we recommend host name preservation for production scenarios
            // https://learn.microsoft.com/en-us/azure/architecture/best-practices/host-name-preservation
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                // not needed when using host name preservation
                options.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
            });

            services.Configure<MicrosoftIdentityOptions>(options =>
            {
                var frontDoorHostname = configuration["App:FrontDoorHostname"];
                var callbackPath = configuration["MicrosoftEntraId:CallbackPath"];

                options.Events.OnTokenValidated += async ctx =>
                {
                    await CreateOrUpdateUserInformation(ctx);
                };
                options.Events.OnRedirectToIdentityProvider += ctx =>
                {
                    // not needed when using host name preservation
                    ctx.ProtocolMessage.RedirectUri = $"https://{frontDoorHostname}{callbackPath}";
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToIdentityProviderForSignOut += ctx =>
                {
                    // not needed when using host name preservation
                    ctx.ProtocolMessage.PostLogoutRedirectUri = $"https://{frontDoorHostname}";
                    return Task.CompletedTask;
                };
            });
        }
    }

    private static void AddCallCenterWebOpenTelemetry(this IServiceCollection services, string appInsightsConnectionString)
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
                       .AddRedisInstrumentation();
            });
    }

    private static async Task CreateOrUpdateUserInformation(Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext ctx)
    {
        try
        {
            if (ctx.Principal?.Identity is not null)
            {
                // The user has signed in, ensure the information in the database is up-to-date.
                var user = new User
                {
                    Id = ctx.Principal.GetUniqueId(),
                    DisplayName = ctx.Principal.Identity.Name ?? "New User"
                };

                var concertService = ctx.HttpContext.RequestServices.GetRequiredService<IConcertContextService>();
                await concertService.CreateOrUpdateUserAsync(user);
            }
        }
        catch (Exception ex)
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Unhandled exception from CreateOrUpdateUserInformation");
        }
    }
}
