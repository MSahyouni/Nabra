using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nabrh.Bootstrapper.Models;
using Nabrh.Bootstrapper.Services;
using WixToolset.BootstrapperApplicationApi;

namespace Nabrh.Bootstrapper.ViewModels
{
    public enum WizardStep
    {
        Welcome,
        License,
        Activation,
        Prerequisites,
        Location,
        Components,
        Security,
        Summary,
        UninstallConfirm,
        Installing,
        Completed,
        Failed
    }

    /// <summary>One entry in the left navigation rail.</summary>
    public partial class WizardStepItem : ObservableObject
    {
        public WizardStep Step { get; }
        public int Number { get; }
        public string Title { get; }

        [ObservableProperty] private bool _isActive;
        [ObservableProperty] private bool _isDone;

        public WizardStepItem(WizardStep step, int number, string title)
        {
            Step = step;
            Number = number;
            Title = title;
        }
    }

    /// <summary>One line in the install step's activity list.</summary>
    public sealed class ActivityEntry
    {
        public string Timestamp { get; }
        public string Text { get; }
        public bool IsError { get; }

        public ActivityEntry(string timestamp, string text, bool isError)
        {
            Timestamp = timestamp;
            Text = text;
            IsError = isError;
        }
    }

    /// <summary>A single prerequisite/environment check row.</summary>
    public partial class PrerequisiteItem : ObservableObject
    {
        public string Title { get; }
        [ObservableProperty] private string _statusText;
        [ObservableProperty] private bool _isOk;
        [ObservableProperty] private bool _isBlocking;

        public PrerequisiteItem(string title, string statusText, bool isOk, bool isBlocking)
        {
            Title = title;
            _statusText = statusText;
            _isOk = isOk;
            _isBlocking = isBlocking;
        }
    }

    /// <summary>
    /// Host view-model for the comprehensive install wizard. Owns all shared install state, drives
    /// step navigation with per-step validation gates, and bridges the Burn engine on the Install
    /// step. The engine STATE MACHINE lives in <see cref="NabrhBootstrapperApplication"/>; this VM
    /// exposes the XAML-bound surface and marshals engine-thread callbacks onto the WPF Dispatcher.
    /// </summary>
    public partial class BootstrapperWizardViewModel : ObservableObject
    {
        private readonly IEngine _engine;
        private readonly IBootstrapperCommand _command;
        private readonly Dispatcher _dispatcher;
        private int _exitCode = InstallerExitCode.Success;

        // Both are only ever touched on the WPF dispatcher thread (BeginInstall runs on it, and
        // OnDetectComplete marshals onto it), so no synchronisation is required.
        private bool _detectCompleted;
        private bool _installPending;
        private SystemAdminApprovalResult? _adminApproval;

        public int ExitCode => _exitCode;
        public bool IsUninstallMode { get; }

        // Ordered flow used by Back/Next (excludes the terminal Installing/Completed/Failed steps).
        private static readonly WizardStep[] Flow =
        {
            WizardStep.Welcome, WizardStep.License, WizardStep.Activation, WizardStep.Prerequisites,
            WizardStep.Location, WizardStep.Components, WizardStep.Security, WizardStep.Summary
        };

        public BootstrapperWizardViewModel(IEngine engine, IBootstrapperCommand command)
        {
            _engine = engine;
            _command = command;
            _dispatcher = Dispatcher.CurrentDispatcher; // captured on the STA UI thread
            IsUninstallMode = command.Action == LaunchAction.Uninstall;

            InstallPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nabrh");

            InstallerLogService.LogInfo("Initializing Bootstrapper Wizard UI...", "Wizard");
            BuildSteps();
            if (IsUninstallMode)
            {
                GoToStep(WizardStep.UninstallConfirm);
            }
            else
            {
                RunPrerequisiteChecks();
                GoToStep(WizardStep.Welcome);
            }
        }

        // ----------------------------------------------------------------- Navigation rail
        public ObservableCollection<WizardStepItem> Steps { get; } = new();

