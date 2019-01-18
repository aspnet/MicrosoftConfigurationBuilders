// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System.Configuration;
using System.Collections.Generic;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    // Covariance is not allowed with generic classes. Lets use this trick instead.
    internal interface ISectionHandler
    {
        void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null);
        IEnumerator<KeyValuePair<string, object>> GetEnumerator();
    }

    public abstract class SectionHandler<T> : ISectionHandler where T : ConfigurationSection
    {
        public T ConfigSection { get; private set; }
        public abstract void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null);
        public abstract IEnumerator<KeyValuePair<string, object>> GetEnumerator();
    }

    public class AppSettingsSectionHandler : SectionHandler<AppSettingsSection>
    {
        public override void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null)
        {
            if (newValue != null)
            {
                if (oldKey != null)
                    ConfigSection.Settings.Remove(oldKey);

                ConfigSection.Settings.Remove(newKey);
                ConfigSection.Settings.Add(newKey, newValue);
            }
        }

        public override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            // Grab a copy of the keys array since we are using 'yield' and the Settings collection may change on us.
            string[] keys = (string[])ConfigSection.Settings.AllKeys;
            foreach (string key in keys)
                yield return new KeyValuePair<string, object>(key, key);
        }
    }

    public class ConnectionStringsSectionHandler : SectionHandler<ConnectionStringsSection>
    {
        public override void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldValue = null)
        {
            if (newValue != null)
            {
                // Preserve the old entry if it exists, as it might have more than just name/connectionString attributes.
                ConnectionStringSettings cs = (oldValue as ConnectionStringSettings) ?? new ConnectionStringSettings();

                // Make sure there are no entries using the old or new name other than this one
                ConfigSection.ConnectionStrings.Remove(oldKey);
                ConfigSection.ConnectionStrings.Remove(newKey);

                // Update values and re-add to the collection
                cs.Name = newKey;
                cs.ConnectionString = newValue;
                ConfigSection.ConnectionStrings.Add(cs);
            }
        }

        public override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            // The ConnectionStrings collection may change on us while we enumerate. :/
            ConnectionStringSettings[] connStrs = new ConnectionStringSettings[ConfigSection.ConnectionStrings.Count];
            ConfigSection.ConnectionStrings.CopyTo(connStrs, 0);

            foreach (ConnectionStringSettings cs in connStrs)
                yield return new KeyValuePair<string, object>(cs.Name, cs);
        }
    }
}
