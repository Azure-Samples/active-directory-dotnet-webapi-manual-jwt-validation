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
        static List<X509SecurityToken> _signingTokens = null;
        static DateTime _stsMetadataRetrievalTime = DateTime.MinValue;
        static string scopeClaimType = "http://schemas.microsoft.com/identity/claims/scope";
        
        //
        // SendAsync checks that incoming requests have a valid access token, and sets the current user identity using that access token.
        //
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string authHeader = null;
            string jwtToken = null;
            string issuer;
            string stsMetadataAddress = string.Format("{0}/federationmetadata/2007-06/federationmetadata.xml", authority);

            List<X509SecurityToken> signingTokens;

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
                return Task.FromResult(response);
            }

            try
            {
                // Get tenant information that's used to validate incoming jwt tokens
                GetTenantInformation(stsMetadataAddress, out issuer, out signingTokens);
            }
            catch (Exception)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler()
            {
                // Disable certificate validation.  Certificate validation is not necessary since the AAD signing certificate is a self-signed certificate.
                CertificateValidator = X509CertificateValidator.None
            };

            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                AllowedAudience = audience,
                ValidIssuer = issuer,
                SigningTokens = signingTokens
            };

            try
            {
                // Validate token.
                ClaimsPrincipal claimsPrincipal = tokenHandler.ValidateToken(jwtToken, validationParameters);

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
                    return Task.FromResult(response);
                }

                return base.SendAsync(request, cancellationToken);
            }
            catch (SecurityTokenValidationException)
            {
                HttpResponseMessage response = BuildResponseErrorMessage(HttpStatusCode.Unauthorized);
                return Task.FromResult(response);
            }
            catch (Exception)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
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

        /// <summary>
        /// Parses the federation metadata document and gets issuer Name and Signing Certificates
        /// </summary>
        /// <param name="metadataAddress">URL of the Federation Metadata document</param>
        /// <param name="issuer">Issuer Name</param>
        /// <param name="signingTokens">Signing Certificates in the form of X509SecurityToken</param>
        static void GetTenantInformation(string metadataAddress, out string issuer, out List<X509SecurityToken> signingTokens)
        {
            signingTokens = new List<X509SecurityToken>();

            // The issuer and signingTokens are cached for 24 hours. They are updated if any of the conditions in the if condition is true.            
            if (DateTime.UtcNow.Subtract(_stsMetadataRetrievalTime).TotalHours > 24
                || string.IsNullOrEmpty(_issuer)
                || _signingTokens == null)
            {
                MetadataSerializer serializer = new MetadataSerializer()
                    {
                        // Disable certificate validation.  Certificate validation is not necessary since the AAD signing certificate is a self-signed certificate.
                        CertificateValidationMode = X509CertificateValidationMode.None
                    };
                MetadataBase metadata = serializer.ReadMetadata(XmlReader.Create(metadataAddress));

                EntityDescriptor entityDescriptor = (EntityDescriptor)metadata;

                // Get the issuer name.
                if (!string.IsNullOrWhiteSpace(entityDescriptor.EntityId.Id))
                {
                    _issuer = entityDescriptor.EntityId.Id;
                }

                // Get the signing certs.
                _signingTokens = ReadSigningCertsFromMetadata(entityDescriptor);

                _stsMetadataRetrievalTime = DateTime.UtcNow;
            }

            issuer = _issuer;
            signingTokens = _signingTokens;
        }

        static List<X509SecurityToken> ReadSigningCertsFromMetadata(EntityDescriptor entityDescriptor)
        {
            List<X509SecurityToken> stsSigningTokens = new List<X509SecurityToken>();

            SecurityTokenServiceDescriptor stsd = entityDescriptor.RoleDescriptors.OfType<SecurityTokenServiceDescriptor>().First();

            if (stsd != null)
            {
                IEnumerable<X509RawDataKeyIdentifierClause> x509DataClauses = stsd.Keys.Where(key => key.KeyInfo != null && (key.Use == KeyType.Signing || key.Use == KeyType.Unspecified)).
                                                             Select(key => key.KeyInfo.OfType<X509RawDataKeyIdentifierClause>().First());

                stsSigningTokens.AddRange(x509DataClauses.Select(token => new X509SecurityToken(new X509Certificate2(token.GetX509RawData()))));
            }
            else
            {
                throw new InvalidOperationException("There is no RoleDescriptor of type SecurityTokenServiceType in the metadata");
            }

            return stsSigningTokens;
        }
    }
}
