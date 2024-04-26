using System;
using System.Collections.Generic;
using System.IO;

namespace BCUpdateUtilities;

public static class Logger
{
    static readonly object @lock = new();

    static readonly List<Entry> _entries = [];

    public struct Entry
    {
        public DateTime timestamp;
        public string timestampString;
        public string time;
        public string type;
        public string message;
    }
    
    public static List<Entry> GetEntries()
    {
        lock (@lock)
        {
            return [.. _entries];
        }
    }

    public static event Action<string, string> OnUpdate;

    static readonly string file = Path.Combine(
        "logs",
        $"{DateTime.Now:yyyy-MM-dd HH-mm-ss fff}.log"
    );

    public static void Pending(string message) => Log("pending", message);
    public static void Info(string message) => Log("info", message);
    public static void Success(string message) => Log("success", message);
    public static void Warning(string message) => Log("warning", message);
    public static void Error(string message) => Log("error", message);
    public static void Exception(Exception ex) => Log("exception", ex.ToString());
    public static void Script(string message) => Log("script", message);
    public static void Result(string message) => Log("result", message);

    static void Log(string type, string message)
    {
        OnUpdate?.Invoke(type, message);

        var timestamp = DateTime.Now;
        var entry = new Entry
        {
            timestamp = timestamp,
            timestampString = timestamp.ToString("yyyy-MM-dd HH-mm-ss fff"),
            time = timestamp.ToString("HH:mm"),
            type = type,
            message = message
        };

        lock (@lock)
        {
            _entries.Add(entry);

            Directory.CreateDirectory("logs");

            File.AppendAllText(
                file,
                "[" + entry.timestampString + "]"
                    + " [" + type + "] "
                    + entry.message
                    + Environment.NewLine
            );
        }
    }
}
