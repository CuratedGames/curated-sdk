using System;
using System.Net;

namespace Curated.Sdk;

/// <summary>Curated rejected a client call (auth, entitlement, validation, or hub error).</summary>
public sealed class CuratedException : Exception
{
    public CuratedException(HttpStatusCode statusCode, string? responseBody, string message)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public HttpStatusCode StatusCode { get; }

    public string? ResponseBody { get; }
}
