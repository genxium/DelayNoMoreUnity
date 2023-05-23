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

    public string getHttpHost() {
        return "http://" + getHostnameOnly() + ":5051";
    }

    public string getWsEndpoint() {
        return "ws://" + getHostnameOnly() + ":5051/Ws";
    }
}
