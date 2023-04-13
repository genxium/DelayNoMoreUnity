//#define USE_CUSTOM_ENV
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

    public string getHttpHost() {
#if USE_CUSTOM_ENV
        return httpHost;
#else
        return "http://localhost:5051";
#endif
    }

    public string getWsEndpoint() {
#if USE_CUSTOM_ENV
        return wsEndpoint;
#else
        return "ws://localhost:5051/Ws";
#endif
    }
}
