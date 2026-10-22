using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal
{
    /// <summary>Argument and state guards that compile on netstandard2.1 and net8.0.</summary>
    internal static class Guard
    {
        public static void NotNull(object? value, string paramName)
        {
            if (value is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }

        public static void NotDisposed(bool disposed, object instance)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(instance.GetType().FullName);
            }
        }
    }

    /// <summary>BCL helpers whose modern overloads are missing on netstandard2.1.</summary>
    internal static class Compat
    {
        public static Task<string> ReadBodyAsync(HttpContent content, CancellationToken cancellationToken)
        {
#if NET5_0_OR_GREATER
            return content.ReadAsStringAsync(cancellationToken);
#else
            cancellationToken.ThrowIfCancellationRequested();
            return content.ReadAsStringAsync();
#endif
        }

        public static Task CopyBodyToAsync(HttpContent content, Stream destination, CancellationToken cancellationToken)
        {
#if NET5_0_OR_GREATER
            return content.CopyToAsync(destination, cancellationToken);
#else
            cancellationToken.ThrowIfCancellationRequested();
            return content.CopyToAsync(destination);
#endif
        }

        public static byte[] Sha256(byte[] data)
        {
#if NET5_0_OR_GREATER
            return SHA256.HashData(data);
#else
            using var sha = SHA256.Create();
            return sha.ComputeHash(data);
#endif
        }

        public static string ToLowerHex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2];
            const string digits = "0123456789abcdef";
            for (var i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = digits[bytes[i] >> 4];
                chars[(i * 2) + 1] = digits[bytes[i] & 0xF];
            }

            return new string(chars);
        }
    }
}

#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
