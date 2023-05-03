
using shared;
using System;
using UnityEngine;

public class LoggerBridgeImpl : ILoggerBridge {
    public void LogError(string str, Exception ex) {
        Debug.LogError(String.Format("{0}: {1}", str, ex));
    }

    public void LogInfo(string str) {
        Debug.Log(str);
    }

    public void LogWarn(string str) {
        Debug.LogWarning(str);
    }
}
