using System.Text;

namespace Mentalist.ReverseProxy.Tools;

public static class PathTools
{
    public static string CreatePathTemplate(this HttpRequest request, int maxPathNodes = 2)
    {
        var pathBase = request.PathBase.ToString();

        var path = request.Path.ToString()
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var template = new StringBuilder(pathBase);
        var count = 0;
        foreach (var p in path)
        {
            count += 1;

            var node = p;

            if (!string.IsNullOrWhiteSpace(node))
            {
                // do not include Guid's to metrics
                if (Guid.TryParse(node, out _))
                    node = "{id}";

                template.Append($"/{node}");
            }

            if (count == maxPathNodes)
                break;
        }

        return template.ToString();
    }
}