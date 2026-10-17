using System;
using System.Collections.Generic;

namespace NextXI.Core
{
    public enum LogLevel { Debug, Info, Warning, Error }

    public interface ILogSink
    {
        void Write(LogLevel level, string category, string message);
    }

    /// <summary>
    /// Central logger. The core never references a specific engine; the Unity layer
    /// installs a sink that forwards to Debug.Log, tests install a memory sink.
    /// </summary>
    public static class Log
    {
        private static readonly List<ILogSink> Sinks = new List<ILogSink>();
        public static LogLevel MinLevel = LogLevel.Info;

        public static void AddSink(ILogSink sink) { lock (Sinks) if (!Sinks.Contains(sink)) Sinks.Add(sink); }
        public static void RemoveSink(ILogSink sink) { lock (Sinks) Sinks.Remove(sink); }

        public static void Debug(string cat, string msg) => Write(LogLevel.Debug, cat, msg);
        public static void Info(string cat, string msg) => Write(LogLevel.Info, cat, msg);
        public static void Warn(string cat, string msg) => Write(LogLevel.Warning, cat, msg);
        public static void Error(string cat, string msg) => Write(LogLevel.Error, cat, msg);
        public static void Exception(string cat, Exception e) => Write(LogLevel.Error, cat, e.ToString());

        private static void Write(LogLevel level, string cat, string msg)
        {
            if (level < MinLevel) return;
            ILogSink[] snapshot;
            lock (Sinks) snapshot = Sinks.ToArray();
            foreach (var s in snapshot)
            {
                try { s.Write(level, cat, msg); }
                catch { /* a faulty sink must never break the game loop */ }
            }
        }
    }

    public sealed class MemoryLogSink : ILogSink
    {
        public readonly List<string> Lines = new List<string>();
        public void Write(LogLevel level, string category, string message)
        {
            lock (Lines) Lines.Add($"[{level}] {category}: {message}");
        }
    }
}
