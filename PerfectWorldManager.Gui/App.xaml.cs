using PerfectWorldManager.Core;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;

namespace PerfectWorldManager.Gui
{
    public partial class App : Application
    {
        public static Settings AppSettings { get; private set; }

        public App()
        {
            AppSettings = SettingsManager.LoadSettings();
            SwitchLanguage(AppSettings.SelectedLanguage);
        }

        public static void SwitchLanguage(string languageCode)
        {
            ResourceDictionary newDict = new ResourceDictionary();
            string appliedLanguageCode = languageCode;

            switch (languageCode)
            {
                case "id-ID":
                    newDict.Source = new Uri("Resources/Strings.id-ID.xaml", UriKind.Relative);
                    break;
                case "ru-RU":
                    newDict.Source = new Uri("Resources/Strings.ru-RU.xaml", UriKind.Relative);
                    break;
                case "pt-PT": // *** ADDED CASE FOR PORTUGUESE (Portugal) ***
                    newDict.Source = new Uri("Resources/Strings.pt-PT.xaml", UriKind.Relative);
                    break;
                // You can add "pt-BR" as another case if you want to distinguish Brazilian Portuguese
                // case "pt-BR": 
                //    newDict.Source = new Uri("Resources/Strings.pt-BR.xaml", UriKind.Relative);
                //    break;
                case "en-US":
                default:
                    appliedLanguageCode = "en-US";
                    newDict.Source = new Uri("Resources/Strings.en-US.xaml", UriKind.Relative);
                    break;
            }

            // Remove any existing language dictionary
            var existingLangDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null &&
                                     (d.Source.OriginalString.EndsWith("Strings.en-US.xaml", StringComparison.OrdinalIgnoreCase) ||
                                      d.Source.OriginalString.EndsWith("Strings.id-ID.xaml", StringComparison.OrdinalIgnoreCase) ||
                                      d.Source.OriginalString.EndsWith("Strings.ru-RU.xaml", StringComparison.OrdinalIgnoreCase) ||
                                      d.Source.OriginalString.EndsWith("Strings.pt-PT.xaml", StringComparison.OrdinalIgnoreCase) || // *** ADDED CHECK FOR PORTUGUESE FILE ***
                                      d.Source.OriginalString.EndsWith("Strings.pt-BR.xaml", StringComparison.OrdinalIgnoreCase)  // *** (Optional) Add if you support pt-BR separately ***
                                      ));

            if (existingLangDict != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existingLangDict);
            }

            Application.Current.Resources.MergedDictionaries.Add(newDict);

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(appliedLanguageCode);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(appliedLanguageCode);
            }
            catch (CultureNotFoundException)
            {
                var fallbackCulture = new CultureInfo("en-US");
                Thread.CurrentThread.CurrentCulture = fallbackCulture;
                Thread.CurrentThread.CurrentUICulture = fallbackCulture;
            }

            if (AppSettings != null)
            {
                AppSettings.SelectedLanguage = appliedLanguageCode;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (AppSettings == null)
            {
                AppSettings = SettingsManager.LoadSettings();
                SwitchLanguage(AppSettings.SelectedLanguage);
            }
        }
    }
}