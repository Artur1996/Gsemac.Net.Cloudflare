﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Gsemac.Net.Cloudflare.FlareSolverr {

    public class FlareSolverrChallengeSolver :
        ChallengeSolverBase {

        // Public members

        public FlareSolverrChallengeSolver(IFlareSolverrService flareSolverrService) :
            this(flareSolverrService, ChallengeSolverOptions.Default) {
        }
        public FlareSolverrChallengeSolver(IFlareSolverrService flareSolverrService, IChallengeSolverOptions options) :
            base("FlareSolverr IUAM Challenge Solver") {

            if (flareSolverrService is null)
                throw new ArgumentNullException(nameof(flareSolverrService));

            if (options is null)
                throw new ArgumentNullException(nameof(options));

            this.flareSolverrService = flareSolverrService;
            this.options = options;

        }

        public override IChallengeResponse GetResponse(Uri uri) {

            // Assume that the FlareSolverr proxy server is already running (on port 8191).

            FlareSolverrCommand getCommand = new FlareSolverrCommand("request.get") {
                Url = uri,
                Download = true,
                UserAgent = options.UserAgent,
                MaxTimeout = options.Timeout,
            };

            IFlareSolverrResponse response;

            try {

                response = flareSolverrService.ExecuteCommand(getCommand);

            }
            catch (WebException ex) {

                if (ex.Status == WebExceptionStatus.ConnectFailure)
                    throw new FlareSolverrException(string.Format(Properties.ExceptionMessages.FailedToConnectToFlareSolverrWithPort, FlareSolverrUtilities.DefaultPort), ex);

                throw new FlareSolverrException(Properties.ExceptionMessages.FailedToExecuteFlareSolverrCommand, ex);

            }

            OnLog.Info($"Got response with status: {response.Status}");

            // I used to check that the response code was 200 instead of 503, but sometimes the response code will be 503 even after a successful bypass.
            // Therefore, it is more reliable to check the status code returned by FlareSolverr rather than the webpage.

            if (response.Status?.Equals("ok", StringComparison.OrdinalIgnoreCase) ?? false) {

                // We successfully received a solution.
                // "Success" is set manually because we may not meet success conditions (sometimes we get a 503 on a successful response or don't get any cookies).

                return new ChallengeResponse(uri, () => StreamFromBase64(response.Solution.Response)) {
                    UserAgent = response.Solution.UserAgent,
                    Cookies = response.Solution.Cookies,
                    ResponseUri = response.Solution.Url,
                    Headers = DictionaryToWebHeaderCollection(response.Solution.Headers),
                    StatusCode = (HttpStatusCode)response.Solution.Status,
                    Success = true,
                };

            }
            else
                return ChallengeResponse.Failed;

        }

        // Private members

        private readonly IFlareSolverrService flareSolverrService;
        private readonly IChallengeSolverOptions options;

        private static Stream StreamFromBase64(string base64String) {

            return new MemoryStream(Convert.FromBase64String(base64String));

        }
        private static WebHeaderCollection DictionaryToWebHeaderCollection(IDictionary<string, string> headers) {

            WebHeaderCollection webHeaderCollection = new WebHeaderCollection();

            foreach (var header in headers) {

                switch (header.Key.ToLowerInvariant()) {

                    case "set-cookie":

                        // FlareSolverr combines multiple set-cookie headers into one newline-delimited header.

                        foreach (string setCookieValue in header.Value.Split('\n'))
                            webHeaderCollection.Add(HttpResponseHeader.SetCookie, setCookieValue);

                        break;

                    default:
                        webHeaderCollection.Add(header.Key, header.Value);
                        break;

                }

            }

            return webHeaderCollection;

        }

    }

}