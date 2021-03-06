﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthenticateAttribute.cs" company="Megadotnet">
//   AuthenticateAttribute
// </copyright>
// <summary>
//   AuthenticateAttribute
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebApi2
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.Caching;
    using System.Web;
    using System.Web.Http.Controllers;
    using System.Web.Http.Filters;

    using BusinessObject.Auth;
    using IronFramework.Utility;
    using Newtonsoft.Json;
    using IronFramework.Common.Logging.Logger;
    using Newtonsoft.Json.Converters;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    ///     Authenticate Attribute with HMAC
    /// </summary>
    /// <example>
    /// <code>
    ///      private void CreateAuthenticationHeader(HttpClient client, string date, string querystring, string routingUrl, HttpMethod httpMethod)
    ///        {
    ///            string message = string.Join("\n", httpMethod.Method, date, routingUrl.ToLower(), querystring);
    ///            log.DebugFormat("Client side Message {0}", message);
    ///            Hashtable remoteDataSource =
    ///(Hashtable)WebConfigurationManager.GetSection(this.Section);
    ///            string password = (string)remoteDataSource["password"];
    ///            string token = VerifyTransactionSN.ComputeHash(password, message);
    ///            log.DebugFormat("Client side token {0}", token);
    ///            client.DefaultRequestHeaders.Add("Authentication", string.Format("{0}:{1}", password, token));
    ///            client.DefaultRequestHeaders.Add("Timestamp", date);
    ///        } 
    /// </code>
    /// </example>
    /// <see cref="https://developer.mozilla.org/en-US/docs/Web/HTTP/Authentication"/>
    public class AuthenticateAttribute : ActionFilterAttribute
    {
        #region Static Fields

        /// <summary>
        /// The authentication header name.
        /// </summary>
        private static readonly string AuthenticationHeaderName = "Authentication";

        /// <summary>
        /// The timestamp header name.
        /// </summary>
        private static readonly string TimestampHeaderName = "Timestamp";


        /// <summary>
        /// The log name
        /// </summary>
        private static readonly ILogger log = new Logger("AuthenticateAttribute");

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets the repository.
        /// </summary>
        public IAccountRepository Repository { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// The on action executing.
        /// </summary>
        /// <param name="actionContext">
        /// The action context.
        /// </param>
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            bool isAuthenticated = this.IsAuthenticated(actionContext);

            if (!isAuthenticated)
            {
                var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                actionContext.Response = response;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// The add name values to collection.
        /// </summary>
        /// <param name="parameterCollection">
        /// The parameter collection.
        /// </param>
        /// <param name="nameValueCollection">
        /// The name value collection.
        /// </param>
        private static void AddNameValuesToCollection(
            List<KeyValuePair<string, string>> parameterCollection, 
            NameValueCollection nameValueCollection)
        {
            if (!nameValueCollection.AllKeys.Any())
            {
                return;
            }

            foreach (string key in nameValueCollection.AllKeys)
            {
                string value = nameValueCollection[key];
                var pair = new KeyValuePair<string, string>(key, HttpUtility.UrlEncode(value));

                parameterCollection.Add(pair);
            }
        }

        /// <summary>
        /// The add to memory cache.
        /// </summary>
        /// <param name="signature">
        /// The signature.
        /// </param>
        private static void AddToMemoryCache(string signature)
        {
            MemoryCache memoryCache = MemoryCache.Default;
            if (!memoryCache.Contains(signature))
            {
                DateTimeOffset expiration = DateTimeOffset.UtcNow.AddMinutes(5);
                memoryCache.Add(signature, signature, expiration);
            }
        }

        /// <summary>
        /// The build base string.
        /// </summary>
        /// <param name="actionContext">
        /// The action context.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string BuildBaseString(HttpActionContext actionContext)
        {
            HttpRequestHeaders headers = actionContext.Request.Headers;
            string date = GetHttpRequestHeader(headers, TimestampHeaderName);

            string methodType = actionContext.Request.Method.Method;

            string absolutePath = actionContext.Request.RequestUri.AbsolutePath.ToLower();
            string uri = HttpContext.Current.Server.UrlDecode(absolutePath);

            string parameterMessage = BuildParameterMessage(actionContext);
            string message = string.Join("\n", methodType, date, uri, parameterMessage);

            // string message = string.Join("\n", methodType, date, uri);
            return message;
        }

        /// <summary>
        /// The build parameter collection.
        /// </summary>
        /// <param name="actionContext">
        /// The action context.
        /// </param>
        /// <returns>
        /// The <see cref="List"/>.
        /// </returns>
        private static List<KeyValuePair<string, string>> BuildParameterCollection(HttpActionContext actionContext)
        {
            // Use the list of keyvalue pair in order to allow the same key instead of dictionary
            var parameterCollection = new List<KeyValuePair<string, string>>();

            var queryStringCollection = actionContext.Request.RequestUri.ParseQueryString();

            #region Sample Data
            //POST http://localhost:3250/api/Values/PostCity HTTP/1.1
            //Timestamp: 2015-10-07 06:09:56Z
            //Content-Type: application/x-www-form-urlencoded
            //Authentication: password:aaI2g+jsdonUOsff/jdrpZVJ29ekYb9kEALhPrO/o8=
            //IcaoCode=ZPWS&CityShortName=文山 
            #endregion

            var formCollection = HttpContext.Current.Request.Form;

            AddNameValuesToCollection(parameterCollection, queryStringCollection);

            //Just for empty formCollection When pass JSON request
            if (parameterCollection.Count==0 && formCollection.Count == 0)
            {
                //For JSON string in HTTP Request Body
                #region JSON string in HTTP Request Body


                var actionArguments=actionContext.ActionArguments;
                foreach(var argDic in actionArguments)
                {
                    var parameters= ConvertObjectAsKeyValuePairList(argDic.Value,null);
                    parameterCollection.AddRange(parameters);
                }

                return parameterCollection.OrderBy(pair => pair.Key).ToList();
                #endregion
            }
            else
            {
                AddNameValuesToCollection(parameterCollection, formCollection);
            }


            return parameterCollection.OrderBy(pair => pair.Key).ToList();
        }

        /// <summary>
        /// Converts the object as key value pair list.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="querystrings">The querystrings.</param>
        /// <returns></returns>
        private static List<KeyValuePair<string, string>> ConvertObjectAsKeyValuePairList(object obj, IList<KeyValuePair<string, string>> querystrings)
        {
            if (obj != null)
            {
                var parameterCollection = new List<KeyValuePair<string, string>>();
                var properties = from p in obj.GetType().GetProperties()
                                 where
                                     p.GetValue(obj, null) != null
                                     && p.CustomAttributes.All(
                                         attc => attc.AttributeType != typeof(KeyAttribute))
                                 orderby p.Name
                                 select
                                    new KeyValuePair<string, string>(p.Name, HttpUtility.UrlEncode(p.GetValue(obj, null).ToString()));

                parameterCollection.AddRange(properties);

                if (querystrings != null)
                    parameterCollection.AddRange(querystrings);

                return parameterCollection;

            }
            return null; 
        }

        /// <summary>
        /// The build parameter message.
        /// </summary>
        /// <param name="actionContext">
        /// The action context.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string BuildParameterMessage(HttpActionContext actionContext)
        {
            List<KeyValuePair<string, string>> parameterCollection = BuildParameterCollection(actionContext);
            if (!parameterCollection.Any())
            {
                return string.Empty;
            }

            IEnumerable<string> keyValueStrings =
                parameterCollection.Select(pair => string.Format("{0}={1}", pair.Key, pair.Value));

            return string.Join("&", keyValueStrings);
        }

        /// <summary>
        /// The get http request header.
        /// </summary>
        /// <param name="headers">
        /// The headers.
        /// </param>
        /// <param name="headerName">
        /// The header name.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string GetHttpRequestHeader(HttpHeaders headers, string headerName)
        {
            if (!headers.Contains(headerName))
            {
                return string.Empty;
            }

            return headers.GetValues(headerName).SingleOrDefault();
        }

        /// <summary>
        /// The is authenticated.
        /// </summary>
        /// <param name="hashedPassword">
        /// The hashed password.
        /// </param>
        /// <param name="message">
        /// The message.
        /// </param>
        /// <param name="signature">
        /// The signature.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private static bool IsAuthenticated(string hashedPassword, string message, string signature)
        {
            if (string.IsNullOrEmpty(hashedPassword))
            {
                return false;
            }
            log.DebugFormat("Server Side Message:{0}", message);
            // Compute the hash with HMAC
            var verifiedHash = VerifyTransactionSN.ComputeHash(hashedPassword, message);
            log.DebugFormat("Server Side verifiedHash:{0}", verifiedHash);
            if (signature != null && signature.Equals(verifiedHash))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// The is date validated 
        /// </summary>
        /// <param name="timestampString">
        /// The timestamp string.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private static bool IsDateValidated(string timestampString)
        {
            DateTime timestamp;

            bool isDateTime = DateTime.TryParseExact(
                timestampString, 
                "u", 
                null, 
                DateTimeStyles.AdjustToUniversal, 
                out timestamp);

            if (!isDateTime)
            {
                return false;
            }

            DateTime now = DateTime.UtcNow;

            // TimeStamp should not be in 5 minutes behind
            if (timestamp < now.AddMinutes(-5))
            {
                return false;
            }

            if (timestamp > now.AddMinutes(5))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// The is signature validated.
        /// </summary>
        /// <param name="signature">
        /// The signature.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private static bool IsSignatureValidated(string signature)
        {
            MemoryCache memoryCache = MemoryCache.Default;
            if (memoryCache.Contains(signature))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// The get hashed password.
        /// </summary>
        /// <param name="username">
        /// The username.
        /// </param>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private string GetHashedPassword(string username)
        {
            this.Repository = new AccountRepository();
            return this.Repository.GetHashedPassword(username);
        }

        /// <summary>
        /// The is authenticated.
        /// </summary>
        /// <param name="actionContext">
        /// The action context.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private bool IsAuthenticated(HttpActionContext actionContext)
        {
            HttpRequestHeaders headers = actionContext.Request.Headers;

            string timeStampString = GetHttpRequestHeader(headers, TimestampHeaderName);
            if (!IsDateValidated(timeStampString))
            {
                return false;
            }

            string authenticationString = GetHttpRequestHeader(headers, AuthenticationHeaderName);
            if (string.IsNullOrEmpty(authenticationString))
            {
                return false;
            }

            string[] authenticationParts = authenticationString.Split(
                new[] { ":" }, 
                StringSplitOptions.RemoveEmptyEntries);

            if (authenticationParts.Length != 2)
            {
                return false;
            }

            string username = authenticationParts[0];
            string signature = authenticationParts[1];

            if (!IsSignatureValidated(signature))
            {
                return false;
            }

            AddToMemoryCache(signature);

            string hashedPassword = this.GetHashedPassword(username);
            string baseString = BuildBaseString(actionContext);
           
            return IsAuthenticated(hashedPassword, baseString, signature);
        }

        #endregion
    }
}