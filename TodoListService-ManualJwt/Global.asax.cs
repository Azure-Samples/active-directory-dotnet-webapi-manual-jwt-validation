//----------------------------------------------------------------------------------------------
//    Copyright 2014 Microsoft Corporation
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//----------------------------------------------------------------------------------------------

using System;
using System.Configuration;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
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
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

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

    internal class TokenValidationHandler : DelegatingHandler
    {
        //
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Tenant is the name of the tenant in which this application is registered.
        // The Authority is the sign-in URL of the tenant.
        // The Audience is the value of one of the 'aud' claims the service expects to find in token to assure the token is addressed to it.
        //

        private string _audience;
        private string _authority;
        private string _clientId;
        private ConfigurationManager<OpenIdConnectConfiguration> _configManager;
        private const string _scopeClaimType = "http://schemas.microsoft.com/identity/claims/scope";
        private ISecurityTokenValidator _tokenValidator;

        public TokenValidationHandler()
        {
            _audience = ConfigurationManager.AppSettings["ida:Audience"];
            _clientId = ConfigurationManager.AppSettings["ida:ClientId"];
            var aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
            var tenant = ConfigurationManager.AppSettings["ida:Tenant"];
            _authority = string.Format(CultureInfo.InvariantCulture, aadInstance, tenant);
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
            // check there is a jwt in the authorization header, return 'Unauthorized' error if the token is null.
            if (request.Headers.Authorization == null || request.Headers.Authorization.Parameter == null)
                return BuildResponseErrorMessage(HttpStatusCode.Unauthorized);

            OpenIdConnectConfiguration config = null;
            try
            {
                config = await _configManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                // App Id URI and AppId of this service application are both valid audiences.
                ValidAudiences = new[] { _audience, _clientId },
                // Support Azure AD V1 and V2 endpoints.
                ValidIssuers = new[] { config.Issuer, $"{config.Issuer}/v2.0" },
                IssuerSigningKeys = config.SigningKeys
            };

            try
            {
                // Validate token.
                var claimsPrincipal = _tokenValidator.ValidateToken(request.Headers.Authorization.Parameter, validationParameters, out SecurityToken _);

                // Set the ClaimsPrincipal on the current thread.
                Thread.CurrentPrincipal = claimsPrincipal;

                // Set the ClaimsPrincipal on HttpContext.Current if the app is running in web hosted environment.
                if (HttpContext.Current != null)
                    HttpContext.Current.User = claimsPrincipal;

                // If the token is scoped, verify that required permission is set in the scope claim.
                if (ClaimsPrincipal.Current.FindFirst(_scopeClaimType) != null && ClaimsPrincipal.Current.FindFirst(_scopeClaimType).Value != "user_impersonation")
                    return BuildResponseErrorMessage(HttpStatusCode.Forbidden);

                return await base.SendAsync(request, cancellationToken);
            }
            catch (SecurityTokenValidationException)
            {
                return BuildResponseErrorMessage(HttpStatusCode.Unauthorized);
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        private HttpResponseMessage BuildResponseErrorMessage(HttpStatusCode statusCode)
        {
            var response = new HttpResponseMessage(statusCode);

            // The Scheme should be "Bearer", authorization_uri should point to the tenant url and resource_id should point to the audience.
            var authenticateHeader = new AuthenticationHeaderValue("Bearer", "authorization_uri=\"" + _authority + "\"" + "," + "resource_id=" + _audience);
            response.Headers.WwwAuthenticate.Add(authenticateHeader);
            return response;
        }
    }
}
