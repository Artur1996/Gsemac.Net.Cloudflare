﻿using CefSharp;
using CefSharp.OffScreen;
using Gsemac.Assembly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Gsemac.CloudflareUtilities.Cef {

    public class CefChallengeSolver :
        IChallengeSolver {

        // Public members

        public CefChallengeSolver(CefChallengeSolverOptions options) {

            this.options = options;

        }

        public IChallengeResponse GetChallengeResponse(string url) {

            IChallengeResponse result = null;

            try {

                lock (lockObject) {

                    InitializeCef(options);

                    using (ChromiumWebBrowser browser = new ChromiumWebBrowser()) {

                        browser.FrameLoadEnd += FrameLoadEnd;
                        browser.BrowserInitialized += BrowserInitialized;

                        waitHandle.WaitOne(options.Timeout);

                        browser.Load(url);

                        if (waitHandle.WaitOne(options.Timeout)) {

                            // The page was loaded successfully, so extract the cookies.

                            result = new ChallengeResponse(GetUserAgent(browser), GetCookies(url, browser));


                        }

                    }

                }

            }
            catch (Exception ex) {

                throw ex;

            }
            finally {

                ShutdownCef();

            }

            return result;

        }

        // Private members

        private class CookieVisitor :
            ICookieVisitor {

            // Public members

            public IDictionary<string, string> Cookies { get; } = new Dictionary<string, string>();

            public void Dispose() {
            }
            public bool Visit(Cookie cookie, int count, int total, ref bool deleteCookie) {

                Cookies[cookie.Name] = cookie.Value;

                return true;

            }

        }

        private readonly CefChallengeSolverOptions options;
        private readonly object lockObject = new object();
        private readonly AutoResetEvent waitHandle = new AutoResetEvent(false);

        private void InitializeCef(CefChallengeSolverOptions options) {

            if (!CefSharp.Cef.IsInitialized) {

                string browserSubprocessPath = string.IsNullOrEmpty(options.BrowserSubprocessPath) ?
                    Path.GetFullPath("CefSharp.BrowserSubprocess.exe") :
                    options.BrowserSubprocessPath;

                var settings = new CefSettings {
                    BrowserSubprocessPath = browserSubprocessPath
                };

                CefSharp.Cef.Initialize(settings, performDependencyCheck: false, browserProcessHandler: null);

            }

        }
        private void ShutdownCef() {

            CefSharp.Cef.Shutdown();

        }

        private string GetUserAgent(ChromiumWebBrowser browser) {

            JavascriptResponse result = browser.EvaluateScriptAsync("navigator.userAgent").Result;

            return result.Success ? result.Result.ToString() : string.Empty;

        }
        private IDictionary<string, string> GetCookies(string url, ChromiumWebBrowser browser) {

            using (CookieVisitor visitor = new CookieVisitor()) {

                browser.GetCookieManager().VisitUrlCookies(url, true, visitor);

                return visitor.Cookies;

            }

        }

        private void BrowserInitialized(object sender, EventArgs e) {

            waitHandle.Set();

        }
        private void FrameLoadEnd(object sender, FrameLoadEndEventArgs e) {

            if (e.Frame.IsMain && sender is ChromiumWebBrowser browser) {

                browser.GetSourceAsync().ContinueWith(task => {
                   
                    if (CloudflareUtilities.GetChallengeType(task.Result) == ChallengeType.None)
                        waitHandle.Set();

                });

            }

        }

    }

}