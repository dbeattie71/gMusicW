﻿// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions.GoogleMusic.Web
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;

    using Newtonsoft.Json;

    using OutcoldSolutions.Diagnostics;
    using OutcoldSolutions.GoogleMusic.Services;
    using OutcoldSolutions.GoogleMusic.Web.Models;
    using OutcoldSolutions.Web;

    public class GoogleMusicWebService : WebServiceBase, IGoogleMusicWebService
    {
        private const string OriginUrl = "https://play.google.com/music/";
        private const string PlayMusicUrl = "https://play.google.com/music/listen?u=0&hl=en";
        private const string RefreshXtPath = "refreshxt";

        private const string SetCookieHeader = "Set-Cookie";
        private const string CookieHeader = "Cookie";

        private readonly IDependencyResolverContainer container;

        private readonly IGoogleMusicSessionService sessionService;
        private readonly ILogger logger;
        private readonly HttpClient httpClient;


        public GoogleMusicWebService(
            IDependencyResolverContainer container,
            ILogManager logManager,
            IGoogleMusicSessionService sessionService)
        {
            var httpClientHandler = new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false
            };

            this.httpClient = new HttpClient(httpClientHandler)
            {
                BaseAddress = new Uri(OriginUrl),
                Timeout = TimeSpan.FromSeconds(90),
                DefaultRequestHeaders =
                    {
                        { HttpRequestHeader.UserAgent.ToString(), "Music Manager (1, 0, 54, 4672 - Windows)" }
                    }
            };

            this.container = container;
            this.sessionService = sessionService;

            this.logger = logManager.CreateLogger("GoogleMusicWebService");
        }

        protected override ILogger Logger
        {
            get
            {
                return this.logger;
            }
        }

        protected override HttpClient HttpClient
        {
            get
            {
                return this.httpClient;
            }
        }

        public string GetServiceUrl()
        {
            return PlayMusicUrl;
        }

        public async Task<HttpResponseMessage> GetAsync(
            string url,
            bool signUrl = true)
        {
            if (this.Logger.IsDebugEnabled)
            {
                this.Logger.LogRequest(HttpMethod.Get, url, this.sessionService.GetCookieContainer().GetCookies());
            }

            if (signUrl)
            {
                url = this.SignUrl(url);
            }

            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
            requestMessage.Headers.Add(CookieHeader, this.sessionService.GetCookieContainer().GetCookieHeader());
            var responseMessage = await this.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

            IEnumerable<string> responseCookies;
            if (responseMessage.Headers.TryGetValues(SetCookieHeader, out responseCookies))
            {
                this.sessionService.GetCookieContainer().SetCookies(responseCookies);
            }

            await this.VerifyAuthorization(responseMessage);

            return responseMessage;
        }

        public async Task<HttpResponseMessage> PostAsync(
            string url,
            IDictionary<string, string> formData = null,
            bool signUrl = true)
        {
            if (this.Logger.IsDebugEnabled)
            {
                this.Logger.LogRequest(HttpMethod.Post, url, this.sessionService.GetCookieContainer().GetCookies(), formData);
            }

            if (signUrl)
            {
                url = this.SignUrl(url);
            }

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);

            if (formData != null)
            {
                requestMessage.Content = new FormUrlEncodedContent(formData);
            }

            requestMessage.Headers.Add(CookieHeader, this.sessionService.GetCookieContainer().GetCookieHeader());
            var responseMessage = await this.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead);

            IEnumerable<string> responseCookies;
            if (responseMessage.Headers.TryGetValues(SetCookieHeader, out responseCookies))
            {
                this.sessionService.GetCookieContainer().SetCookies(responseCookies);
            }

            await this.VerifyAuthorization(responseMessage);

            return responseMessage;
        }

        public async Task<TResult> GetAsync<TResult>(string url, bool signUrl = true) where TResult : CommonResponse
        {
            HttpResponseMessage responseMessage = null;

            try
            {
                responseMessage = await this.GetAsync(url, signUrl);

                // This means that google asked us to relogin. Let's try again this request.
                if (responseMessage.StatusCode == HttpStatusCode.Found
                    || responseMessage.StatusCode == HttpStatusCode.Forbidden)
                {
                    responseMessage = await this.GetAsync(url, signUrl);
                }

                responseMessage.EnsureSuccessStatusCode();
                TResult result = await responseMessage.Content.ReadAsJsonObject<TResult>();

                if (result.ReloadXsrf.HasValue && result.ReloadXsrf.Value)
                {
                    await this.RefreshXtAsync();

                    responseMessage = await this.GetAsync(url, signUrl);

                    responseMessage.EnsureSuccessStatusCode();

                    result = await responseMessage.Content.ReadAsJsonObject<TResult>();
                }

                return result;
            }
            catch (HttpRequestException exception)
            {
                HttpStatusCode statusCode = 0;
                if (responseMessage != null)
                {
                    statusCode = responseMessage.StatusCode;
                }

                throw new WebRequestException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Exception while we tried to get resposne for url (GET) '{0}'. {1}",
                        url,
                        exception.Message),
                    exception,
                    statusCode);
            }
        }

        public async Task<TResult> PostAsync<TResult>(
            string url,
            IDictionary<string, string> formData = null,
            IDictionary<string, string> jsonProperties = null,
            bool forceJsonBody = true,
            bool signUrl = true) where TResult : CommonResponse
        {
            HttpResponseMessage responseMessage = null;

            try
            {
                if (forceJsonBody && (formData == null || !formData.ContainsKey("json")))
                {
                    var jsonBody = new StringBuilder();

                    jsonBody.Append("{");
                    if (jsonProperties != null)
                    {
                        foreach (var jsonProperty in jsonProperties)
                        {
                            jsonBody.AppendFormat("\"{0}\":{1},", jsonProperty.Key, jsonProperty.Value);
                        }
                    }

                    jsonBody.AppendFormat("\"sessionId\":{0}", JsonConvert.ToString(this.sessionService.GetSession().SessionId));
                    jsonBody.Append("}");

                    if (formData == null)
                    {
                        formData = new Dictionary<string, string>();
                    }

                    formData.Add("json", jsonBody.ToString());
                }

                responseMessage = await this.PostAsync(url, formData, signUrl);

                // This means that google asked us to relogin. Let's try again this request.
                if (responseMessage.StatusCode == HttpStatusCode.Found
                    || responseMessage.StatusCode == HttpStatusCode.Forbidden)
                {
                    responseMessage = await this.PostAsync(url, formData, signUrl);
                }

                responseMessage.EnsureSuccessStatusCode();
                TResult result = await responseMessage.Content.ReadAsJsonObject<TResult>();

                if (result.ReloadXsrf.HasValue && result.ReloadXsrf.Value)
                {
                    await this.RefreshXtAsync();

                    responseMessage = await this.PostAsync(url, formData, signUrl);

                    responseMessage.EnsureSuccessStatusCode();

                    result = await responseMessage.Content.ReadAsJsonObject<TResult>();
                }

                return result;
            }
            catch (HttpRequestException exception)
            {
                HttpStatusCode statusCode = 0;
                if (responseMessage != null)
                {
                    statusCode = responseMessage.StatusCode;
                }

                StringBuilder errorMessage = new StringBuilder();
                errorMessage.AppendFormat(
                    CultureInfo.CurrentCulture,
                    "Exception while we tried to get resposne for url (POST) '{0}'. {1}",
                    url,
                    exception.Message);

                if (responseMessage != null && responseMessage.StatusCode == HttpStatusCode.Found)
                {
                    errorMessage.AppendFormat(
                        CultureInfo.CurrentCulture, ". 302: Moved to '{0}'", responseMessage.Headers.Location.LocalPath);
                }

                throw new WebRequestException(errorMessage.ToString(), exception, statusCode);
            }
        }

        public async Task RefreshXtAsync()
        {
            this.Logger.Debug("PostAsync :: Reload Xsrf requested. Reloading.");
            await this.PostAsync(RefreshXtPath);
        }

        private string SignUrl(string url)
        {
            if (this.sessionService.GetCookieContainer() != null)
            {
                var cookie = this.sessionService.GetCookieContainer().FindCookie("xt");

                if (cookie != null)
                {
                    if (url.IndexOf("?", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        url += "?";
                    }
                    else
                    {
                        url += "&";
                    }

                    url += string.Format(CultureInfo.InvariantCulture, "u=0&xt={0}", cookie.Value);

                    this.Logger.Debug("Found XT cookie, transforming url to '{0}'.", url);
                }
                else
                {
                    this.Logger.Debug("Could not find XT cookie.");
                }
            }

            return url;
        }

        private async Task VerifyAuthorization(HttpResponseMessage responseMessage)
        {
            if (!responseMessage.IsSuccessStatusCode)
            {
                bool clearSession = false;
                bool relogin = false;

                if (responseMessage.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await BugSense.BugSenseHandler.Instance.LogEventAsync("GoogleMusicWebService:Unauthorized");
                    this.Logger.Warning("Got the Unauthorized http status code. Going to clear session.");

                    clearSession = true;
                }
                else if (responseMessage.StatusCode == HttpStatusCode.Found)
                {
                    await BugSense.BugSenseHandler.Instance.LogEventAsync("GoogleMusicWebService:Found:re-authentification");
                    if (string.Equals(responseMessage.Headers.Location.LocalPath, "/accounts/ServiceLogin", StringComparison.OrdinalIgnoreCase))
                    {
                        relogin = true;
                    }
                }
                else if (responseMessage.StatusCode == HttpStatusCode.Forbidden)
                {
                    await BugSense.BugSenseHandler.Instance.LogEventAsync("GoogleMusicWebService:Forbidden:re-authentification");
                    relogin = true;
                }

                if (relogin)
                {
                    this.Logger.Warning("We been asked to re-authentification");

                    var googleAccountService = this.container.Resolve<IGoogleAccountService>();
                    var userInfo = googleAccountService.GetUserInfo(retrievePassword: true);
                    if (userInfo != null)
                    {
                        var googleAccountWebService = this.container.Resolve<IGoogleAccountWebService>();
                        var result = await googleAccountWebService.AuthenticateAsync(new Uri(this.GetServiceUrl()), userInfo.Email, userInfo.Password);
                        if (result.Success)
                        {
                            await BugSense.BugSenseHandler.Instance.LogEventAsync("GoogleMusicWebService:Found:re-authentification:success");
                            this.sessionService.InitializeCookieContainer(result.CookieCollection.Cast<Cookie>(), result.Auth);
                            await this.sessionService.SaveCurrentSessionAsync();
                            return;
                        }

                        await BugSense.BugSenseHandler.Instance.LogEventAsync("GoogleMusicWebService:Found:re-authentification:failed");
                    }

                    clearSession = true;
                }

                if (clearSession)
                {
                    await this.sessionService.ClearSession();
                    responseMessage.EnsureSuccessStatusCode();
                }
            }
        }
    }
}