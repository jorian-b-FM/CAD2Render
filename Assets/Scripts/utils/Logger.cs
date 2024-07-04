using System;
using System.IO;
using UnityEngine;

public static class Logger
{
    private static readonly string _logFilePath = "out.log";
    private static readonly object _fileLock = new object();

    static Logger()
    {
        _logFilePath = $"out_{DateTime.Now:yyMMdd}.log";
    }

    public static void LogInfo(string message)
    {
        Log(message, "INFO", Debug.Log);
    }

    public static void LogWarning(string message)
    {
        Log(message, "WARN", Debug.LogWarning);
    }

    public static void LogError(string message)
    {
        Log(message, "ERROR", Debug.LogError);
    }

    private static void Log(string message, string level, Action<string> unityLogMethod)
    {
        string formattedMessage = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";

        // Log to Unity console
        unityLogMethod(formattedMessage);

        // Log to file
        lock (_fileLock)
        {
            File.AppendAllText(_logFilePath, formattedMessage + Environment.NewLine);
        }
    }

    private static void ClearLogFile()
    {
        lock (_fileLock)
        {
            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath);
            }
        }
    }
}