        private void BuildSteps()
        {
            if (IsUninstallMode)
            {
                Steps.Add(new WizardStepItem(WizardStep.UninstallConfirm, 1, "تأكيد إزالة نبرة"));
                return;
            }

            Steps.Add(new WizardStepItem(WizardStep.Welcome, 1, "مرحباً"));
            Steps.Add(new WizardStepItem(WizardStep.License, 2, "اتفاقية الترخيص"));
            Steps.Add(new WizardStepItem(WizardStep.Activation, 3, "تفعيل المنتج"));
            Steps.Add(new WizardStepItem(WizardStep.Prerequisites, 4, "فحص المتطلبات"));
            Steps.Add(new WizardStepItem(WizardStep.Location, 5, "مجلد التثبيت"));
            Steps.Add(new WizardStepItem(WizardStep.Components, 6, "المكوّنات"));
            Steps.Add(new WizardStepItem(WizardStep.Security, 7, "إعدادات الحماية"));
            Steps.Add(new WizardStepItem(WizardStep.Summary, 8, "المراجعة والتأكيد"));
            // Installing/completed are terminal result views rather than user-navigable steps.
            // Keeping them out of the rail lets all eight setup decisions remain visible.
        }

        // ----------------------------------------------------------------- Current step + header
        [ObservableProperty] private WizardStep _currentStep;
        [ObservableProperty] private string _stepTitle = "";
        [ObservableProperty] private string _stepSubtitle = "";

        // Footer button visibility (each toggled per step).
        [ObservableProperty] private bool _showBack;
        [ObservableProperty] private bool _showNext;
        [ObservableProperty] private bool _showInstall;
        [ObservableProperty] private bool _showFinish;
        [ObservableProperty] private bool _showLaunch;
        [ObservableProperty] private bool _showCancel = true;
        [ObservableProperty] private bool _canGoNext = true;
        [ObservableProperty] private string _primaryActionText = "تثبيت الآن";
        [ObservableProperty] private bool _uninstallConfirmed;
        partial void OnUninstallConfirmedChanged(bool value) => RefreshNav();

        // ----------------------------------------------------------------- Step 2: License
        [ObservableProperty] private bool _licenseAccepted;
        partial void OnLicenseAcceptedChanged(bool value) => RefreshNav();

        public string LicenseText =>
            "اتفاقية ترخيص المستخدم النهائي — نبرة (Nabrh AI)\n\n" +
            "١. يمنحك هذا الترخيص الحق في تثبيت واستخدام تطبيق «نبرة» المساعد الذكي لإدارة الاجتماعات على أجهزتك.\n" +
            "٢. تم تصميم نبرة بالتركيز الكامل على الخصوصية (Privacy-First)؛ حيث تتم عمليات التقاط الصوت، وتفريغ النصوص بالذكاء الاصطناعي (Whisper)، وتوليد الملخصات محلياً على جهازك دون إرسال بيانات صوتية إلى خوادم خارجية.\n" +
            "٣. تظل جميع حقوق الملكية الفكرية لتطبيق نبرة وخوارزمياته مملوكة لشركة نبرة للذكاء الاصطناعي.\n" +
            "٤. يُحظر إجراء الهندسة العكسية أو تفكيك أو تعديل مكونات البرنامج المحمية.\n" +
            "٥. يُقدَّم البرنامج «كما هو» للمساعدة في إدارة الاجتماعات وتدوين الملاحظات وفق المعايير المعتمدة.\n\n" +
            "بمتابعتك للتثبيت فإنك تقر بقراءة هذه الاتفاقية والموافقة على كافة بنودها.";

        // ----------------------------------------------------------------- Step 3: Activation (protection)
        [ObservableProperty] private string _licenseKey = "";
        [ObservableProperty] private string _licenseStatusMessage = "";
        [ObservableProperty] private bool _isLicenseValid;
        [ObservableProperty] private bool _licenseChecked;
        [ObservableProperty] private bool _isAuthorizing;

