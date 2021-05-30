﻿using System;
using System.IO;
using System.Net;

namespace Gsemac.Net.Cloudflare {

    public interface IChallengeResponse {

        CookieCollection Cookies { get; }
        WebHeaderCollection Headers { get; }
        Uri ResponseUri { get; }
        HttpStatusCode StatusCode { get; }
        string UserAgent { get; }

        bool Success { get; }
        bool HasResponseStream { get; }

        Stream GetResponseStream();

    }

}