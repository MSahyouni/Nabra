using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WixToolset.BootstrapperApplicationApi;

namespace Nabrh.Bootstrapper.ViewModels
{
    // Pure UI-state/logic for the bootstrapper window. The engine STATE MACHINE lives in
    // ERPUIBootstrapperApplication (it owns the engine callbacks); this VM only exposes the
    // XAML-bound surface (StatusMessage / ProgressPercentage / StartInstallCommand /
    // StartUninstallCommand) and marshals engine-thread updates onto the WPF Dispatcher.
    public partial class MainBootstrapperViewModel : ObservableObject
    {
        private readonly IEngine _engine;
        private readonly IBootstrapperCommand _command;
        private readonly Dispatcher _dispatcher;

        [ObservableProperty] private string _statusMessage = "جاري تحضير التثبيت...";
        [ObservableProperty] private double _progressPercentage;
        [ObservableProperty] private bool _isExecuting;

        public MainBootstrapperViewModel(IEngine engine, IBootstrapperCommand command)
        {
            _engine = engine;
            _command = command;
            _dispatcher = Dispatcher.CurrentDispatcher; // captured on the STA UI thread
        }

        public void UpdateProgress(double progress, string message)
        {
            _dispatcher.InvokeAsync(() =>
            {
                ProgressPercentage = progress;
                StatusMessage = message;
            });
        }

        public void OnDetectComplete(bool succeeded)
        {
            _dispatcher.InvokeAsync(() =>
                StatusMessage = succeeded ? "جاهز للتثبيت." : "تعذّر فحص مكوّنات النظام.");
        }

        public void Finish(bool succeeded, string message)
        {
            _dispatcher.InvokeAsync(() =>
            {
                IsExecuting = false;
                if (succeeded)
                {
                    ProgressPercentage = 100;
                }
                StatusMessage = message;
            });
        }

        [RelayCommand]
        private void StartInstall()
        {
            IsExecuting = true;
            // v7: Plan takes (LaunchAction, BundleScope). Let Burn choose the scope.
            _engine.Plan(LaunchAction.Install, BundleScope.Default);
        }

        [RelayCommand]
        private void StartUninstall()
        {
            IsExecuting = true;
            _engine.Plan(LaunchAction.Uninstall, BundleScope.Default);
        }
    }
}

