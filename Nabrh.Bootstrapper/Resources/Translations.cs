using ERPUI.Services.Localization;

namespace ERPUI.Resources
{
    /// <summary>
    /// Static helper class for programmatic access to localized strings in C# code.
    /// </summary>
    public static class Translations
    {
        public static string Get(string key) => LocalizationManager.Instance[key];

        public static string Get(string key, string defaultValue)
        {
            var result = LocalizationManager.Instance[key];
            return result.StartsWith('[') && result.EndsWith(']') ? defaultValue : result;
        }
    }
}

