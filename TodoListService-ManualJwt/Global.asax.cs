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
using System.Net;

// The following using statements were added for this sample.
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

    internal class TokenValidationHandler : DelegatingHandler
    {
        //
        // The AAD Instance is the instance of Azure, for example public Azure or Azure China.
        // The Tenant is the name of the tenant in which this application is registered.
        // The Authority is the sign-in URL of the tenant.
        // The Audience is the value the service expects to see in tokens that are addressed to it.
        //
        private static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];

        private static string tenant = ConfigurationManager.AppSettings["ida:Tenant"];
        private static string audience = ConfigurationManager.AppSettings["ida:Audience"];
        private static string clientId = ConfigurationManager.AppSettings["ida:ClientId"];
        private string authority = String.Format(CultureInfo.InvariantCulture, aadInstance, tenant);

        private static string _issuer = string.Empty;
        private static ICollection<SecurityKey> _signingKeys = null;
        private static DateTime _stsMetadataRetrievalTime = DateTime.MinValue;
        private static string scopeClaimType = "http://schemas.microsoft.com/identity/claims/scope";

        //
        // SendAsync checks that incoming requests have a valid access token, and sets the current user identity using that access token.
        //
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Get the jwt bearer token from the authorization header
            string jwtToken = null;
            AuthenticationHeaderValue authHeader = request.Headers.Authorization;
            if (authHeader != null)
            {
                jwtToken = authHeader.Parameter;
            }

            if (jwtToken == null)
            {
                HttpResponseMessage response = this.BuildResponseErrorMessage(HttpStatusCode.Unauthorized);
                return response;
            }

            string issuer;
            ICollection<SecurityKey> signingKeys;

            try
            {
                // The issuer and signingKeys are cached for 24 hours. They are updated if any of the conditions in the if condition is true.
                if (DateTime.UtcNow.Subtract(_stsMetadataRetrievalTime).TotalHours > 24
                    || string.IsNullOrEmpty(_issuer)
                    || _signingKeys == null)
                {
                    // Get tenant information that's used to validate incoming jwt tokens
                    string stsDiscoveryEndpoint = $"{this.authority}/.well-known/openid-configuration";
                    var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(stsDiscoveryEndpoint, new OpenIdConnectConfigurationRetriever());
                    var config = await configManager.GetConfigurationAsync(cancellationToken);
                    _issuer = config.Issuer;
                    _signingKeys = config.SigningKeys;

                    _stsMetadataRetrievalTime = DateTime.UtcNow;
                }

                issuer = _issuer;
                signingKeys = _signingKeys;
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                // We accept both the App Id URI and the AppId of this service application
                ValidAudiences = new[] { audience, clientId },

                // Supports both the Azure AD V1 and V2 endpoint
                ValidIssuers = new[] { issuer, $"{issuer}/v2.0" },
                IssuerValidator = AadIssuerValidator.GetIssuerValidator(authority).ValidateAadIssuer,
                IssuerSigningKeys = signingKeys
            };

            try
            {
                // Validate token.
                SecurityToken validatedToken = new JwtSecurityToken();
                ClaimsPrincipal claimsPrincipal = tokenHandler.ValidateToken(jwtToken, validationParameters, out validatedToken);

                // Set the ClaimsPrincipal on the current thread.
                Thread.CurrentPrincipal = claimsPrincipal;

                // Set the ClaimsPrincipal on HttpContext.Current if the app is running in web hosted environment.
                if (HttpContext.Current != null)
                {
                    HttpContext.Current.User = claimsPrincipal;
                }

                // If the token is scoped, verify that required permission is set in the scope claim.
                if (ClaimsPrincipal.Current.FindFirst(scopeClaimType) != null && ClaimsPrincipal.Current.FindFirst(scopeClaimType).Value != "user_impersonation")
                {
                    HttpResponseMessage response = this.BuildResponseErrorMessage(HttpStatusCode.Forbidden);
                    return response;
                }

                return await base.SendAsync(request, cancellationToken);
            }
            catch (SecurityTokenValidationException)
            {
                HttpResponseMessage response = this.BuildResponseErrorMessage(HttpStatusCode.Unauthorized);
                return response;
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        private HttpResponseMessage BuildResponseErrorMessage(HttpStatusCode statusCode)
        {
            HttpResponseMessage response = new HttpResponseMessage(statusCode);

            //
            // The Scheme should be "Bearer", authorization_uri should point to the tenant url and resource_id should point to the audience.
            //
            AuthenticationHeaderValue authenticateHeader = new AuthenticationHeaderValue("Bearer", "authorization_uri=\"" + this.authority + "\"" + "," + "resource_id=" + audience);

            response.Headers.WwwAuthenticate.Add(authenticateHeader);

            return response;
        }
    }
}