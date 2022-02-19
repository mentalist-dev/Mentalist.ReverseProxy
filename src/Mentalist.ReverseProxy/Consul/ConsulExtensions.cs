namespace Mentalist.ReverseProxy.Consul;

internal static class ConsulExtensions
{
    public static long GetConsulIndex(this HttpResponseMessage response, long lastConsulIndex)
    {
        long index;
        if (!response.Headers.TryGetValues("X-Consul-Index", out var headers))
        {
            index = ConsulConfiguration.BlockingIndex;
        }
        else
        {
            var header = headers.FirstOrDefault();
            if (!long.TryParse(header, out var consulIndex))
            {
                index = ConsulConfiguration.BlockingIndex;
            }
            else
            {
                // Implementations must check to see if a returned index is lower than the previous value, and if it is, should reset index to 0
                // Failure to do so may cause the client to miss future updates for an unbounded time, or to use an invalid index value that causes no blocking and increases load on the servers
                if (consulIndex < lastConsulIndex)
                {
                    index = ConsulConfiguration.ResetIndex;
                }
                else
                {
                    index = consulIndex;
                }
            }
        }

        return index;
    }
}