/*
 The MIT License (MIT)

Copyright (c) 2018 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace TodoListService_ManualJwt
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            GlobalConfiguration.Configuration.MessageHandlers.Add(new TokenValidationHandler());
        }
    }

    /// <summary>This Message handler inspects incoming access tokens and validates them.</summary>
    /// <seealso cref="System.Net.Http.DelegatingHandler" />
    internal class TokenValidationHandler : DelegatingHandler
    {
        //
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Tenant is the name of the tenant in which this application is registered.
        // The Authority is the sign-in URL of the tenant.
        // The Audience is the value of one of the 'aud' claims the service expects to find in token to assure the token is addressed to it.

        private string _audience;
        private string _authority;
        private string _clientId;
        private ConfigurationManager<OpenIdConnectConfiguration> _configManager;
        private string _tenant;
        private ISecurityTokenValidator _tokenValidator;

        public TokenValidationHandler()
        {
            _audience = ConfigurationManager.AppSettings["ida:Audience"];
            _clientId = ConfigurationManager.AppSettings["ida:ClientId"];
            var aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
            _tenant = ConfigurationManager.AppSettings["ida:TenantId"];
            _authority = string.Format(CultureInfo.InvariantCulture, aadInstance, _tenant);
            _configManager = new ConfigurationManager<OpenIdConnectConfiguration>($"{_authority}/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever());
            _tokenValidator = new JwtSecurityTokenHandler();
        }

        /// <summary>
        /// Checks that incoming requests have a valid access token, and sets the current user identity using that access token.
        /// </summary>
        /// <param name="request">the current <see cref="HttpRequestMessage"/>.</param>
        /// <param name="cancellationToken">a <see cref="CancellationToken"/> set by application.</param>
        /// <returns>A <see cref="HttpResponseMessage"/>.</returns>
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // For debugging/development purposes, one can enable additional detail in exceptions by setting IdentityModelEventSource.ShowPII to true.
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;

            // check if there is a jwt in the authorization header, return 'Unauthorized' error if the token is null.
            if (request.Headers.Authorization == null || request.Headers.Authorization.Parameter == null)
                return BuildResponseErrorMessage(HttpStatusCode.Unauthorized);

            // Pull OIDC discovery document from Azure AD. For example, the tenant-independent version of the document is located
            // at https://login.microsoftonline.com/common/.well-known/openid-configuration.
            OpenIdConnectConfiguration config = null;
            try
            {
                config = await _configManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
#if DEBUG
                return BuildResponseErrorMessage(HttpStatusCode.InternalServerError, ex.Message);
#else
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
#endif
            }

            // You can get a list of issuers for the various Azure AD deployments (global & sovereign) from the following endpoint
            //https://login.microsoftonline.com/common/discovery/instance?authorization_endpoint=https://login.microsoftonline.com/common/oauth2/v2.0/authorize&api-version=1.1;

            IList<string> validissuers = new List<string>()
            {
                $"https://login.microsoftonline.com/{_tenant}/",
                $"https://login.microsoftonline.com/{_tenant}/v2.0",
                $"https://login.windows.net/{_tenant}/",
                $"https://login.microsoft.com/{_tenant}/",
                $"https://sts.windows.net/{_tenant}/"
            };

            // Initialize the token validation parameters
            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                // App Id URI and AppId of this service application are both valid audiences.
                ValidAudiences = new[] { _audience, _clientId },

                // Support Azure AD V1 and V2 endpoints.
                ValidIssuers = validissuers,
                IssuerSigningKeys = config.SigningKeys

                // Please inspect TokenValidationParameters class for a lot more validation parameters.
            };

            try
            {
                // Validate token.
                SecurityToken securityToken;
                var claimsPrincipal = _tokenValidator.ValidateToken(request.Headers.Authorization.Parameter, validationParameters, out securityToken);

#pragma warning disable 1998
                // This check is required to ensure that the Web API only accepts tokens from tenants where it has been consented to and provisioned.
                if (!claimsPrincipal.Claims.Any(x => x.Type == ClaimConstants.ScopeClaimType)
                    && !claimsPrincipal.Claims.Any(y => y.Type == ClaimConstants.ScpClaimType)
                    && !claimsPrincipal.Claims.Any(y => y.Type == ClaimConstants.RolesClaimType))
                {
#if DEBUG
                    return BuildResponseErrorMessage(HttpStatusCode.Forbidden, "Neither 'scope' or 'roles' claim was found in the bearer token.");
#else
                    return BuildResponseErrorMessage(HttpStatusCode.Forbidden);
#endif
                }
#pragma warning restore 1998

                // Set the ClaimsPrincipal on the current thread.
                Thread.CurrentPrincipal = claimsPrincipal;

                // Set the ClaimsPrincipal on HttpContext.Current if the app is running in web hosted environment.
                if (HttpContext.Current != null)
                    HttpContext.Current.User = claimsPrincipal;

                // If the token is scoped, verify that required permission is set in the scope claim. This could be done later at the controller level as well
                if (ClaimsPrincipal.Current.FindFirst(ClaimConstants.ScopeClaimType).Value != ClaimConstants.ScopeClaimValue)
                    return BuildResponseErrorMessage(HttpStatusCode.Forbidden);

                return await base.SendAsync(request, cancellationToken);
            }
            catch (SecurityTokenValidationException stex)
            {
#if DEBUG
                return BuildResponseErrorMessage(HttpStatusCode.Unauthorized, stex.Message);
#else
                return BuildResponseErrorMessage(HttpStatusCode.Unauthorized);
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                return BuildResponseErrorMessage(HttpStatusCode.InternalServerError, ex.Message);
#else
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
#endif
            }
        }

        private HttpResponseMessage BuildResponseErrorMessage(HttpStatusCode statusCode, string error_description = "")
        {
            var response = new HttpResponseMessage(statusCode);

            // The Scheme should be "Bearer", authorization_uri should point to the tenant url and resource_id should point to the audience.
            var authenticateHeader = new AuthenticationHeaderValue("Bearer", "authorization_uri=\"" + _authority + "\"" + "," + "resource_id=" + _audience + $",error_description={error_description}");
            response.Headers.WwwAuthenticate.Add(authenticateHeader);
            return response;
        }
    }
}