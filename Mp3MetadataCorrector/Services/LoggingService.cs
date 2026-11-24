// Author:
// Bhushan Kamble
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Mp3MetadataCorrector.Services;

/// <summary>
/// Thread-safe file and debug logger providing persistent diagnostics and crash tracing.
/// </summary>
public static class Logger
{
    private static readonly object SyncLock = new();
    private static readonly string LogDirectory;
    private static readonly string LogFilePath;
    private static readonly long MaxLogSizeBytes = 5 * 1024 * 1024; // 5 MB

    static Logger()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            LogDirectory = Path.Combine(localAppData, "Mp3MetadataCorrector", "logs");
            Directory.CreateDirectory(LogDirectory);
            LogFilePath = Path.Combine(LogDirectory, "app.log");
        }
        catch
        {
            LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            LogFilePath = Path.Combine(LogDirectory, "app.log");
        }
    }

    /// <summary>
    /// Gets the full path to the active log file.
    /// </summary>
    public static string CurrentLogFilePath => LogFilePath;

    /// <summary>
    /// Gets the directory where application log files are stored.
    /// </summary>
    public static string CurrentLogDirectory => LogDirectory;

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Info(string message) => Log("INFO", message, null);

    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Debug(string message) => Log("DEBUG", message, null);

    /// <summary>
    /// Logs a warning message with an optional exception.
    /// </summary>
    /// <param name="message">The warning message.</param>
    /// <param name="ex">Optional associated exception.</param>
    public static void Warn(string message, Exception? ex = null) => Log("WARN", message, ex);

    /// <summary>
    /// Logs an error message with an optional exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="ex">Optional associated exception.</param>
    public static void Error(string message, Exception? ex = null) => Log("ERROR", message, ex);

    /// <summary>
    /// Logs a fatal or unhandled exception message.
    /// </summary>
    /// <param name="message">The critical error description.</param>
    /// <param name="ex">Optional associated exception.</param>
    public static void Fatal(string message, Exception? ex = null) => Log("FATAL", message, ex);

    /// <summary>
    /// Opens the folder containing the log files in Windows File Explorer.
    /// </summary>
    public static void OpenLogFolder()
    {
        try
        {
            if (Directory.Exists(LogDirectory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = LogDirectory,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Error("Failed to open log directory", ex);
        }
    }

    /// <summary>
    /// Opens the current log file with the system default text viewer.
    /// </summary>
    public static void OpenLogFile()
    {
        try
        {
            if (File.Exists(LogFilePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = LogFilePath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Error("Failed to open log file", ex);
        }
    }

    private static void Log(string level, string message, Exception? ex)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            int threadId = Environment.CurrentManagedThreadId;
            var sb = new StringBuilder();
            sb.Append($"[{timestamp}] [{level,-5}] [T{threadId:D2}] {message}");

            if (ex != null)
            {
                sb.AppendLine();
                sb.Append($"  Exception: {ex.GetType().FullName}: {ex.Message}");
                if (!string.IsNullOrEmpty(ex.StackTrace))
                {
                    sb.AppendLine();
                    sb.Append($"  StackTrace: {ex.StackTrace}");
                }
                var inner = ex.InnerException;
                while (inner != null)
                {
                    sb.AppendLine();
                    sb.Append($"  InnerException: {inner.GetType().FullName}: {inner.Message}");
                    if (!string.IsNullOrEmpty(inner.StackTrace))
                    {
                        sb.AppendLine();
                        sb.Append($"  InnerStackTrace: {inner.StackTrace}");
                    }
                    inner = inner.InnerException;
                }
            }

            string line = sb.ToString();

            // Write to debug stream
            System.Diagnostics.Debug.WriteLine(line);

            // Write to disk with rolling check
            lock (SyncLock)
            {
                RotateLogsIfNeeded();
                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging should never throw
        }
    }

    private static void RotateLogsIfNeeded()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            var fi = new FileInfo(LogFilePath);
            if (fi.Exists && fi.Length > MaxLogSizeBytes)
            {
                string oldFile = Path.Combine(LogDirectory, "app.old.log");
                if (File.Exists(oldFile))
                {
                    File.Delete(oldFile);
                }
                File.Move(LogFilePath, oldFile);
            }
        }
        catch
        {
            // Best effort rotation
        }
    }
}
