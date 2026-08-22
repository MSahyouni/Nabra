using System;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using WixToolset.BootstrapperApplicationApi;
using Nabrh.Bootstrapper.Models;
using Nabrh.Bootstrapper.Services;
using Nabrh.Bootstrapper.ViewModels;
using Nabrh.Bootstrapper.Views;

namespace Nabrh.Bootstrapper
{
    // WiX v5+/v7 out-of-process (OOP) managed bootstrapper application for Nabrh.
    public static class Program
    {
        // Keep Main minimal so we connect to the parent Burn process as fast as possible.
        public static int Main(string[] args)
        {
            var app = new NabrhBootstrapperApplication();
            ManagedBootstrapperApplication.Run(app); // pass the instance (never null)
            return 0;
        }
    }

    public sealed class NabrhBootstrapperApplication : BootstrapperApplication
    {
        private readonly ManualResetEventSlim _shutdownCompleted = new(false);
        // Signals the headless (non-UI) path that Apply has finished, so Run() can return.
        private readonly ManualResetEventSlim _applyCompleted = new(false);
        private IBootstrapperCommand? _command;
        private BootstrapperWizardViewModel? _viewModel;
        private int _exitCode = InstallerExitCode.Success;

        // What Burn asked us to do. Never assume Install: Add/Remove Programs invokes the bundle
        // with LaunchAction.Uninstall, and /quiet + /passive arrive as Display.None/Passive.
        private LaunchAction _launchAction = LaunchAction.Install;
        private Display _display = Display.Full;

        // True once OnDetectComplete has fired. Burn rejects Plan() before the engine reaches the
        // Detected state, so nothing may call Plan until this flips.
        private bool _detectCompleted;

        // Parent window handle handed to IEngine.Apply. Burn rejects IntPtr.Zero outright
        // ("BA passed NULL hwndParent to Apply", 0x80070057), so this must be a real HWND:
        // the wizard window when interactive, a hidden pumped window when headless.
        private IntPtr _applyParentHwnd = IntPtr.Zero;

        protected override void OnCreate(CreateEventArgs args)
        {
            base.OnCreate(args); // sets this.engine = args.Engine
            _command = args.Command;
        }

        protected override void Run()
        {
            // 1. Initialize diagnostic file & engine logging
            InstallerLogService.Initialize(this.engine, _command!);
            InstallerLogService.LogInfo("Starting Nabrh Managed Bootstrapper Application Host.", "Engine");

            // 2. Installation protection: single instance check
            if (!InstallerProtection.TryAcquireSingleInstance())
            {
                InstallerLogService.LogError("Another Nabrh installer instance is already running. Aborting setup.", null, "Engine");
                _exitCode = InstallerExitCode.AnotherInstanceRunning;
                this.engine.Quit(_exitCode);
                return;
            }

            if (InstallerProtection.IsDebuggerAttached())
            {
                InstallerLogService.LogWarning("Notice: Installer is running under an attached debugger (anti-tamper audit).", "Security");
            }

            // 3. Honour what Burn actually asked for.
            _launchAction = _command!.Action;
            _display = _command!.Display;
            InstallerLogService.LogInfo(
                $"Bootstrapper invoked with Action={_launchAction}, Display={_display}, Relation={_command!.Relation}, Resume={_command!.Resume}.",
                "Engine");

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                InstallerLogService.LogError($"FATAL AppDomain Unhandled Exception: {ex?.Message ?? e.ExceptionObject?.ToString()}", ex, "Crash");
            };

            // Full-display install and uninstall both have purpose-built UI. Quiet/passive and
            // embedded deployments remain headless for enterprise automation.
            bool interactive = _display == Display.Full &&
                               (_launchAction == LaunchAction.Install || _launchAction == LaunchAction.Uninstall);
            if (!interactive)
            {
                RunHeadless();
                return;
            }

            var app = new Application
            {
                Resources = new ResourceDictionary
                {
                    // Assembly-qualified: the application-relative form resolves against the
                    // entry assembly, which is fragile even though it is this one today.
                    Source = new Uri(
                        "pack://application:,,,/Nabrh.Bootstrapper;component/Resources/BootstrapperResources.xaml",
                        UriKind.Absolute)
                }
            };

