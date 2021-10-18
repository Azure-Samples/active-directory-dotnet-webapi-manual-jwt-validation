using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Web;

namespace TodoListService_ManualJwt.Services
{
    /// <summary>
    /// Custom token handler to apply custom logic to token validation
    /// </summary>
    /// <seealso cref="JwtSecurityTokenHandler" />
    public class CustomTokenHandler : JwtSecurityTokenHandler
    {
        public override ClaimsPrincipal ValidateToken(
            string token, TokenValidationParameters validationParameters,
            out SecurityToken validatedToken)
        {
            try
            {
                var claimsPrincipal = base.ValidateToken(token, validationParameters, out validatedToken);

                // Custom token validation to allow callers from a list of whitelisted tenants
                string[] allowedTenants = { "14c2f153-90a7-4689-9db7-9543bf084dad", "af8cc1a0-d2aa-4ca7-b829-00d361edb652", "979f4440-75dc-4664-b2e1-2cafa0ac67d1", "4d39e77c-b0f3-4253-ae0b-7068ddd47949", "556b80b7-c9fc-41fd-92da-c3635f7918e5" };
                string tenantId = claimsPrincipal.Claims.FirstOrDefault(x => x.Type == "tid" || x.Type == "http://schemas.microsoft.com/identity/claims/tenantid")?.Value;

                if (!allowedTenants.Contains(tenantId))
                {
                    throw new Exception("This tenant is not authorized to this web api");
                }

                return claimsPrincipal;
            }
            catch (Exception e)
            {

                throw;
            }
        }
    }
}