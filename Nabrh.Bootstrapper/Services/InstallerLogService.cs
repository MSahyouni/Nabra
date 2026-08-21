using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using WixToolset.BootstrapperApplicationApi;

namespace Nabrh.Bootstrapper.Services
{
    public static class InstallerLogService
    {
        private static readonly object _lock = new object();
        private static string? _logFilePath;
        private static IEngine? _engine;

        public static string LogFilePath
        {
            get
            {
                if (_logFilePath == null)
                {
                    string logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Nabrh",
                        "Logs");

                    try
                    {
                        if (!Directory.Exists(logDir))
                        {
                            Directory.CreateDirectory(logDir);
                        }
                    }
                    catch
                    {
                        logDir = Path.GetTempPath();
                    }

                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    _logFilePath = Path.Combine(logDir, $"Nabrh_Installer_{timestamp}.log");
                }
                return _logFilePath;
            }
        }

        public static void Initialize(IEngine engine, IBootstrapperCommand command)
        {
            _engine = engine;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"  Nabrh Intelligent Meeting Assistant — Installer Diagnostics Log");
                sb.AppendLine($"  Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"  Log File: {LogFilePath}");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"[SYSTEM INFO]");
                sb.AppendLine($"  OS Version: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
                sb.AppendLine($"  Process Architecture: {(Environment.Is64BitProcess ? "x64" : "x86")}");
                sb.AppendLine($"  Machine Name: {Environment.MachineName}");
                sb.AppendLine($"  User Name: {Environment.UserName}");
                sb.AppendLine($"  Is Administrator (Elevated): {IsAdministrator()}");
                sb.AppendLine($"  CLR Version: {Environment.Version}");
                sb.AppendLine($"  Processor Count: {Environment.ProcessorCount}");
                sb.AppendLine($"  System Memory (Available/Total): {GetMemoryDiagnosticString()}");
                sb.AppendLine();
                sb.AppendLine($"[WIX COMMAND INFO]");
                sb.AppendLine($"  Display Mode: {command.Display}");
                sb.AppendLine($"  Action: {command.Action}");
                sb.AppendLine($"  Command Line: {command.CommandLine}");
                sb.AppendLine("================================================================================");
                sb.AppendLine();

                WriteToFile(sb.ToString());
                _engine.Log(LogLevel.Standard, $"[Nabrh.Installer] Diagnostics session initialized. Log: {LogFilePath}");
            }
            catch (Exception ex)
            {
                _engine?.Log(LogLevel.Error, $"[Nabrh.Installer] Failed to initialize file logger: {ex.Message}");
            }
        }

        public static void LogInfo(string message, string category = "General")
        {
            Log(LogLevel.Standard, "INFO", category, message);
        }

        public static void LogWarning(string message, string category = "General")
        {
            Log(LogLevel.Standard, "WARN", category, message);
        }

        public static void LogError(string message, Exception? ex = null, string category = "General")
        {
            string fullMessage = ex != null
                ? $"{message} | Exception: {DescribeException(ex)}\n{ex.StackTrace}"
                : message;
            Log(LogLevel.Error, "ERROR", category, fullMessage);
        }

        /// <summary>
        /// Renders an exception together with its full InnerException chain.
        /// </summary>
        /// <remarks>
        /// Logging only the outer message loses the part that identifies the fault. A
        /// XamlParseException reports nothing more useful than "Provide value on
        /// 'StaticResourceHolder' threw an exception"; the inner exception is what names the
        /// missing resource key.
        /// </remarks>
        private static string DescribeException(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            for (Exception? current = ex; current is not null; current = current.InnerException)
            {
                if (sb.Length > 0)
                {
                    sb.Append(" --> ");
                }
                sb.Append(current.GetType().Name).Append(": ").Append(current.Message);
            }
            return sb.ToString();
        }

        public static void LogEngineEvent(string eventName, string details)
        {
            Log(LogLevel.Standard, "ENGINE", eventName, details);
        }

        private static void Log(LogLevel level, string levelTag, string category, string message)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = $"[{timestamp}] [{levelTag,-5}] [{category}] {message}";

            // 1. Write to local log file
            try
            {
                WriteToFile(line + Environment.NewLine);
            }
            catch
            {
                // Suppress disk write failures to prevent crash
            }

            // 2. Write to WiX Burn engine log
            try
            {
                _engine?.Log(level, $"[Nabrh.{category}] {message}");
            }
            catch
            {
                // Suppress engine log failures
            }
        }

        private static void WriteToFile(string text)
        {
            lock (_lock)
            {
                File.AppendAllText(LogFilePath, text, Encoding.UTF8);
            }
        }

        public static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static string GetMemoryDiagnosticString()
        {
            try
            {
                var gcInfo = GC.GetGCMemoryInfo();
                double totalGb = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
                return $"{totalGb:F2} GB Total Available";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