        partial void OnLicenseKeyChanged(string value)
        {
            IsLicenseValid = false;
            LicenseChecked = false;
            LicenseStatusMessage = "";
            RefreshNav();
        }

        [RelayCommand]
        private async Task ValidateLicenseAsync()
        {
            if (IsAuthorizing) return;

            IsAuthorizing = true;
            LicenseChecked = false;
            IsLicenseValid = false;
            LicenseStatusMessage = "جارٍ طلب موافقة مشرف النظام…";

            try
            {
                string serverUrl = _engine.GetVariableString("NabrhApprovalServerUrl");
                var result = await SystemAdminApprovalService.RequestAsync(serverUrl, LicenseKey);
                _adminApproval = result;
                IsLicenseValid = result.IsCurrent;
                LicenseStatusMessage = result.Message;
                LicenseChecked = true;

                if (result.IsCurrent)
                {
                    _engine.SetVariableString("NabrhApprovalToken", result.ApprovalToken!, true);
                    InstallerLogService.LogInfo(
                        $"Installation approved by system administrators. ApprovalId={result.ApprovalId ?? "not-provided"}, ExpiresAt={result.ExpiresAtUtc:O}",
                        "Approval");
                }
                else
                {
                    InstallerLogService.LogWarning("Installation approval was denied or invalid.", "Approval");
                }
            }
            catch (Exception ex)
            {
                _adminApproval = null;
                LicenseChecked = true;
                IsLicenseValid = false;
                LicenseStatusMessage = "تعذّر التحقق من تصريح مشرف النظام. تم منع التثبيت.";
                InstallerLogService.LogError("Unexpected approval-gate failure.", ex, "Approval");
            }
            finally
            {
                IsAuthorizing = false;
                RefreshNav();
            }
        }

        // ----------------------------------------------------------------- Step 4: Prerequisites
        public ObservableCollection<PrerequisiteItem> Prerequisites { get; } = new();
        [ObservableProperty] private bool _prerequisitesPassed;

