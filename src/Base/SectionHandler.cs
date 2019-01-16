// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System.Configuration;
using System.Collections.Generic;
using System.Collections;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    // Covariance is not allowed with generic classes. Lets use this trick instead.
    internal interface ISectionHandler
    {
        void InsertOrUpdate(string newKey, string newValue, object oldItem);
        IEnumerator<KeyValuePair<string, object>> GetEnumerator();
    }

    public abstract class SectionHandler<T> : ISectionHandler where T : ConfigurationSection
    {
        public T ConfigSection { get; private set; }
        public abstract void InsertOrUpdate(string newKey, string newValue, object oldItem);
        public abstract IEnumerator<KeyValuePair<string, object>> GetEnumerator();
    }

    public class AppSettingsSectionHandler : SectionHandler<AppSettingsSection>
    {
        public override void InsertOrUpdate(string newKey, string newValue, object oldItem)
        {
            if (newValue != null)
            {
                string oldKey = oldItem as string;
                if (oldKey != null)
                    ConfigSection.Settings.Remove(oldKey);

                ConfigSection.Settings.Remove(newKey);
                ConfigSection.Settings.Add(newKey, newValue);
            }
        }

        public override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            foreach (string key in ConfigSection.Settings.AllKeys)
                yield return new KeyValuePair<string, object>(key, key);
        }
    }

    public class ConnectionStringsSectionHandler : SectionHandler<ConnectionStringsSection>
    {
        public override void InsertOrUpdate(string newKey, string newValue, object oldItem)
        {
            if (newValue != null)
            {
                ConnectionStringSettings oldSettings = oldItem as ConnectionStringSettings;
                if (oldSettings != null)
                    ConfigSection.ConnectionStrings.Remove(oldSettings);

                ConnectionStringSettings newSettings = ConfigSection.ConnectionStrings[newKey] ?? new ConnectionStringSettings();
                newSettings.Name = newKey;
                newSettings.ConnectionString = newValue;
                ConfigSection.ConnectionStrings.Add(newSettings);
            }
        }

        public override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            foreach (ConnectionStringSettings cs in ConfigSection.ConnectionStrings)
                yield return new KeyValuePair<string, object>(cs.Name, cs);
        }
    }
}
