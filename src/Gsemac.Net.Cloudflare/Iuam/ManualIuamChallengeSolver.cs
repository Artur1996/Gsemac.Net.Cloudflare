﻿using Gsemac.Net.WebBrowsers;
using Gsemac.Net.WebBrowsers.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace Gsemac.Net.Cloudflare.Iuam {

    public delegate bool ConfirmManualWebBrowserIuamChallengeSolverDelegate();

    public class ManualIuamChallengeSolver :
        IuamChallengeSolverBase {

        // Public members

        public ManualIuamChallengeSolver(IWebBrowserInfo webBrowserInfo) :
           this(webBrowserInfo, IuamChallengeSolverOptions.Default) {
        }
        public ManualIuamChallengeSolver(IWebBrowserInfo webBrowserInfo, IIuamChallengeSolverOptions options) :
            this(webBrowserInfo, null, options, () => true) {
        }
        public ManualIuamChallengeSolver(IWebBrowserInfo webBrowserInfo, IHttpWebRequestFactory webRequestFactory, IIuamChallengeSolverOptions options, ConfirmManualWebBrowserIuamChallengeSolverDelegate allowManualSolutionDelegate) :
            base("Manual IUAM Challenge Solver") {

            this.webBrowserInfo = webBrowserInfo;
            this.options = options;
            this.webRequestFactory = webRequestFactory;
            this.allowManualSolutionDelegate = allowManualSolutionDelegate;

        }

        public override IIuamChallengeResponse GetResponse(Uri uri) {

            // Attempt to solve the challenge silently (without directly opening the user's web browser) if possible.

            IIuamChallengeResponse response = GetChallengeResponseSilent(uri);

            if (!response.Success && allowManualSolutionDelegate()) {

                // We couldn't solve the challenge silently, allow the user to solve it manually.

                WebHeaderCollection requestHeaders = WebBrowserUtilities.GetWebBrowserRequestHeaders(webBrowserInfo, options.Timeout,
                    $"Redirecting to {uri.AbsoluteUri}...<script>window.location.href=\"{uri.AbsoluteUri}\";</script>");

                string userAgent = requestHeaders[HttpRequestHeader.UserAgent];

                // Wait for clearance cookies to become available.

                DateTimeOffset startedWaiting = DateTimeOffset.Now;

                while (DateTimeOffset.Now - startedWaiting < options.Timeout) {

                    CookieCollection cfCookies = GetClearanceCookiesFromWebBrowser(uri);

                    if (cfCookies.Count > 0) {

                        return new IuamChallengeResponse(uri, string.Empty) {
                            UserAgent = userAgent,
                            Cookies = cfCookies,
                        };

                    }

                    Thread.Sleep(TimeSpan.FromSeconds(5));

                }

            }

            return response;

        }

        // Private members

        private readonly IWebBrowserInfo webBrowserInfo;
        private readonly IIuamChallengeSolverOptions options;
        private readonly IHttpWebRequestFactory webRequestFactory;
        private readonly ConfirmManualWebBrowserIuamChallengeSolverDelegate allowManualSolutionDelegate;

        private CookieCollection GetClearanceCookiesFromWebBrowser(Uri uri) {

            ICookiesReaderFactory cookiesReaderFactory = new CookiesReaderFactory();
            ICookiesReader cookieReader = cookiesReaderFactory.Create(webBrowserInfo);
            IEnumerable<Cookie> cookies = cookieReader.GetCookies(uri);

            Cookie cfduid = cookies.Where(cookie => cookie.Name.Equals("__cfduid")).FirstOrDefault();
            Cookie cf_clearance = cookies.Where(cookie => cookie.Name.Equals("cf_clearance")).FirstOrDefault();

            if (!(cfduid is null || cf_clearance is null)) {

                CookieCollection cfCookies = new CookieCollection {
                        cfduid,
                        cf_clearance
                    };

                return cfCookies;

            }

            return new CookieCollection();

        }
        private IIuamChallengeResponse GetChallengeResponseSilent(Uri uri) {

            if (!(webRequestFactory is null)) {

                try {

                    CookieCollection clearanceCookies = GetClearanceCookiesFromWebBrowser(uri);

                    if (clearanceCookies.Count > 0) {

                        IHttpWebRequest request = webRequestFactory.Create(uri);

                        request.CookieContainer = new CookieContainer();
                        request.CookieContainer.Add(uri, clearanceCookies);

                        if (!string.IsNullOrWhiteSpace(options.UserAgent))
                            request.UserAgent = options.UserAgent;

                        WebResponse response = request.GetResponse();

                        using (Stream responseStream = response.GetResponseStream())
                        using (StreamReader reader = new StreamReader(responseStream)) {

                            if (CloudflareUtilities.GetProtectionType(reader.ReadToEnd()) == ProtectionType.None) {

                                return new IuamChallengeResponse(uri, string.Empty) {
                                    UserAgent = request.UserAgent,
                                    Cookies = clearanceCookies,
                                };

                            }

                        }

                    }

                }
                catch (WebException) { }

            }

            return IuamChallengeResponse.Failed;

        }

    }

}