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
using System.Text;
using System.Threading.Tasks;

// The following using statements were added for this sample.
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace TodoListClient
{
    class TokenCacheKeyElements
    {
        public DateTimeOffset ExpiresOn { get; set; }

        public string Authority { get; set; }
        public string ClientId { get; set; }
        public string FamilyName { get; set; }
        public string GivenName { get; set; }
        public string IdentityProviderName { get; set; }
        public string Resource { get; set; }
        public string TenantId { get; set; }
        public string UserId { get; set; }

        public bool IsMultipleResourceRefreshToken { get; set; }
        public bool IsUserIdDisplayable { get; set; }
    }

    enum TokenCacheKeyElement
    {
        Authority = 0,
        ClientId = 1,
        ExpiresOn = 2,
        FamilyName = 3,
        GivenName = 4,
        IdentityProviderName = 5,
        IsMultipleResourceRefreshToken = 6,
        IsUserIdDisplayable = 7,
        Resource = 8,
        TenantId = 9,
        UserId = 10
    }

    public class CacheHelper
    {
        private const string ElementDelimiter = ":";
        private const string SegmentDelimiter = "::";

        public static string EncodeCacheKey(TokenCacheKey cacheKey)
        {
            var keyElements = new SortedDictionary<TokenCacheKeyElement, string>();

            keyElements[TokenCacheKeyElement.Authority] = cacheKey.Authority;

            if (!String.IsNullOrEmpty(cacheKey.Resource))
            {
                keyElements[TokenCacheKeyElement.Resource] = cacheKey.Resource;
            }

            if (!String.IsNullOrEmpty(cacheKey.ClientId))
            {
                keyElements[TokenCacheKeyElement.ClientId] = cacheKey.ClientId;
            }

            if (null != cacheKey.ExpiresOn)
            {
                keyElements[TokenCacheKeyElement.ExpiresOn] = cacheKey.ExpiresOn.ToString();
            }

            if (!String.IsNullOrEmpty(cacheKey.FamilyName))
            {
                keyElements[TokenCacheKeyElement.FamilyName] = cacheKey.FamilyName;
            }

            if (!String.IsNullOrEmpty(cacheKey.GivenName))
            {
                keyElements[TokenCacheKeyElement.GivenName] = cacheKey.GivenName;
            }

            if (!String.IsNullOrEmpty(cacheKey.IdentityProviderName))
            {
                keyElements[TokenCacheKeyElement.IdentityProviderName] = cacheKey.IdentityProviderName;
            }

            if (false != cacheKey.IsMultipleResourceRefreshToken)
            {
                keyElements[TokenCacheKeyElement.IsMultipleResourceRefreshToken] = cacheKey.IsMultipleResourceRefreshToken.ToString();
            }

            if (false != cacheKey.IsUserIdDisplayable)
            {
                keyElements[TokenCacheKeyElement.IsUserIdDisplayable] = cacheKey.IsUserIdDisplayable.ToString();
            }

            if (!String.IsNullOrEmpty(cacheKey.TenantId))
            {
                keyElements[TokenCacheKeyElement.TenantId] = cacheKey.TenantId;
            }

            if (!String.IsNullOrEmpty(cacheKey.UserId))
            {
                keyElements[TokenCacheKeyElement.UserId] = cacheKey.UserId;
            }

            return CreateFromKeyElements(keyElements);
        }

        internal static string CreateFromKeyElements(SortedDictionary<TokenCacheKeyElement, string> keyElements)
        {
            if (keyElements == null)
            {
                throw new ArgumentNullException("keyElements");
            }

            string keyHeader = String.Join(ElementDelimiter, keyElements.Keys.Select(k => (int)k));
            string keyContent = String.Join(ElementDelimiter, keyElements.Values.Select(Base64Encode));
            return CreateKey(keyHeader, keyContent);
        }

        private static string CreateKey(string keyHeader, string keyContent)
        {
            return String.Join(
                SegmentDelimiter,
                new[] { Base64Encode(keyHeader), Base64Encode(keyContent) });
        }

        internal static string Base64Encode(string input)
        {
            string encodedString = String.Empty;

            if (!String.IsNullOrEmpty(input))
            {
                encodedString = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
            }

            return encodedString;
        }

        internal static string Base64Decode(string encodedString)
        {
            string output = null;

            if (!String.IsNullOrEmpty(encodedString))
            {
                byte[] outputBytes = Convert.FromBase64String(encodedString);
                output = Encoding.UTF8.GetString(outputBytes, 0, outputBytes.Length);
            }

            return output;
        }

        public static TokenCacheKey DecodeCacheKey(string cacheKey)
        {
            var elements = new TokenCacheKey();
            IDictionary<TokenCacheKeyElement, string> elementDictionary = Decode(cacheKey);
            elements.Authority = elementDictionary.ContainsKey(TokenCacheKeyElement.Authority) ? elementDictionary[TokenCacheKeyElement.Authority] : null;
            elements.ClientId = elementDictionary.ContainsKey(TokenCacheKeyElement.ClientId) ? elementDictionary[TokenCacheKeyElement.ClientId] : null;
            elements.FamilyName = elementDictionary.ContainsKey(TokenCacheKeyElement.FamilyName) ? elementDictionary[TokenCacheKeyElement.FamilyName] : null;
            elements.GivenName = elementDictionary.ContainsKey(TokenCacheKeyElement.GivenName) ? elementDictionary[TokenCacheKeyElement.GivenName] : null;
            elements.IdentityProviderName = elementDictionary.ContainsKey(TokenCacheKeyElement.IdentityProviderName) ? elementDictionary[TokenCacheKeyElement.IdentityProviderName] : null;
            elements.Resource = elementDictionary.ContainsKey(TokenCacheKeyElement.Resource) ? elementDictionary[TokenCacheKeyElement.Resource] : null;
            elements.TenantId = elementDictionary.ContainsKey(TokenCacheKeyElement.TenantId) ? elementDictionary[TokenCacheKeyElement.TenantId] : null;
            elements.UserId = elementDictionary.ContainsKey(TokenCacheKeyElement.UserId) ? elementDictionary[TokenCacheKeyElement.UserId] : null;

            if (elementDictionary.ContainsKey(TokenCacheKeyElement.ExpiresOn))
            {
                elements.ExpiresOn = DateTimeOffset.Parse(elementDictionary[TokenCacheKeyElement.ExpiresOn]);
            }

            if (elementDictionary.ContainsKey(TokenCacheKeyElement.IsUserIdDisplayable))
            {
                elements.IsUserIdDisplayable = bool.Parse(elementDictionary[TokenCacheKeyElement.IsUserIdDisplayable]);
            }

            if (elementDictionary.ContainsKey(TokenCacheKeyElement.IsMultipleResourceRefreshToken))
            {
                elements.IsMultipleResourceRefreshToken = bool.Parse(elementDictionary[TokenCacheKeyElement.IsMultipleResourceRefreshToken]);
            }

            return elements;
        }

        internal static Dictionary<TokenCacheKeyElement, string> Decode(string cacheKey)
        {
            string[] keySegments = cacheKey.Split(new[] { SegmentDelimiter }, StringSplitOptions.None);

            if (keySegments.Length != 2)
            {
                throw new ArgumentException("invalid key format", "key");
            }

            string[] headerElements = Base64Decode(keySegments[0]).Split(new[] { ElementDelimiter }, StringSplitOptions.None);
            string[] contentElements = Base64Decode(keySegments[1]).Split(new[] { ElementDelimiter }, StringSplitOptions.None);

            if (headerElements.Length != contentElements.Length)
            {
                throw new ArgumentException("iunvalid key format", "key");
            }

            var keyElementDictionary = new Dictionary<TokenCacheKeyElement, string>();
            for (int i = 0; i < headerElements.Length; i++)
            {
                keyElementDictionary.Add((TokenCacheKeyElement)Enum.Parse(typeof(TokenCacheKeyElement), headerElements[i]), Base64Decode(contentElements[i]));
            }

            return keyElementDictionary;
        }
    }
}
