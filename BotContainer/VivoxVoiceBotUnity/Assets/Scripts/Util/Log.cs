using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class Log {
    private static readonly ILogger logger = Debug.unityLogger;

    [Conditional("DEBUG")]
    public static void Assert(bool condition, string message = "Assertion failed") {
        if (!condition) {
            Debug.LogError(message);
        }
    }

    [Conditional("VERBOSE")]
    public static void v(string msg) {
        Debug.Log(msg);
    }
    public static void d(string msg) {
        Debug.Log(msg);
    }

    public static void i(string msg, object context = null) {
        if (context == null) {
            Debug.Log(msg);
        } else {
            logger.Log("Info", $"[{context.GetType().Name}] {msg}");
        }
    }

    public static void log(params string[] msgs) {
        logger.Log(string.Join("  ", msgs));
    }

    public static void e(params string[] msgs) {
        Debug.LogError(string.Join("  ", msgs));
    }

    public static void e(Exception ex, string msg = null) {
        Debug.LogException(ex);
        Debug.LogError(msg);
    }

    public static void w(params string[] msgs) {
        Debug.LogWarning(string.Join("  ", msgs));
    }

    public static void wtf(Exception ex, string msg = null) {
        if (msg != null) {
            ex = new Exception(msg, ex);
        }
        Debug.LogException(ex);
    }
}
