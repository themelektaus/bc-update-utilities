using System;
using System.Collections.Generic;
using System.IO;

namespace BCUpdateUtilities;

public static class Logger
{
    public struct Entry
    {
        public DateTime timestamp;
        public string timestampString;
        public string time;
        public string type;
        public string message;
    }
    static readonly object entriesLock = new();
    static readonly List<Entry> entries = new();

    public static List<Entry> GetEntries()
    {

        var entries = new List<Entry>();
        lock (entriesLock)
            entries.AddRange(Logger.entries);
        return entries;
    }

    static void AddEntry(Entry entry)
    {
        lock (entriesLock)
            entries.Add(entry);
    }

    public static event Action onUpdate;

    static readonly string file = Path.Combine(
        "logs",
        $"{DateTime.Now:yyyy-MM-dd HH-mm-ss fff}.log"
    );

    public static void Pending(string message)
    {
        Log("pending", message);
    }

    public static void Info(string message)
    {
        Log("info", message);
    }

    public static void Success(string message)
    {
        Log("success", message);
    }

    public static void Warning(string message)
    {
        Log("warning", message);
    }

    public static void Error(string message)
    {
        Log("error", message);
    }

    static void Log(string type, string message)
    {
        var timestamp = DateTime.Now;
        var entry = new Entry
        {
            timestamp = timestamp,
            timestampString = timestamp.ToString("yyyy-MM-dd HH-mm-ss fff"),
            time = timestamp.ToString("HH:mm"),
            type = type,
            message = message
        };
        AddEntry(entry);

        onUpdate?.Invoke();

        Directory.CreateDirectory("logs");

        File.AppendAllText(file, $"[{entry.timestampString}] {entry.message}{Environment.NewLine}");
    }
}
