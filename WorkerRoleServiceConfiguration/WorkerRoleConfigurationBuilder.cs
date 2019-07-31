using System;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    public class WorkerRoleConfigurationBuilder : KeyValueConfigBuilder
    {
        public override string GetValue(string key)
        {
            return null;
        }

        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            return new Dictionary<string, string>();
        }

        public override ConfigurationSection ProcessConfigurationSection(ConfigurationSection configSection)
        {
            var res = base.ProcessConfigurationSection(configSection);

            switch (configSection)
            {
                case ConnectionStringsSection connectionStringsSection:
                    OverrideConnectionStringsWithCloud(connectionStringsSection);
                    break;

                case AppSettingsSection appSettingsSection:
                    OverrideAppSettingsWithCloud(appSettingsSection);
                    break;
            }

            return res;
        }

        private void OverrideConnectionStringsWithCloud(ConnectionStringsSection section)
        {
            foreach (ConnectionStringSettings conString in section.ConnectionStrings)
            {
                conString.ConnectionString = ResolveAppSettingValue(key: conString.Name, defaultValue: conString.ConnectionString);
            }
        }

        private void OverrideAppSettingsWithCloud(AppSettingsSection section)
        {
            foreach (KeyValueConfigurationElement appSetting in section.Settings)
            {
                appSetting.Value = ResolveAppSettingValue(key: appSetting.Key, defaultValue: appSetting.Value);
            }
        }


        private string ResolveAppSettingValue(string key, string defaultValue)
        {
            var hasSettingOnCloud = TryGetCloudAppSetting(key, out var cloudSettingValue);
            return (hasSettingOnCloud) ? cloudSettingValue : defaultValue;
        }

        private bool TryGetCloudAppSetting(string key, out string settingValue)
        {
            try
            {
                settingValue = RoleEnvironment.GetConfigurationSettingValue(key);
                return !string.IsNullOrWhiteSpace(settingValue);
            }
            catch (Exception)
            {
                // RoleEnvironment.GetConfigurationSettingValue throws if no value, so we want to trace this error and move on. 
                settingValue = null;
                return false;
            }
        }
    }
}
