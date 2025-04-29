#define USE_CUSTOM_ENV
public partial class Env {
    private static Env ins = null;

    public static Env Instance {
        get {
            if (null == ins) {
                ins = new Env();
            }
            return ins;
        }
    }

    public string getHostnameOnly() {
#if USE_CUSTOM_ENV
        return hostnameOnly;
#else
        return "127.0.0.1"; // Don't use "localhost", it's not parsable as the UdpTunnelIp for C# class "IPAddress"!
#endif
    }

    private static int fixedTcpPort = 8081;

    public string getHttpHost() {
        return $"http://{getHostnameOnly()}:{fixedTcpPort}";
    }

    public string getWsEndpoint() {
        return $"ws://{getHostnameOnly()}:{fixedTcpPort}/Ws";
    }
}