        private void RunPrerequisiteChecks()
        {
            Prerequisites.Clear();

            bool osOk = Environment.OSVersion.Version.Major >= 10;
            Prerequisites.Add(new PrerequisiteItem(
                $"نظام التشغيل {Ltr("Windows 10 / 11")}",
                osOk ? "متوافق" : $"غير متوافق — يتطلب {Ltr("Windows 10")} أو أحدث",
                osOk, isBlocking: true));

            bool arch64 = Environment.Is64BitOperatingSystem;
            Prerequisites.Add(new PrerequisiteItem(
                $"معمارية النظام {Ltr("64-bit (x64)")}",
                arch64 ? "متوافق" : "غير متوافق — يتطلب نظاماً بمعمارية 64-بت",
                arch64, isBlocking: true));

            CudaPrerequisiteResult cuda = CudaPrerequisiteService.Check();
            bool cudaRuntimeChained = IsCudaRuntimeChained();
            bool cudaCanBeInstalled = cuda.DriverAvailable && !cuda.RuntimeAvailable && cudaRuntimeChained;
            Prerequisites.Add(new PrerequisiteItem(
                $"{Ltr("NVIDIA CUDA 12")} — إلزامي لمحرك التفريغ",
                cudaCanBeInstalled
                    ? $"سيتم تنزيل وتثبيت {Ltr("CUDA Runtime 12.1")} و{Ltr("cuBLAS")} تلقائياً"
                    : cuda.Details,
                cuda.IsAvailable,
                isBlocking: !cudaCanBeInstalled));

            bool ramOk = HasEnoughRam(out long ramGb);
            Prerequisites.Add(new PrerequisiteItem(
                $"الذاكرة العشوائية {Ltr("RAM")} — يوصى بـ 8 جيجابايت",
                ramOk ? $"متوافق — {Ltr(ramGb.ToString())} جيجابايت متاحة" : $"المتاح {Ltr(ramGb.ToString())} جيجابايت فقط؛ قد لا تكفي للنماذج الكبيرة",
                ramOk, isBlocking: false));

            bool diskOk = HasEnoughDisk(out long freeMb);
            string freeGb = (freeMb / 1024d).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            Prerequisites.Add(new PrerequisiteItem(
                $"مساحة التطبيق ونماذج {Ltr("Ollama")} — 8 جيجابايت على الأقل",
                diskOk ? $"متوفّر — {Ltr(freeGb)} جيجابايت حرة" : $"غير كافية — المتاح {Ltr(freeGb)} جيجابايت",
                diskOk, isBlocking: true));

            bool vcPresent = IsVcRedistPresent();
            Prerequisites.Add(new PrerequisiteItem(
                Ltr("Microsoft Visual C++ 2015–2022 Redistributable"),
                vcPresent ? "مثبّت" : "سيتم تثبيته تلقائياً ضمن حزمة التثبيت",
                vcPresent, isBlocking: false));

            bool wordPresent = IsWordDesktopPresent(out string wordDetails);
            Prerequisites.Add(new PrerequisiteItem(
                $"{Ltr("Microsoft Word Desktop")} — للتصدير بصيغة {Ltr("DOCX")}",
                wordPresent ? wordDetails : "غير موجود — سيبقى التطبيق قابلاً للتثبيت، لكن تصدير Word لن يعمل",
                wordPresent, isBlocking: false));

            bool ollamaPresent = IsOllamaPresent(out string ollamaDetails);
            Prerequisites.Add(new PrerequisiteItem(
                $"{Ltr("Ollama")} المحلي — للتلخيص الخاص",
                ollamaPresent ? ollamaDetails : "غير موجود — يمكن تثبيته لاحقاً ثم إعادة الفحص من إعدادات نبرة",
                ollamaPresent, isBlocking: false));

            bool elevated = IsElevated();
            Prerequisites.Add(new PrerequisiteItem(
                "صلاحيات المسؤول (تثبيت لكل الأجهزة)",
                elevated ? "متوفّرة" : "سيُطلب رفع الصلاحيات عند التثبيت",
                elevated, isBlocking: false));

            PrerequisitesPassed = true;
            foreach (var p in Prerequisites)
            {
                if (p.IsBlocking && !p.IsOk) { PrerequisitesPassed = false; break; }
            }
        }

        [RelayCommand]
        private void RecheckPrerequisites()
        {
            RunPrerequisiteChecks();
            RefreshNav();
        }

        private static bool HasEnoughRam(out long totalGb)
        {
            totalGb = 0;
            try
            {
                var mem = GC.GetGCMemoryInfo();
                totalGb = (long)Math.Round((double)mem.TotalAvailableMemoryBytes / (1024 * 1024 * 1024));
                return totalGb >= 6; // 8GB systems report around 6-8GB
            }
            catch { return true; }
        }

