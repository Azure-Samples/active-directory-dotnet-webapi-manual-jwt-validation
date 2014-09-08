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
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

// The following using statements were added for this sample.
using System.Net.Http;
using System.IdentityModel.Tokens;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.IdentityModel.Selectors;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.IdentityModel.Metadata;
using System.ServiceModel.Security;
using System.Xml;
using System.Security.Cryptography.X509Certificates;
using System.Globalization;
using System.Configuration;
using Microsoft.IdentityModel.Protocols;

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
        static string aadInstance = ConfigurationManager.AppSettings["ida:AADInstance"];
        static string tenant = ConfigurationManager.AppSettings["ida:Tenant"];
        static string audience = ConfigurationManager.AppSettings["ida:Audience"];
        string authority = String.Format(CultureInfo.InvariantCulture, aadInstance, tenant);

        static string _issuer = string.Empty;
        static List<SecurityToken> _signingTokens = null;
        static DateTime _stsMetadataRetrievalTime = DateTime.MinValue;
        static string scopeClaimType = "http://schemas.microsoft.com/identity/claims/scope";
        
        //
        // SendAsync checks that incoming requests have a valid access token, and sets the current user identity using that access token.
        //
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string authHeader = null;
            string jwtToken = null;
            string issuer;
            string stsDiscoveryEndpoint = string.Format("{0}/.well-known/openid-configuration", authority);

            List<SecurityToken> signingTokens;

            // The header is of the form "bearer <accesstoken>", so extract to the right of the whitespace to find the access token.
            authHeader = HttpContext.Current.Request.Headers["Authorization"];
            if (authHeader != null)
            {
                int startIndex = authHeader.LastIndexOf(' ');
                if (startIndex > 0)
                {
                    jwtToken = authHeader.Substring(startIndex).Trim();
                }
            }

            if (jwtToken == null)
            {
                HttpResponseMessage response = BuildResponseErrorMessage(HttpStatusCode.Unauthorized);
                return response;
            }

            try
            {
                // The issuer and signingTokens are cached for 24 hours. They are updated if any of the conditions in the if condition is true.            
                if (DateTime.UtcNow.Subtract(_stsMetadataRetrievalTime).TotalHours > 24
                    || string.IsNullOrEmpty(_issuer)
                    || _signingTokens == null)
                {
                    // Get tenant information that's used to validate incoming jwt tokens
                    ConfigurationManager<OpenIdConnectConfiguration> configManager = new ConfigurationManager<OpenIdConnectConfiguration>(stsDiscoveryEndpoint);
                    OpenIdConnectConfiguration config = await configManager.GetConfigurationAsync();
                    _issuer = config.Issuer;
                    _signingTokens = config.SigningTokens.ToList();
                    
                    _stsMetadataRetrievalTime = DateTime.UtcNow;
                }

                issuer = _issuer;
                signingTokens = _signingTokens;
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();

            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                ValidAudience = audience,
                ValidIssuer = issuer,
                IssuerSigningTokens = signingTokens,
                CertificateValidator = X509CertificateValidator.None
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
                    HttpResponseMessage response = BuildResponseErrorMessage(HttpStatusCode.Forbidden);
                    return response;
                }

                return await base.SendAsync(request, cancellationToken);
            }
            catch (SecurityTokenValidationException)
            {
                HttpResponseMessage response = BuildResponseErrorMessage(HttpStatusCode.Unauthorized);
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
            AuthenticationHeaderValue authenticateHeader = new AuthenticationHeaderValue("Bearer", "authorization_uri=\"" + authority + "\"" + "," + "resource_id=" + audience);

            response.Headers.WwwAuthenticate.Add(authenticateHeader);

            return response;
        }
    }
}
