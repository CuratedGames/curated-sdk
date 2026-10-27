using System;

namespace Curated.Sdk;

/// <summary>Why a launch from the Curated launcher could not start a Curated session.</summary>
public enum CuratedLaunchError
{
    /// <summary>Launch variables are missing. Contract code <c>not_launched_by_curated</c>.</summary>
    NotLaunchedByCurated,

    /// <summary>Contract version is below the minimum this game still supports, or invalid. Contract code <c>unsupported_contract_version</c>.</summary>
    UnsupportedContractVersion,

    /// <summary>The launch was for a different app. Contract code <c>app_mismatch</c>.</summary>
    AppMismatch,

    /// <summary>The session expired or the player signed out. Contract code <c>session_invalid</c>.</summary>
    SessionInvalid,

    /// <summary>The player does not own this app. Contract code <c>not_entitled</c>.</summary>
    NotEntitled,

    /// <summary>No response from Curated. Contract code <c>network_error</c>.</summary>
    NetworkError,

    /// <summary>Curated returned an unexpected error. Contract code <c>hub_error</c>.</summary>
    HubError,
}

/// <summary>Starting a Curated session from the launcher failed. See <see cref="Error"/>.</summary>
public sealed class CuratedLaunchException : Exception
{
    public CuratedLaunchException(CuratedLaunchError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public CuratedLaunchError Error { get; }

    /// <summary>Launch contract error code, identical across every Curated engine binding.</summary>
    public string Code => ToCode(Error);

    public static string ToCode(CuratedLaunchError error) => error switch
    {
        CuratedLaunchError.NotLaunchedByCurated => "not_launched_by_curated",
        CuratedLaunchError.UnsupportedContractVersion => "unsupported_contract_version",
        CuratedLaunchError.AppMismatch => "app_mismatch",
        CuratedLaunchError.SessionInvalid => "session_invalid",
        CuratedLaunchError.NotEntitled => "not_entitled",
        CuratedLaunchError.NetworkError => "network_error",
        _ => "hub_error",
    };
}
