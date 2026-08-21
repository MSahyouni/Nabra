using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Nabrh.Bootstrapper.Services
{
    /// <summary>
    /// Installer-side anti-tamper guard. HONEST SCOPE (AGENTS.md §13.1): these are speed bumps that
    /// stop casual interference and duplicate/concurrent installs — not a barrier against a determined
    /// local operator. The durable protection is Authenticode on the bundle + server-side licensing.
    /// </summary>
    public static class InstallerProtection
    {
        private static Mutex? _instanceMutex;

        // Renamed from the inherited "ERPUI_Governance_Installer_SingleInstance_v2_1".
        // The name only establishes cross-process identity, so an unrelated ERPUI installer
        // no longer blocks a Nabrh install (and vice versa).
        private const string MutexName = "Nabrh_Installer_SingleInstance_v1";

        /// <summary>
        /// Acquires a machine-local single-instance mutex so two installer copies can't race on the
        /// same files/keys. Returns false if another instance already holds it. Hold the returned
        /// mutex for the lifetime of the process (kept in a static field).
        /// </summary>
        public static bool TryAcquireSingleInstance()
        {
            try
            {
                _instanceMutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out bool createdNew);
                return createdNew;
            }
            catch
            {
                // If the mutex cannot be created, fail open (do not block a legitimate install).
                return true;
            }
        }

        public static void ReleaseSingleInstance()
        {
            try { _instanceMutex?.ReleaseMutex(); } catch { /* ignore */ }
            _instanceMutex?.Dispose();
            _instanceMutex = null;
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsDebuggerPresent();

        /// <summary>True if a managed or native debugger is attached to the installer process.</summary>
        public static bool IsDebuggerAttached()
        {
            try { return IsDebuggerPresent() || Debugger.IsAttached; }
            catch { return Debugger.IsAttached; }
        }
    }
}

