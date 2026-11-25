// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using Azure.Identity;
using Microsoft.IdentityModel.Logging;
using AsyncTickets.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);
var azureCredential = new DefaultAzureCredential();

var hasRequiredConfigSettings = !string.IsNullOrEmpty(builder.Configuration["App:AppConfig:Uri"]);

if (hasRequiredConfigSettings)
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        options
            .Connect(new Uri(builder.Configuration["App:AppConfig:Uri"]), azureCredential)
            .ConfigureKeyVault(kv =>
            {
                // Some of the values coming from Azure App Configuration are stored Key Vault, use
                // the managed identity of this host for the authentication.
                kv.SetCredential(azureCredential);
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
    builder.Services.AddCallCenterWebServices(builder.Configuration, azureCredential);
}

var hasMicrosoftEntraIdSettings = !string.IsNullOrEmpty(builder.Configuration["MicrosoftEntraId:ClientId"]);

var app = builder.Build();

if (hasRequiredConfigSettings && hasMicrosoftEntraIdSettings)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    // this sample uses AFD for the URL registered with Microsoft Entra ID to make it easier to get started
    // but we recommend host name preservation for production scenarios
    // https://learn.microsoft.com/en-us/azure/architecture/best-practices/host-name-preservation
    app.UseForwardedHeaders();

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseSession(); // required for carts

    app.MapHealthChecks("/healthz");

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
    app.MapRazorPages();
}
else if (!hasMicrosoftEntraIdSettings)
{
    app.MapGet("/", () => $"" +
    "Could not find required Microsoft Entra ID settings. Check your App Config Service, you may need to run the create-app-registrations script.");
}
else
{
    app.MapGet("/", () => "Could not find required settings. Check your App Service's Configuration section to verify the required settings.");
}

app.Run();
