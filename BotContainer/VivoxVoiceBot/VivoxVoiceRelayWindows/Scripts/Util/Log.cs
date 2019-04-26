using System;
using System.Diagnostics;
using NLog;
using NLog.Config;
using NLog.Targets;

public static class Log {
    private static Logger logger;

    public static void ConfigureLogger() {
        var config = new LoggingConfiguration();

        var consoleTarget = new ColoredConsoleTarget("console") {
            Layout = @"${date:format=HH\:mm\:ss} [${level}] ${message} ${exception}"
        };
        config.AddTarget(consoleTarget);
        config.AddRuleForAllLevels(consoleTarget); // all logs go to console

        LogManager.Configuration = config;

        logger = LogManager.GetLogger("vrelay");
    }
    
    [Conditional("DEBUG")]
    public static void Assert(bool condition, string message = "Assertion failed") {
        if (!condition) {
            logger.Error(message);
        }
    }

    public static void TODO(string message = "TODO, not implemented yet") {
        logger.Fatal(message);
    }

    public static void d(string msg, object context = null) {
        logger.Debug(msg, context);
    }

    public static void i(string msg, object context = null) {
        if (context == null) {
            logger.Info(msg);
        } else {
            logger.Info($"[{context.GetType().Name}] {msg}");
        }
    }

    public static void log(params string[] msgs) {
        logger.Debug(string.Join("  ", msgs));
    }

    public static void e(params string[] msgs) {
        logger.Error(string.Join("  ", msgs));
    }

    public static void e(Exception ex, string msg = null) {
        logger.Error(ex, msg);
    }

    public static void w(params string[] msgs) {
        logger.Warn(string.Join("  ", msgs));
    }

    public static void wtf(Exception ex, string msg = null) {
        if (msg != null) {
            ex = new Exception(msg, ex);
        }
        logger.Fatal(ex);
    }
}
