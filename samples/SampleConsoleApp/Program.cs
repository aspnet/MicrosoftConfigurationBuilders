using System;
using System.Collections.Specialized;
using System.Configuration;

namespace SampleConsoleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("---------- App Settings ----------");
            foreach (string appsetting in ConfigurationManager.AppSettings.Keys)
            {
                Console.WriteLine($"{appsetting}\t{ConfigurationManager.AppSettings[appsetting]}");
            }

            Console.WriteLine("");
            Console.WriteLine("");

            Console.WriteLine("---------- Custom Settings ----------");
            var customSettings = ConfigurationManager.GetSection("customSettings") as NameValueCollection;
            foreach (string setting in customSettings.Keys)
            {
                Console.WriteLine($"{setting}\t{customSettings[setting]}");
            }

            Console.WriteLine("");
            Console.WriteLine("");

            Console.WriteLine("---------- Expanded Settings ----------");
            var expandedSettings = ConfigurationManager.GetSection("expandedSettings") as NameValueCollection;
            foreach (string setting in expandedSettings.Keys)
            {
                Console.WriteLine($"{setting}\t{expandedSettings[setting]}");
            }

            Console.WriteLine("");
            Console.WriteLine("");

            Console.WriteLine("---------- Client Application Settings ----------");
            Console.WriteLine("Note: These _might_ be inaccurate due to the additional layers of building and caching that the");
            Console.WriteLine("\tClient Application Settings framework uses. Read more about the Settings architecture");
            Console.WriteLine("\there: https://docs.microsoft.com/en-us/dotnet/desktop/winforms/advanced/application-settings-architecture?view=netframeworkdesktop-4.8");
            Console.WriteLine("-------------------------------------------------");
            foreach (SettingsProperty sp in ClientSettings.Default.Properties)
            {
                Console.WriteLine($"{sp.Name}\t{ClientSettings.Default[sp.Name]}");
            }
            Console.WriteLine("-------------------------------------------------");
            Console.WriteLine("Note: It is also possible to skip the extra layers of the Client Application Settings framework.");
            Console.WriteLine("\tHowever, going this route loses the strongly and richly typed nature of Client Application");
            Console.WriteLine("\tsettings. And at this point, its no different from using 'appSettings.' Code that goes through");
            Console.WriteLine("\tthe full Client Settings stack will still see things as they look above, not below.");
            Console.WriteLine("-------------------------------------------------");
            var css = ConfigurationManager.GetSection("applicationSettings/SampleConsoleApp.ClientSettings") as ClientSettingsSection;
            foreach (SettingElement setting in css.Settings)
            {
                Console.WriteLine($"{setting.Name}\t{setting.Value.ValueXml.InnerXml}");
            }
        }
    }
}
