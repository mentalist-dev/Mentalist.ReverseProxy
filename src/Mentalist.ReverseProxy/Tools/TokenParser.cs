using System.Collections.ObjectModel;
using System.Text;
using Newtonsoft.Json;

namespace Mentalist.ReverseProxy.Tools;

public static class TokenParser
{
    private static readonly ReadOnlyDictionary<string, object> Empty = new(new Dictionary<string, object>());

    public static IDictionary<string, object>? Parse(string authorizationHeaderValue)
    {
        if (string.IsNullOrEmpty(authorizationHeaderValue))
            return Empty;

        var tokenNodes = authorizationHeaderValue.Split('.');
        if (tokenNodes.Length != 3)
            return Empty;

        string? tokenBody;
        var bodyNode = tokenNodes[1];
        try
        {
            tokenBody = DecodeFromBase64(bodyNode);
            if (tokenBody == null)
                return Empty;
        }
        catch
        {
            return Empty;
        }

        try
        {
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(tokenBody);
        }
        catch
        {
            return Empty;
        }
    }

    private static string? DecodeFromBase64(string str)
    {
        str = str.Replace('-', '+');
        str = str.Replace('_', '/');

        var length = str.Length % 4;
        if (length == 1)
            return null;

        if (length == 2)
            str += "==";
        else if (length == 3)
            str += "=";

        return Encoding.UTF8.GetString(Convert.FromBase64String(str));
    }
}