            app.DispatcherUnhandledException += (s, e) =>
            {
                InstallerLogService.LogError($"FATAL WPF Dispatcher Exception: {e.Exception.Message}", e.Exception, "Crash");

                // Marking the exception handled keeps the process alive, which is right - killing
                // the BA mid-install is worse. But it previously did only that, so the wizard
                // sailed on with a half-built page and the user kept clicking Next through a UI
                // that had failed to render. Surface it and stop the run instead.
                MessageBox.Show(
                    "حدث خطأ أثناء عرض واجهة معالج التثبيت، وتم إيقاف العملية لتفادي تثبيت غير مكتمل.\n\n" +
                    $"{e.Exception.Message}\n\n" +
                    $"التفاصيل الكاملة في:\n{InstallerLogService.LogFilePath}",
                    "نبرة — خطأ في واجهة التثبيت",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                e.Handled = true;

                _exitCode = InstallerExitCode.FatalError;
                _viewModel?.Finish(false, "تعذّر عرض واجهة معالج التثبيت.", _exitCode);
            };

            // 4. Bind ViewModel and launch window
            _viewModel = new BootstrapperWizardViewModel(this.engine, _command!);
            var window = new MainWindow { DataContext = _viewModel };

            // Capture the wizard's HWND for Apply. SourceInitialized is the first point at which
            // the native handle exists, and it fires long before the user can reach Install.
            window.SourceInitialized += (s, e) =>
            {
                _applyParentHwnd = new WindowInteropHelper(window).Handle;
                InstallerLogService.LogInfo($"Apply parent window handle: 0x{_applyParentHwnd.ToInt64():X}", "UI");
            };

            // 4b. Kick off detection now. It runs on the engine thread and completes long before the
            // user reaches the Summary step; the wizard gates its Install button on _detectCompleted
            // regardless, so a slow detect cannot produce a premature Plan().
            InstallerLogService.LogInfo("Starting engine Detect phase.", "Engine");
            this.engine.Detect();

            InstallerLogService.LogInfo("Pumping WPF UI message loop for setup wizard.", "UI");
            app.Run(window);

            // 5. Cleanup single instance lock & capture final exit code
            InstallerProtection.ReleaseSingleInstance();

            if (_viewModel.ExitCode != InstallerExitCode.Success && _exitCode == InstallerExitCode.Success)
            {
                _exitCode = _viewModel.ExitCode;
            }

            InstallerLogService.LogInfo($"Shutting down WiX Engine with final ExitCode: {_exitCode} (0x{_exitCode:X8})", "Engine");
            this.engine.Quit(_exitCode);

            // Allow Burn engine time to complete the OnShutdown / OnDestroy RPC sequence cleanly
            _shutdownCompleted.Wait(TimeSpan.FromSeconds(3));
        }

