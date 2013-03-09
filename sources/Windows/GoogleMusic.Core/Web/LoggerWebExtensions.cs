﻿// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions.GoogleMusic.Web
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;

    using OutcoldSolutions.Diagnostics;

    using Windows.Storage;

#if !DEBUG
    using Windows.Security.Cryptography;
    using Windows.Security.Cryptography.Core;
    using Windows.Storage.Streams;
#endif

    public static class LoggerWebExtensions
    {
        private const string WebResponseLogs = "WebResponses_Logs";

        public static void LogRequest(
            this ILogger @this,
            HttpMethod method,
            string requestUrl, 
            IEnumerable<Cookie> cookieCollection = null,
            IDictionary<string, string> formData = null)
        {
            if (@this == null)
            {
                throw new ArgumentNullException("this");
            }

            if (@this.IsDebugEnabled)
            {
                var log = new StringBuilder();

                log.AppendLine();
                log.AppendFormat("{0} REQUEST: {1}.", method, requestUrl);
                log.AppendLine();

                if (formData != null)
                {
                    log.AppendLine("    FORMDATA: ");

                    foreach (var argument in formData)
                    {
                        log.AppendFormat("        {0}={1}", argument.Key, argument.Value);
                        log.AppendLine();
                    }
                }

                LogCookies(log, cookieCollection);

                log.AppendLine();

                @this.Debug(log.ToString());
            }
        }

        public static void LogCookies(this ILogger @this, IEnumerable<Cookie> cookieCollection)
        {
            if (@this.IsDebugEnabled)
            {
                if (cookieCollection != null)
                {
                    var log = new StringBuilder();
                    LogCookies(log, cookieCollection);

                    @this.Debug(log.ToString());
                }
            }
        }

        public static async Task LogResponseAsync(
            this ILogger @this, 
            string requestUrl, 
            HttpResponseMessage responseMessage)
        {
            if (@this == null)
            {
                throw new ArgumentNullException("this");
            }

            if (responseMessage == null)
            {
                throw new ArgumentNullException("responseMessage");
            }

            if (@this.IsDebugEnabled)
            {
                var log = new StringBuilder();

                log.AppendLine();
                log.AppendFormat("RESPONSE FROM '{0}' COMPLETED, STATUS CODE: {1}.", responseMessage.RequestMessage.RequestUri, responseMessage.StatusCode);
                log.AppendLine();
                if (!string.IsNullOrEmpty(responseMessage.ReasonPhrase))
                {
                    log.AppendFormat("  REASON PHRASE '{0}'", responseMessage.ReasonPhrase);
                    log.AppendLine();
                }
                
                log.AppendFormat("  ORIGINAL URI: {0}.", requestUrl);
                log.AppendLine();

                log.AppendLine("    RESPONSE HEADERS: ");
                LogHeaders(log, responseMessage.Headers);

                await LogContentAsync(log, responseMessage.Content);

                log.AppendLine();

                @this.Debug(log.ToString());
            }
        }

        private static async Task LogContentAsync(StringBuilder log, HttpContent httpContent)
        {
            if (httpContent != null)
            {
                log.AppendLine("    CONTENT HEADERS: ");

                LogHeaders(log, httpContent.Headers);

                if (httpContent.IsPlainText()
                    || httpContent.IsHtmlText()
                    || httpContent.IsJson()
                    || httpContent.IsFormUrlEncoded())
                {
                    var content = await httpContent.ReadAsStringAsync();

                    StorageFolder folder = null;

                    try
                    {
                        folder = (await ApplicationData.Current.LocalFolder.GetFoldersAsync())
                            .FirstOrDefault(x => string.Equals(x.Name, WebResponseLogs, StringComparison.OrdinalIgnoreCase));
                    }
                    catch (InvalidOperationException)
                    {
                        // Unit tests does not have package identity. We just ignore them.
                    }

                    if (folder != null)
                    {
                        var fileName = string.Format("{0}.log", Guid.NewGuid());
                        var file = await folder.CreateFileAsync(fileName);
                        await FileIO.WriteTextAsync(file, content);
                        log.AppendFormat("    CONTENT FILE: {0}", file.Path);
                    }
                    else
                    {
                        log.AppendFormat(
                            "    CONTENT:{0}{1}",
                            Environment.NewLine,
                            content.Substring(0, Math.Min(4096, content.Length)));
                        log.AppendLine();
                        log.AppendFormat("    ENDCONTENT.");
                        log.AppendLine();
                    }
                }
            }
            else
            {
                log.AppendLine("    CONTENT IS NULL.");
            }
        }

        private static void LogHeaders(StringBuilder log, HttpHeaders headers)
        {
            if (headers != null)
            {
                foreach (var httpResponseHeader in headers)
                {
                    log.AppendFormat("        {0}={1}", httpResponseHeader.Key, string.Join("&&&", httpResponseHeader.Value));
                    log.AppendLine();
                }
            }
        }

        private static void LogCookies(StringBuilder log, IEnumerable<Cookie> cookieCollection)
        {
            if (cookieCollection != null)
            {
                var cookies = cookieCollection.ToList();
                log.AppendFormat("    COOKIES({0}):", cookies.Count);
                log.AppendLine();

                foreach (Cookie cookieLog in cookies)
                {
#if DEBUG
                    log.AppendFormat("        {0}={1}, Expires={2}", cookieLog.Name, cookieLog.Value, cookieLog.Expires);
                    log.AppendLine();
#else

                    HashAlgorithmProvider hashProvider = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
                    IBuffer hash =
                        hashProvider.HashData(
                            CryptographicBuffer.ConvertStringToBinary(cookieLog.Value, BinaryStringEncoding.Utf8));
                    string hashValue = CryptographicBuffer.EncodeToBase64String(hash);

                    log.AppendFormat("        {0}={{MD5_VALUE_HASH}}{1}, Expires={2}", cookieLog.Name, hashValue, cookieLog.Expires);
                    log.AppendLine();
#endif
                }
            }
        }
    }
}