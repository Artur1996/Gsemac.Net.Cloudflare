﻿using Gsemac.Net.Extensions;
using System;
using System.Net;

namespace Gsemac.Net.Cloudflare.Iuam {

    internal class IuamChallengeSolverHttpWebResponse :
        HttpWebResponseBase {

        // Public members

        public IuamChallengeSolverHttpWebResponse(Uri requestUri, IIuamChallengeResponse challengeResponse) :
            base(challengeResponse.ResponseUri, challengeResponse.GetResponseStream()) {

            if (challengeResponse is null)
                throw new ArgumentNullException(nameof(challengeResponse));

            if (!challengeResponse.Success)
                throw new WebException(string.Format(Properties.ExceptionMessages.FailedToSolveCloudflareIUAMChallengeWithUri, requestUri));

            ReadChallengeResponse(challengeResponse);

        }

        // Private members

        private void ReadChallengeResponse(IIuamChallengeResponse challengeResponse) {

            Cookies.Add(challengeResponse.Cookies);

            challengeResponse.Headers.CopyTo(Headers);

            StatusCode = challengeResponse.StatusCode;

        }

    }

}