        /// <summary>
        /// Drives Detect -> Plan -> Apply with no window for quiet, passive and embedded runs.
        /// </summary>
        private void RunHeadless()
        {
            InstallerLogService.LogInfo($"Running headless: Action={_launchAction}, Display={_display}.", "Engine");

            // Quiet deployment must obey the same central approval gate as the wizard. An
            // administrator supplies NabrhApprovalCode on the Burn command line; failures deny
            // the install before Detect/Plan. Uninstall is deliberately always allowed.
            if (_launchAction == LaunchAction.Install)
            {
                CudaPrerequisiteResult cuda = CudaPrerequisiteService.Check();
                bool cudaRuntimeChained;
                try
                {
                    cudaRuntimeChained = string.Equals(
                        this.engine.GetVariableString("NabrhCudaRuntimeChained"),
                        "1",
                        StringComparison.Ordinal);
                }
                catch
                {
                    cudaRuntimeChained = false;
                }

                bool cudaReadyOrInstallable = cuda.DriverAvailable
                    && (cuda.RuntimeAvailable || cudaRuntimeChained);
                if (!cudaReadyOrInstallable)
                {
                    InstallerLogService.LogError(
                        $"Headless install blocked by CUDA prerequisite: {cuda.Details}",
                        null,
                        "Prerequisites");
                    _exitCode = InstallerExitCode.PrerequisiteFailed;
                    InstallerProtection.ReleaseSingleInstance();
                    this.engine.Quit(_exitCode);
                    return;
                }

                InstallerLogService.LogInfo(
                    cuda.RuntimeAvailable
                        ? $"Headless CUDA prerequisite passed: {cuda.Details}"
                        : "Headless CUDA driver prerequisite passed; CUDA Runtime 12.1 will be installed by Burn.",
                    "Prerequisites");

                SystemAdminApprovalResult approval;
                try
                {
                    string serverUrl = this.engine.GetVariableString("NabrhApprovalServerUrl");
                    string approvalCode = this.engine.GetVariableString("NabrhApprovalCode");
                    approval = SystemAdminApprovalService
                        .RequestAsync(serverUrl, approvalCode)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception ex)
                {
                    InstallerLogService.LogError("Headless approval gate failed unexpectedly.", ex, "Approval");
                    approval = new(false, "تعذّر التحقق من تصريح مشرف النظام. تم منع التثبيت.");
                }

                if (!approval.IsCurrent)
                {
                    InstallerLogService.LogWarning($"Headless install denied: {approval.Message}", "Approval");
                    _exitCode = InstallerExitCode.AdministratorApprovalRequired;
                    InstallerProtection.ReleaseSingleInstance();
                    this.engine.Quit(_exitCode);
                    return;
                }

                this.engine.SetVariableString("NabrhApprovalToken", approval.ApprovalToken!, true);
                InstallerLogService.LogInfo(
                    $"Headless install approved. ApprovalId={approval.ApprovalId ?? "not-provided"}, ExpiresAt={approval.ExpiresAtUtc:O}",
                    "Approval");
            }

            // Apply needs a non-null parent HWND even with no visible UI.
            _applyParentHwnd = CreateHiddenParentWindow();

            // OnDetectComplete continues the sequence by calling Plan(_launchAction);
            // OnPlanComplete then calls Apply; OnApplyComplete sets _applyCompleted.
            this.engine.Detect();

            // Bounded so a wedged engine cannot hang an unattended deployment forever.
            if (!_applyCompleted.Wait(TimeSpan.FromMinutes(30)))
            {
                InstallerLogService.LogError("Headless apply did not complete within 30 minutes. Aborting.", null, "Engine");
                _exitCode = InstallerExitCode.FatalError;
            }

            InstallerProtection.ReleaseSingleInstance();
            InstallerLogService.LogInfo($"Headless run finished with ExitCode: {_exitCode} (0x{_exitCode:X8})", "Engine");
            this.engine.Quit(_exitCode);
            _shutdownCompleted.Wait(TimeSpan.FromSeconds(3));
        }

