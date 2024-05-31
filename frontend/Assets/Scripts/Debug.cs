using System;
using UnityEngine;

public class Debug : MonoBehaviour {
    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public static void Log(string msg) {
        UnityEngine.Debug.Log("[" + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff") + "] " + msg);
    }

    public static void LogWarning(string msg) {
        UnityEngine.Debug.LogWarning("[" + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff") + "] " + msg);
    }

    public static void LogError(string msg) {
        UnityEngine.Debug.LogError("[" + DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff") + "] " + msg);
    }

    public static void LogError(Exception ex) {
        UnityEngine.Debug.LogError(ex);
    }

    public static void LogFormat(String format, params object[] objects) {
        UnityEngine.Debug.LogFormat(format, objects);
    }

    public static void LogWarningFormat(String format, params object[] objects) {
        UnityEngine.Debug.LogWarningFormat(format, objects);
    }

    public static void LogErrorFormat(String format, params object[] objects) {
        UnityEngine.Debug.LogErrorFormat(format, objects);
    }
}
