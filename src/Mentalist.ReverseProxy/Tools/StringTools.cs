using System.Security.Cryptography;
using System.Text;

namespace Mentalist.ReverseProxy.Tools;

public static class StringTools
{
    // ReSharper disable once InconsistentNaming
    public static string ToSHA1(this string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var buffer = Encoding.UTF8.GetBytes(key);
        using var sha1 = SHA1.Create();
        return BitConverter.ToString(sha1.ComputeHash(buffer)).Replace("-", "");
    }
}