        /// <summary>
        /// Creates an invisible top-level window on a dedicated STA thread with a running message
        /// loop, and returns its handle for use as Apply's parent.
        ///
        /// It needs its own thread because the headless path blocks waiting for Apply to finish;
        /// a window created on that thread would never pump messages, and Burn can send messages
        /// to the parent it was given.
        /// </summary>
        private static IntPtr CreateHiddenParentWindow()
        {
            const int WS_POPUP = unchecked((int)0x80000000); // top-level, no WS_VISIBLE

            var ready = new ManualResetEventSlim(false);
            IntPtr handle = IntPtr.Zero;

            var thread = new Thread(() =>
            {
                try
                {
                    var parameters = new HwndSourceParameters("NabrhBootstrapperHiddenParent")
                    {
                        WindowStyle = WS_POPUP,
                        Width = 0,
                        Height = 0,
                    };
                    var source = new HwndSource(parameters);
                    handle = source.Handle;
                }
                catch (Exception ex)
                {
                    InstallerLogService.LogError("Failed to create hidden parent window for Apply.", ex, "Engine");
                }
                finally
                {
                    ready.Set();
                }

                if (handle != IntPtr.Zero)
                {
                    Dispatcher.Run();
                }
            })
            {
                IsBackground = true,
                Name = "NabrhBaHiddenWindow",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            ready.Wait(TimeSpan.FromSeconds(15));
            InstallerLogService.LogInfo($"Hidden Apply parent window handle: 0x{handle.ToInt64():X}", "Engine");
            return handle;
        }

        /// <summary>
        /// Maps a Burn package id to something a user can read. Package ids come from Bundle.wxs;
        /// anything unrecognised falls back to the id so a new chain entry still shows something.
        /// </summary>
        private static string DescribePackage(string packageId) => packageId switch
        {
            "Nabrh_MSI" => "تطبيق نبرة",
            "VcRedistX64" => "مكتبات Visual C++ 2015-2022",
            "WebView2Runtime" => "متصفّح Microsoft Edge WebView2",
            "CudaRuntime12_1" => "مكتبات NVIDIA CUDA Runtime 12.1 وcuBLAS",
            _ => packageId,
        };

        // =========================================================================
        // Engine State Machine Overrides & Logging
        // =========================================================================

        protected override void OnDetectBegin(DetectBeginEventArgs args)
        {
            base.OnDetectBegin(args);
            InstallerLogService.LogEngineEvent("OnDetectBegin", $"PackageCount={args.PackageCount}");
        }

        protected override void OnDetectPackageBegin(DetectPackageBeginEventArgs args)
        {
            base.OnDetectPackageBegin(args);
            InstallerLogService.LogEngineEvent("OnDetectPackageBegin", $"PackageId={args.PackageId}");
        }

        protected override void OnDetectPackageComplete(DetectPackageCompleteEventArgs args)
        {
            base.OnDetectPackageComplete(args);
            InstallerLogService.LogEngineEvent("OnDetectPackageComplete", $"PackageId={args.PackageId}, State={args.State}, Status={args.Status}");
        }

        protected override void OnDetectComplete(DetectCompleteEventArgs args)
        {
            base.OnDetectComplete(args);
            bool ok = args.Status >= 0;
            _detectCompleted = ok;
            InstallerLogService.LogEngineEvent("OnDetectComplete", $"Status={args.Status} (Success={ok})");

            if (!ok)
            {
                _exitCode = args.Status != 0 ? args.Status : InstallerExitCode.FatalError;
                InstallerLogService.LogError($"Detect failed with status 0x{_exitCode:X8}", null, "Engine");
                _viewModel?.Finish(false, $"تعذّر فحص مكوّنات النظام (0x{_exitCode:X8})", _exitCode);
                _applyCompleted.Set();
                return;
            }

            if (_viewModel is null)
            {
                // Headless: continue the sequence directly.
                InstallerLogService.LogInfo($"Detect succeeded. Planning {_launchAction}...", "Engine");
                this.engine.Plan(_launchAction, BundleScope.Default);
                return;
            }

            // Interactive: release the wizard's Install gate (and run a queued install, if the user
            // already pressed Install while detection was still in flight).
            _viewModel.OnDetectComplete(true);
        }

        protected override void OnPlanBegin(PlanBeginEventArgs args)
        {
            base.OnPlanBegin(args);
            InstallerLogService.LogEngineEvent("OnPlanBegin", $"PackageCount={args.PackageCount}");
        }

        protected override void OnPlanPackageBegin(PlanPackageBeginEventArgs args)
        {
            base.OnPlanPackageBegin(args);
            InstallerLogService.LogEngineEvent("OnPlanPackageBegin", $"PackageId={args.PackageId}, State={args.State}");
        }

        protected override void OnPlanPackageComplete(PlanPackageCompleteEventArgs args)
        {
            base.OnPlanPackageComplete(args);
            InstallerLogService.LogEngineEvent("OnPlanPackageComplete", $"PackageId={args.PackageId}, Status={args.Status}");
        }

        protected override void OnPlanComplete(PlanCompleteEventArgs args)
        {
            base.OnPlanComplete(args);
            InstallerLogService.LogEngineEvent("OnPlanComplete", $"Status={args.Status}");

            if (args.Status >= 0)
            {
                if (_applyParentHwnd == IntPtr.Zero)
                {
                    // Last-resort fallback. Burn hard-rejects a null parent, so failing here with a
                    // clear message beats an opaque 0x80070057 from the engine.
                    _exitCode = InstallerExitCode.FatalError;
                    InstallerLogService.LogError("No parent window handle available for Apply; aborting.", null, "Engine");
                    _viewModel?.Finish(false, IsUninstallAction ? "تعذّر تهيئة نافذة الإزالة." : "تعذّر تهيئة نافذة التثبيت.", _exitCode);
                    _applyCompleted.Set();
                    return;
                }

                InstallerLogService.LogInfo($"Plan succeeded. Triggering Apply phase (hwnd=0x{_applyParentHwnd.ToInt64():X})...", "Engine");
                _viewModel?.SetStatus("اكتمل التخطيط، بدء التنفيذ...", "الحزم جاهزة");
                try
                {
                    this.engine.Apply(_applyParentHwnd);
                }
                catch (Exception ex)
                {
                    // If Apply never starts, OnApplyComplete never fires. Without releasing the
                    // wait here the headless path sat blocked until its 30-minute timeout and the
                    // BA process was left orphaned holding the single-instance mutex.
                    _exitCode = InstallerExitCode.FatalError;
                    InstallerLogService.LogError("engine.Apply() failed to start.", ex, "Engine");
                    _viewModel?.Finish(false, IsUninstallAction ? "تعذّر بدء عملية الإزالة." : "تعذّر بدء عملية التثبيت.", _exitCode);
                    _applyCompleted.Set();
                }
            }
            else
            {
                _exitCode = args.Status != 0 ? args.Status : InstallerExitCode.FatalError;
                InstallerLogService.LogError($"Planning failed with status code 0x{_exitCode:X8}", null, "Engine");
                _viewModel?.Finish(false, $"فشل التخطيط لعملية {(IsUninstallAction ? "الإزالة" : "التثبيت")} (0x{_exitCode:X8})", _exitCode);
                _applyCompleted.Set();
            }
        }

        protected override void OnApplyBegin(ApplyBeginEventArgs args)
        {
            base.OnApplyBegin(args);
            InstallerLogService.LogEngineEvent("OnApplyBegin", "");
            _viewModel?.SetStatus(IsUninstallAction ? "بدء تنفيذ عملية الإزالة..." : "بدء تنفيذ عملية التثبيت...", "تهيئة الحزم");
        }

        protected override void OnElevateBegin(ElevateBeginEventArgs args)
        {
            base.OnElevateBegin(args);
            InstallerLogService.LogEngineEvent("OnElevateBegin", "Requesting UAC administrator elevation...");
            _viewModel?.SetStatus("في انتظار موافقة مسؤول النظام...", "طلب رفع الصلاحيات (UAC)");
        }

        protected override void OnElevateComplete(ElevateCompleteEventArgs args)
        {
            base.OnElevateComplete(args);
            InstallerLogService.LogEngineEvent("OnElevateComplete", $"Status={args.Status}");
        }

        protected override void OnExecuteBegin(ExecuteBeginEventArgs args)
        {
            base.OnExecuteBegin(args);
            InstallerLogService.LogEngineEvent("OnExecuteBegin", $"PackageCount={args.PackageCount}");
            _viewModel?.SetStatus("جارٍ تنفيذ الحزم...", $"عدد الحزم: {args.PackageCount}");
        }

        protected override void OnExecutePackageBegin(ExecutePackageBeginEventArgs args)
        {
            base.OnExecutePackageBegin(args);
            InstallerLogService.LogEngineEvent("OnExecutePackageBegin", $"PackageId={args.PackageId}, ShouldExecute={args.ShouldExecute}");
            if (args.ShouldExecute)
            {
                _viewModel?.SetStatus(DescribePackage(args.PackageId), IsUninstallAction ? "جارٍ الإزالة" : "جارٍ التثبيت");
            }
            else
            {
                _viewModel?.LogActivity(IsUninstallAction
                    ? $"{DescribePackage(args.PackageId)} — لا يحتاج إلى إزالة، تم التخطي"
                    : $"{DescribePackage(args.PackageId)} — مثبّت مسبقاً، تم التخطي");
            }
        }

        protected override void OnExecutePackageComplete(ExecutePackageCompleteEventArgs args)
        {
            base.OnExecutePackageComplete(args);
            InstallerLogService.LogEngineEvent("OnExecutePackageComplete", $"PackageId={args.PackageId}, Status={args.Status}");

            if (args.Status < 0)
            {
                _exitCode = args.Status;
                InstallerLogService.LogError($"Package {args.PackageId} failed with status code 0x{args.Status:X8}", null, "Engine");
                _viewModel?.SetStatus(
                    $"فشل {(IsUninstallAction ? "إزالة" : "تثبيت")} {DescribePackage(args.PackageId)}",
                    $"رمز الخطأ 0x{args.Status:X8}",
                    isError: true);
            }
            else
            {
                _viewModel?.LogActivity($"{DescribePackage(args.PackageId)} — اكتمل");
            }
        }

        protected override void OnExecuteProgress(ExecuteProgressEventArgs args)
        {
            base.OnExecuteProgress(args);
            _viewModel?.UpdateProgress(args.OverallPercentage,
                $"جاري {(IsUninstallAction ? "الإزالة" : "التثبيت")}... {args.OverallPercentage}%");
        }

        protected override void OnError(ErrorEventArgs args)
        {
            base.OnError(args);
            _exitCode = args.ErrorCode != 0 ? args.ErrorCode : InstallerExitCode.FatalError;
            InstallerLogService.LogError($"WiX Engine Error ({args.ErrorType}) Code: 0x{args.ErrorCode:X8} ({args.ErrorCode}): {args.ErrorMessage}", null, "Engine");

            // Burn blocks waiting for the BA to answer error prompts (e.g. MSI "retry/cancel").
            // Leaving Result unset stalls the install indefinitely — fatal under /quiet.
            // There is no UI to ask, so decline and let the package report its own failure.
            args.Result = Result.Cancel;
        }

        protected override void OnApplyComplete(ApplyCompleteEventArgs args)
        {
            base.OnApplyComplete(args);
            bool ok = args.Status >= 0;
            _exitCode = ok ? InstallerExitCode.Success : (args.Status != 0 ? args.Status : InstallerExitCode.FatalError);

            InstallerLogService.LogEngineEvent("OnApplyComplete", $"Status={args.Status}, Final ExitCode=0x{_exitCode:X8}");
            string operation = IsUninstallAction ? "الإزالة" : "التثبيت";
            _viewModel?.Finish(ok,
                ok ? $"تمت {operation} بنجاح!" : $"فشلت {operation} (رمز الخطأ: 0x{_exitCode:X8}). يرجى مراجعة سجلات النظام.",
                _exitCode);
            _applyCompleted.Set();
        }

        protected override void OnStartup(WixToolset.BootstrapperApplicationApi.StartupEventArgs args)
        {
            base.OnStartup(args);
            InstallerLogService.LogEngineEvent("OnStartup", "Engine startup sequence completed.");
        }

        protected override void OnShutdown(ShutdownEventArgs args)
        {
            base.OnShutdown(args);
            InstallerLogService.LogEngineEvent("OnShutdown", $"Action={args.Action}, HResult=0x{args.HResult:X8}");

            // Release Run()/RunHeadless() immediately instead of always burning the full 3s timeout.
            _shutdownCompleted.Set();
        }

        protected override void OnDestroy(DestroyEventArgs args)
        {
            base.OnDestroy(args);
            InstallerLogService.LogEngineEvent("OnDestroy", $"Reload={args.Reload}");
        }

        private bool IsUninstallAction => _launchAction == LaunchAction.Uninstall;
    }
}
