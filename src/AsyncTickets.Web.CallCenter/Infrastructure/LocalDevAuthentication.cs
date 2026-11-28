// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace AsyncTickets.Web.CallCenter.Infrastructure
{
    /// <summary>
    /// Local-emulator-only authentication. Every request is authenticated as a fixed administrator.
    /// Registered only when App:UseLocalEmulators is true.
    /// </summary>
    public class LocalDevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "LocalDev";

        public LocalDevAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "Local Developer"),
                new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "00000000-0000-0000-0000-000000000001"),
                new Claim(ClaimTypes.Role, Roles.Administrator)
            ], SchemeName);

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    /// <summary>
    /// Local-emulator-only stand-in for ITokenAcquisition. The local API accepts any request.
    /// </summary>
    public class LocalDevTokenAcquisition : ITokenAcquisition
    {
        private const string Token = "local-dev-token";

        public Task<string> GetAccessTokenForUserAsync(IEnumerable<string> scopes, string? authenticationScheme = null, string? tenantId = null, string? userFlow = null, ClaimsPrincipal? user = null, TokenAcquisitionOptions? tokenAcquisitionOptions = null)
            => Task.FromResult(Token);

        public Task<string> GetAccessTokenForAppAsync(string scope, string? authenticationScheme = null, string? tenant = null, TokenAcquisitionOptions? tokenAcquisitionOptions = null)
            => Task.FromResult(Token);

        public Task<Microsoft.Identity.Client.AuthenticationResult> GetAuthenticationResultForUserAsync(IEnumerable<string> scopes, string? authenticationScheme = null, string? tenantId = null, string? userFlow = null, ClaimsPrincipal? user = null, TokenAcquisitionOptions? tokenAcquisitionOptions = null)
            => throw new NotSupportedException("Not available in local emulator mode.");

        public Task<Microsoft.Identity.Client.AuthenticationResult> GetAuthenticationResultForAppAsync(string scope, string? authenticationScheme = null, string? tenant = null, TokenAcquisitionOptions? tokenAcquisitionOptions = null)
            => throw new NotSupportedException("Not available in local emulator mode.");

        public void ReplyForbiddenWithWwwAuthenticateHeader(IEnumerable<string> scopes, Microsoft.Identity.Client.MsalUiRequiredException msalServiceException, string? authenticationScheme, HttpResponse? httpResponse = null)
            => throw new NotSupportedException("Not available in local emulator mode.");

        public string GetEffectiveAuthenticationScheme(string? authenticationScheme)
            => authenticationScheme ?? LocalDevAuthenticationHandler.SchemeName;

        public Task ReplyForbiddenWithWwwAuthenticateHeaderAsync(IEnumerable<string> scopes, Microsoft.Identity.Client.MsalUiRequiredException msalServiceException, HttpResponse? httpResponse = null)
            => throw new NotSupportedException("Not available in local emulator mode.");
    }
}
