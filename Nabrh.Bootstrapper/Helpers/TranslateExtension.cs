using System;
using System.Windows.Data;
using System.Windows.Markup;
using ERPUI.Services.Localization;

namespace ERPUI.Helpers
{
    /// <summary>
    /// Custom WPF MarkupExtension providing dynamic localization bindings ({loc:Translate Key})
    /// backed by LocalizationManager.Instance without third-party dependencies.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class TranslateExtension : MarkupExtension
    {
        public string Key { get; set; }

        public TranslateExtension()
        {
            Key = string.Empty;
        }

        public TranslateExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key)) return string.Empty;

            var binding = new Binding($"[{Key}]")
            {
                Source = LocalizationManager.Instance,
                Mode = BindingMode.OneWay
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}

