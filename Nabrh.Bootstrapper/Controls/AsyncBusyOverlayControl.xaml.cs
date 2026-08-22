using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace ERPUI.Controls
{
    public partial class AsyncBusyOverlayControl : UserControl
    {
        public AsyncBusyOverlayControl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty StatusTitleProperty =
            DependencyProperty.Register(
                nameof(StatusTitle),
                typeof(string),
                typeof(AsyncBusyOverlayControl),
                new PropertyMetadata("جاري معالجة العملية..."));

        public string StatusTitle
        {
            get => (string)GetValue(StatusTitleProperty);
            set => SetValue(StatusTitleProperty, value);
        }

        public static readonly DependencyProperty SubStatusMessageProperty =
            DependencyProperty.Register(
                nameof(SubStatusMessage),
                typeof(string),
                typeof(AsyncBusyOverlayControl),
                new PropertyMetadata("يرجى الانتظار حتى التكتم أو اضغط إلغاء للرجوع."));

        public string SubStatusMessage
        {
            get => (string)GetValue(SubStatusMessageProperty);
            set => SetValue(SubStatusMessageProperty, value);
        }

        public static readonly DependencyProperty SymbolIconProperty =
            DependencyProperty.Register(
                nameof(SymbolIcon),
                typeof(SymbolRegular),
                typeof(AsyncBusyOverlayControl),
                new PropertyMetadata(SymbolRegular.ArrowSync24));

        public SymbolRegular SymbolIcon
        {
            get => (SymbolRegular)GetValue(SymbolIconProperty);
            set => SetValue(SymbolIconProperty, value);
        }

        public static readonly DependencyProperty IsIndeterminateProperty =
            DependencyProperty.Register(
                nameof(IsIndeterminate),
                typeof(bool),
                typeof(AsyncBusyOverlayControl),
                new PropertyMetadata(true));

        public bool IsIndeterminate
        {
            get => (bool)GetValue(IsIndeterminateProperty);
            set => SetValue(IsIndeterminateProperty, value);
        }

        public static readonly DependencyProperty ProgressValueProperty =
            DependencyProperty.Register(
                nameof(ProgressValue),
                typeof(double),
                typeof(AsyncBusyOverlayControl),
                new PropertyMetadata(0.0));

        public double ProgressValue
        {
            get => (double)GetValue(ProgressValueProperty);
            set => SetValue(ProgressValueProperty, value);
        }

        public static readonly DependencyProperty CancelCommandProperty =
            DependencyProperty.Register(
                nameof(CancelCommand),
                typeof(ICommand),
                typeof(AsyncBusyOverlayControl),
                new PropertyMetadata(null));

        public ICommand CancelCommand
        {
            get => (ICommand)GetValue(CancelCommandProperty);
            set => SetValue(CancelCommandProperty, value);
        }
    }
}