        private static bool HasEnoughDisk(out long freeMb)
        {
            freeMb = 0;
            try
            {
                string root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)) ?? "C:\\";
                var drive = new DriveInfo(root);
                freeMb = drive.AvailableFreeSpace / (1024 * 1024);
                return freeMb >= 8192;
            }
            catch { return true; } // fail open on detection error
        }

        private static bool IsVcRedistPresent()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
                if (key != null)
                {
                    var val = key.GetValue("Installed");
                    if (val is int i && i == 1) return true;
                }
                return false;
            }
            catch { return false; }
        }

        private static bool IsWordDesktopPresent(out string details)
        {
            details = "جاهز للتصدير إلى Word";
            try
            {
                if (Type.GetTypeFromProgID("Word.Application", throwOnError: false) is null)
                    return false;

                const string appPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\WINWORD.EXE";
                foreach (var hive in new[] { Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryHive.CurrentUser })
                foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
                {
                    using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey(appPath);
                    if (key?.GetValue(null) is string path && File.Exists(path))
                    {
                        details = Environment.Is64BitProcess ? "جاهز — إصدار 64 بت" : "جاهز — إصدار 32 بت";
                        return true;
                    }
                }

                return false;
            }
            catch { return false; }
        }

        private static bool IsOllamaPresent(out string details)
        {
            details = "Ollama جاهز";
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string standardPath = Path.Combine(localAppData, "Programs", "Ollama", "ollama.exe");
                if (File.Exists(standardPath))
                {
                    details = "مثبّت للمستخدم الحالي";
                    return true;
                }

                var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (string entry in pathEntries)
                {
                    if (File.Exists(Path.Combine(entry, "ollama.exe")))
                    {
                        details = "موجود ضمن PATH";
                        return true;
                    }
                }

                return false;
            }
            catch { return false; }
        }

        private bool IsCudaRuntimeChained()
        {
            try
            {
                return string.Equals(
                    _engine.GetVariableString("NabrhCudaRuntimeChained"),
                    "1",
                    StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        // WPF inherits RTL from the window. Explicit Unicode embedding keeps product names,
        // versions and numbers together instead of letting the bidi algorithm interleave them
        // with the surrounding Arabic sentence.
        private static string Ltr(string value) => $"\u202A{value}\u202C";

        private static bool IsElevated()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        // ----------------------------------------------------------------- Step 5: Location
        [ObservableProperty] private string _installPath = "";
        [ObservableProperty] private bool _perMachine = true;

        [RelayCommand]
        private void BrowseFolder()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "اختر مجلد التثبيت",
                    Multiselect = false
                };
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
                    InstallPath = Path.Combine(dialog.FolderName, "Nabrh");
            }
            catch { /* dialog unavailable — keep default path */ }
        }

        // ----------------------------------------------------------------- Step 6: Components
        [ObservableProperty] private bool _installCore = true;          // locked (required)
        [ObservableProperty] private bool _createDesktopShortcut = true;
        [ObservableProperty] private bool _createStartMenuShortcut = true;
        [ObservableProperty] private bool _enableAutoUpdate = true;

        // ----------------------------------------------------------------- Step 7: Security / protection
        [ObservableProperty] private bool _enableEncryption = true;     // locked (Re-Encryption Cycle)
        [ObservableProperty] private bool _enableObfuscation = true;
        [ObservableProperty] private bool _enableAntiTamper = true;

        // ----------------------------------------------------------------- Steps 9/10/11: Progress + result
        [ObservableProperty] private double _progressPercentage;
        [ObservableProperty] private string _statusMessage = "جاري تحضير التثبيت...";
        [ObservableProperty] private bool _isExecuting;
        [ObservableProperty] private string _resultTitle = "";
        [ObservableProperty] private string _resultMessage = "";

        /// <summary>
        /// Secondary status line: the specific phase or package Burn is working on, shown under
        /// <see cref="StatusMessage"/>. Kept separate so per-package detail is not lost every time
        /// a progress tick overwrites the headline.
        /// </summary>
        [ObservableProperty] private string _statusDetail = "";

        /// <summary>Formatted percentage for the progress readout, e.g. "42%".</summary>
        public string ProgressText => $"{(int)Math.Round(ProgressPercentage)}%";

        partial void OnProgressPercentageChanged(double value) => OnPropertyChanged(nameof(ProgressText));

        /// <summary>
        /// Rolling record of what the engine has done, newest last. The install step previously
        /// showed a single line that each progress tick overwrote, so a failure gave the user no
        /// idea how far it got or what it was doing.
        /// </summary>
        public ObservableCollection<ActivityEntry> Activity { get; } = new();

        private const int MaxActivityEntries = 200;

        /// <summary>Appends a line to <see cref="Activity"/>, marshalling onto the UI thread.</summary>
        public void LogActivity(string text, bool isError = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            _dispatcher.InvokeAsync(() =>
            {
                Activity.Add(new ActivityEntry(DateTime.Now.ToString("HH:mm:ss"), text, isError));

                // Bounded so a long install cannot grow the list without limit.
                while (Activity.Count > MaxActivityEntries)
                {
                    Activity.RemoveAt(0);
                }
            });
        }

        // ----------------------------------------------------------------- Navigation commands
        [RelayCommand]
        private void GoNext()
        {
            if (CurrentStep == WizardStep.UninstallConfirm)
            {
                BeginInstall();
                return;
            }

            int idx = Array.IndexOf(Flow, CurrentStep);
            if (idx < 0) return;

            if (CurrentStep == WizardStep.Summary)
            {
                BeginInstall();
                return;
            }
            if (idx + 1 < Flow.Length)
                GoToStep(Flow[idx + 1]);
        }

        [RelayCommand]
        private void GoBack()
        {
            int idx = Array.IndexOf(Flow, CurrentStep);
            if (idx > 0)
                GoToStep(Flow[idx - 1]);
        }

        [RelayCommand]
        private void Cancel()
        {
            InstallerLogService.LogWarning("Installer cancelled by user via Cancel button.", "Wizard");
            _exitCode = InstallerExitCode.UserCancelled;
            _dispatcher.InvokeShutdown();
        }

        [RelayCommand]
        private void Finish()
        {
            InstallerLogService.LogInfo($"Wizard closed by user. Final ExitCode: {_exitCode}", "Wizard");
            _dispatcher.InvokeShutdown();
        }

        [RelayCommand]
        private void LaunchApp()
        {
            try
            {
                string launcher = Path.Combine(InstallPath, "nabra.exe");

                if (File.Exists(launcher))
                {
                    InstallerLogService.LogInfo($"Launching installed application: {launcher}", "Wizard");
                    Process.Start(new ProcessStartInfo(launcher) { UseShellExecute = true });
                }
                else
                {
                    InstallerLogService.LogWarning($"Application executable not found at: {launcher}", "Wizard");
                }
            }
            catch (Exception ex)
            {
                InstallerLogService.LogError("Failed to launch application after install.", ex, "Wizard");
            }
            finally
            {
                _dispatcher.InvokeShutdown();
            }
        }

        [RelayCommand]
        private void Retry()
        {
            InstallerLogService.LogInfo($"Retrying {_command.Action} from confirmation step.", "Wizard");
            GoToStep(IsUninstallMode ? WizardStep.UninstallConfirm : WizardStep.Summary);
        }

        private void BeginInstall()
        {
            IsExecuting = true;

            // Retry re-enters this method, so start each attempt from a clean slate rather than
            // appending to the previous attempt's activity.
            Activity.Clear();
            ProgressPercentage = 0;
            StatusDetail = "";

            GoToStep(WizardStep.Installing);

            // Burn rejects Plan() before the engine reaches the Detected state. Detection is started
            // by the BA at startup and normally finishes while the user is still reading step 1, but
            // if it has not, queue the request and let OnDetectComplete run it.
            if (!_detectCompleted)
            {
                _installPending = true;
                StatusMessage = IsUninstallMode ? "جارٍ فحص مكوّنات نبرة المثبّتة…" : "جارٍ فحص مكوّنات النظام…";
                InstallerLogService.LogInfo($"{_command.Action} requested before Detect completed; queued.", "Engine");
                return;
            }

            StartPlan();
        }

        private void StartPlan()
        {
            if (_command.Action == LaunchAction.Install && (_adminApproval is null || !_adminApproval.IsCurrent))
            {
                IsExecuting = false;
                _installPending = false;
                LicenseChecked = true;
                IsLicenseValid = false;
                LicenseStatusMessage = "انتهى تصريح المشرف أو لم يتم اعتماده. اطلب تصريحاً جديداً.";
                InstallerLogService.LogWarning("Install planning blocked: no current system administrator approval.", "Approval");
                GoToStep(WizardStep.Activation);
                return;
            }

            InstallerLogService.LogInfo($"Planning {_command.Action} for path: {InstallPath}", "Engine");
            if (_command.Action == LaunchAction.Install)
                _engine.SetVariableString("InstallFolder", Path.GetFullPath(InstallPath), false);
            // v7: Plan takes (LaunchAction, BundleScope). Use the action Burn actually requested —
            // hardcoding Install made an Add/Remove Programs uninstall re-install the product.
            _engine.Plan(_command.Action, BundleScope.Default);
        }

        // ----------------------------------------------------------------- Engine bridge (called by BA)
        public void OnDetectComplete(bool succeeded)
        {
            _dispatcher.InvokeAsync(() =>
            {
                _detectCompleted = succeeded;
                StatusMessage = succeeded ? "تم فحص مكوّنات النظام بنجاح." : "تعذّر فحص مكوّنات النظام.";
                InstallerLogService.LogInfo($"Engine detect completed. Succeeded: {succeeded}", "Engine");

                if (succeeded && _installPending)
                {
                    _installPending = false;
                    StartPlan();
                }
            });
        }

        public void UpdateProgress(double progress, string message)
        {
            _dispatcher.InvokeAsync(() =>
            {
                ProgressPercentage = progress;
                StatusMessage = message;
            });
        }

        /// <summary>
        /// Sets the headline status without touching the percentage, and records the change in the
        /// activity list. Used for phase transitions (elevation, caching, per-package execution).
        /// </summary>
        public void SetStatus(string message, string? detail = null, bool isError = false)
        {
            _dispatcher.InvokeAsync(() =>
            {
                StatusMessage = message;
                if (detail is not null)
                {
                    StatusDetail = detail;
                }
            });

            LogActivity(detail is null ? message : $"{message} — {detail}", isError);
        }

        public void Finish(bool succeeded, string message, int errorCode = 0)
        {
            _dispatcher.InvokeAsync(() =>
            {
                IsExecuting = false;
                _exitCode = succeeded ? InstallerExitCode.Success : (errorCode != 0 ? errorCode : InstallerExitCode.FatalError);
                if (succeeded) ProgressPercentage = 100;
                StatusMessage = message;
                ResultTitle = succeeded
                    ? (IsUninstallMode ? "اكتملت إزالة نبرة بنجاح" : "اكتمل التثبيت بنجاح")
                    : (IsUninstallMode ? "تعذّرت إزالة نبرة" : "فشل التثبيت");
                ResultMessage = succeeded
                    ? (IsUninstallMode
                        ? "تمت إزالة تطبيق نبرة ومكوّناته المثبّتة من هذا الجهاز. لم تُحذف ملفات الاجتماعات الشخصية."
                        : "تم تثبيت تطبيق نبرة وتهيئة بيئة الذكاء الاصطناعي بنجاح على هذا الجهاز.")
                    : (string.IsNullOrWhiteSpace(message) ? "حدث خطأ أثناء تنفيذ العملية. يرجى مراجعة سجلّات النظام." : message);

                InstallerLogService.LogInfo($"Installation cycle completed. Succeeded: {succeeded}, ExitCode: {_exitCode}, Message: {message}", "Engine");
                GoToStep(succeeded ? WizardStep.Completed : WizardStep.Failed);
            });
        }

        // ----------------------------------------------------------------- Step transition core
        private void GoToStep(WizardStep step)
        {
            CurrentStep = step;
            InstallerLogService.LogInfo($"Step transitioned to: {step}", "Navigation");

            foreach (var item in Steps)
            {
                item.IsActive = item.Step == step;
                item.IsDone = IsBefore(item.Step, step);
            }

            (StepTitle, StepSubtitle) = HeaderFor(step);

            bool isFlowStep = Array.IndexOf(Flow, step) >= 0;
            ShowBack = !IsUninstallMode && isFlowStep && step != WizardStep.Welcome;
            ShowNext = isFlowStep && step != WizardStep.Summary;
            ShowInstall = step == WizardStep.Summary || step == WizardStep.UninstallConfirm;
            PrimaryActionText = IsUninstallMode ? "إزالة نبرة" : "تثبيت الآن";
            ShowCancel = step != WizardStep.Completed && step != WizardStep.Failed && step != WizardStep.Installing;
            ShowFinish = step == WizardStep.Completed || step == WizardStep.Failed;
            ShowLaunch = !IsUninstallMode && step == WizardStep.Completed;

            RefreshNav();
        }

        private void RefreshNav()
        {
            CanGoNext = CurrentStep switch
            {
                WizardStep.License => LicenseAccepted,
                WizardStep.Activation => IsLicenseValid,
                WizardStep.Prerequisites => PrerequisitesPassed,
                WizardStep.UninstallConfirm => UninstallConfirmed,
                _ => true
            };
        }

        private static bool IsBefore(WizardStep a, WizardStep b) => (int)a < (int)b;

        private (string, string) HeaderFor(WizardStep step) => step switch
        {
            WizardStep.Welcome => ("مرحباً بك في معالج تثبيت نبرة (Nabrh)",
                                   "المساعد الذكي لتسجيل وتفريغ الاجتماعات محلياً بأعلى درجات الخصوصية والأداء."),
            WizardStep.License => ("اتفاقية ترخيص المستخدم النهائي",
                                   "يرجى قراءة الشروط والموافقة عليها قبل المتابعة."),
            WizardStep.Activation => ("تصريح مشرفي النظام",
                                      "أدخل رمز التصريح المؤقت للتحقق منه عبر سيرفر الموافقات المحدد للمؤسسة."),
            WizardStep.Prerequisites => ("فحص متطلبات النظام والذكاء الاصطناعي",
                                         "نتأكّد من جاهزية نظام التشغيل والذاكرة ومكتبات التشغيل الأصلية قبل بدء التثبيت."),
            WizardStep.Location => ("اختيار مجلد التثبيت",
                                    "حدّد موقع تثبيت ملفات تطبيق نبرة ونطاق التثبيت."),
            WizardStep.Components => ("اختيار المكوّنات",
                                      "حدّد المكوّنات والاختصارات وخيارات التحديث التلقائي."),
            WizardStep.Security => ("إعدادات الحماية والخصوصية",
                                    "آليات التشفير المحلي لقواعد بيانات وتفريغات الاجتماعات."),
            WizardStep.Summary => ("مراجعة الإعدادات والتأكيد",
                                   "راجع اختياراتك قبل بدء عملية التثبيت."),
            WizardStep.UninstallConfirm => ("إزالة تطبيق نبرة",
                                             "راجع أثر الإزالة ثم أكّد العملية. لا تتطلب الإزالة تصريحاً من سيرفر المشرفين."),
            WizardStep.Installing => IsUninstallMode
                ? ("جارٍ إزالة نبرة", "يتم الآن إلغاء تسجيل المكوّنات وإزالة ملفات التطبيق بأمان…")
                : ("جارٍ التثبيت", "يتم الآن نسخ الملفات وتهيئة بيئة الذكاء الاصطناعي…"),
            WizardStep.Completed => IsUninstallMode
                ? ("اكتملت إزالة نبرة", "تمت إزالة التطبيق مع الاحتفاظ بملفات الاجتماعات الشخصية.")
                : ("اكتمل التثبيت بنجاح", "أصبح تطبيق نبرة جاهزاً للاستخدام الآن."),
            WizardStep.Failed => IsUninstallMode
                ? ("تعذّرت إزالة نبرة", "لم تكتمل الإزالة. يمكنك إعادة المحاولة أو مراجعة سجل العملية.")
                : ("تعذّر إكمال التثبيت", "لم تكتمل العملية. يمكنك إعادة المحاولة أو مراجعة المتطلبات."),
            _ => ("", "")
        };
    }
}
