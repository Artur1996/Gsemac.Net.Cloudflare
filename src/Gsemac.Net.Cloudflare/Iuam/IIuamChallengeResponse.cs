﻿using System.Net;

namespace Gsemac.Net.Cloudflare.Iuam {

    public interface IIuamChallengeResponse {

        string UserAgent { get; }
        CookieCollection Cookies { get; }

        bool Success { get; }

    }

}