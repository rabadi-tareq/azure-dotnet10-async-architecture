// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using Azure.Identity;
using Microsoft.IdentityModel.Logging;
using AsyncTickets.Web.Api.Extensions;
using AsyncTickets.Web.Api.Infrastructure;
using AsyncTickets.Web.CallCenter.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var hasRequiredConfigSettings = !string.IsNullOrEmpty(builder.Configuration["App:AppConfig:Uri"]);

if (hasRequiredConfigSettings)
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options
            .Connect(new Uri(builder.Configuration["App:AppConfig:Uri"]), new DefaultAzureCredential())
            .UseFeatureFlags() // Feature flags will be loaded and, by default, refreshed every 30 seconds
            .ConfigureKeyVault(kv =>
            {
                // Some of the values coming from Azure App Configuration are stored Key Vault, use
                // the managed identity of this host for the authentication.
                kv.SetCredential(new DefaultAzureCredential());
            });
    });
}

// enable developers to override settings with user secrets
builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Logging.AddConsole();
builder.Logging.AddOpenTelemetry(o =>
{
    o.IncludeFormattedMessage = true;
    o.IncludeScopes = true;
});

if (builder.Environment.IsDevelopment())
{
    IdentityModelEventSource.ShowPII = true;
}

// Add services to the container.
if (hasRequiredConfigSettings)
{
    builder.Services.AddCallCenterApiServices(builder.Configuration);
}

var hasMicrosoftEntraIdSettings = !string.IsNullOrEmpty(builder.Configuration["Api:MicrosoftEntraId:ClientId"]);

var app = builder.Build();

if (hasRequiredConfigSettings)
{
    // Allows refreshing configuration values from Azure App Configuration
    app.UseAzureAppConfiguration();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    using (var serviceScope = app.Services.CreateScope())
    {
        serviceScope.ServiceProvider.GetRequiredService<ApplicationInitializer>().Initialize();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseIntermittentErrorRequestMiddleware();

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/healthz");

    app.MapGet("/", () => "Default Web API endpoint");
    app.MapControllers();
}
else if (!hasMicrosoftEntraIdSettings)
{
    app.MapGet("/", () => "Could not find required Microsoft Entra ID settings. Check your App Config Service, you may need to run the createAppRegistrations script.");
}
else
{
    app.MapGet("/", () => "Could not find required settings. Check your App Service's Configuration section to verify the required settings.");
}

app.Run();
