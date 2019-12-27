// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System.Configuration;
using System.Collections.Generic;
using System.Configuration.Provider;
using System.Collections.Specialized;
using System;
using System.Linq.Expressions;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    // Covariance is not allowed with generic classes. Lets use this trick instead.
    internal interface ISectionHandler
    {
        void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null);
        IEnumerator<KeyValuePair<string, object>> GetEnumerator();
        string TryGetOriginalCase(string requestedKey);
    }

    /// <summary>
    /// A class to be used by <see cref="KeyValueConfigBuilder"/>s to apply key/value config pairs to .Net configuration sections.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="ConfigurationSection"/> that the implementing class can process.</typeparam>
    public abstract class SectionHandler<T> : ProviderBase, ISectionHandler where T : ConfigurationSection
    {
        /// <summary>
        /// The <see cref="ConfigurationSection"/> instance being processed by this <see cref="SectionHandler{T}"/>.
        /// </summary>
        public T ConfigSection { get; private set; }

        /// <summary>
        /// Gets an <see cref="IEnumerator{T}"/> that iterates over the key/value pairs contained in the assigned <see cref="ConfigSection"/>. />
        /// </summary>
        /// <returns>An enumerator over pairs consisting of the existing key for a config value in the config section, and an object reference
        /// for the key/value pair to be passed in to <see cref="InsertOrUpdate(string, string, string, object)"/> while processing the config section.</returns>
        public abstract IEnumerator<KeyValuePair<string, object>> GetEnumerator();

        /// <summary>
        /// Updates an existing config value in the assigned <see cref="ConfigSection"/> with a new key and a new value. The old config value
        /// can be located using the <paramref name="oldKey"/> or <paramref name="oldItem"/> parameters. If an old config value is not
        /// found, a new config value should be inserted.
        /// </summary>
        /// <param name="newKey">The updated key name for the config item.</param>
        /// <param name="newValue">The updated value for the config item.</param>
        /// <param name="oldKey">The old key name for the config item, or null.</param>
        /// <param name="oldItem">A reference to the old key/value pair obtained by <see cref="GetEnumerator"/>, or null.</param>
        public abstract void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null);

        /// <summary>
        /// Attempt to lookup the original key casing so it can be preserved during greedy updates which would otherwise lose
        /// the original casing in favor of the casing used in the config source.
        /// </summary>
        /// <param name="requestedKey">The key to find original casing for.</param>
        /// <returns>Unless overridden, returns the string passed in.</returns>
        public virtual string TryGetOriginalCase(string requestedKey)
        {
            return requestedKey;
        }

        private void Initialize(string name, T configSection, NameValueCollection config)
        {
            ConfigSection = configSection;
            Initialize(name, config);
        }
    }

    /// <summary>
    /// A class that can be used by <see cref="KeyValueConfigBuilder"/>s to apply key/value config pairs to <see cref="AppSettingsSection"/>.
    /// </summary>
    public class AppSettingsSectionHandler : SectionHandler<AppSettingsSection>
    {
        /// <summary>
        /// Updates an existing app setting in the assigned <see cref="SectionHandler{T}.ConfigSection"/> with a new key and a new value. The old setting
        /// can be located using the <paramref name="oldKey"/> parameter. If an old setting is not found, a new setting should be inserted.
        /// </summary>
        /// <param name="newKey">The updated key name for the app setting.</param>
        /// <param name="newValue">The updated value for the app setting.</param>
        /// <param name="oldKey">The old key name for the app setting, or null.</param>
        /// <param name="oldItem">The old key name for the app setting, or null., or null.</param>
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

        /// <summary>
        /// Gets an <see cref="IEnumerator{T}"/> that iterates over the key/value pairs contained in the assigned <see cref="SectionHandler{T}.ConfigSection"/>. />
        /// </summary>
        /// <returns>An enumerator over pairs where both values of the pair are the existing key for each setting in the appSettings section.</returns>
        public override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            // Grab a copy of the keys array since we are using 'yield' and the Settings collection may change on us.
            string[] keys = (string[])ConfigSection.Settings.AllKeys;
            foreach (string key in keys)
                yield return new KeyValuePair<string, object>(key, key);
        }

        /// <summary>
        /// Attempt to lookup the original key casing so it can be preserved during greedy updates which would otherwise lose
        /// the original casing in favor of the casing used in the config source.
        /// </summary>
        /// <param name="requestedKey">The key to find original casing for.</param>
        /// <returns>A string containing the key with original casing from the config section, or the key as passed in if no match
        /// can be found.</returns>
        public override string TryGetOriginalCase(string requestedKey)
        {
            if (!String.IsNullOrWhiteSpace(requestedKey))
            {
                var keyval = ConfigSection.Settings[requestedKey];
                if (keyval != null)
                    return keyval.Key;
            }

            return base.TryGetOriginalCase(requestedKey);
        }
    }

    /// <summary>
    /// A class that can be used by <see cref="KeyValueConfigBuilder"/>s to apply key/value config pairs to <see cref="ConnectionStringsSection"/>.
    /// </summary>
    public class ConnectionStringsSectionHandler : SectionHandler<ConnectionStringsSection>
    {
        /// <summary>
        /// Updates an existing connection string in the assigned <see cref="SectionHandler{T}.ConfigSection"/> with a new name and a new value. The old
        /// connection string can be located using the <paramref name="oldItem"/> parameter. If an old connection string is not found, a new connection
        /// string should be inserted.
        /// </summary>
        /// <param name="newKey">The updated key name for the connection string.</param>
        /// <param name="newValue">The updated value for the connection string.</param>
        /// <param name="oldKey">The old key name for the connection string, or null.</param>
        /// <param name="oldItem">A reference to the old <see cref="ConnectionStringSettings"/> object obtained by <see cref="GetEnumerator"/>, or null.</param>
        public override void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null)
        {
            if (newValue != null)
            {
                // Preserve the old entry if it exists, as it might have more than just name/connectionString attributes.
                ConnectionStringSettings cs = (oldItem as ConnectionStringSettings) ?? new ConnectionStringSettings();

                // Make sure there are no entries using the old or new name other than this one
                ConfigSection.ConnectionStrings.Remove(oldKey);
                ConfigSection.ConnectionStrings.Remove(newKey);

                // Update values and re-add to the collection
                cs.Name = newKey;
                cs.ConnectionString = newValue;
                ConfigSection.ConnectionStrings.Add(cs);
            }
        }

        /// <summary>
        /// Gets an <see cref="IEnumerator{T}"/> that iterates over the key/value pairs contained in the assigned <see cref="SectionHandler{T}.ConfigSection"/>. />
        /// </summary>
        /// <returns>An enumerator over pairs where the key is the existing name for each setting in the connectionStrings section, and the value
        /// is a reference to the <see cref="ConnectionStringSettings"/> object referred to by that name.</returns>
        public override IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            // The ConnectionStrings collection may change on us while we enumerate. :/
            ConnectionStringSettings[] connStrs = new ConnectionStringSettings[ConfigSection.ConnectionStrings.Count];
            ConfigSection.ConnectionStrings.CopyTo(connStrs, 0);

            foreach (ConnectionStringSettings cs in connStrs)
                yield return new KeyValuePair<string, object>(cs.Name, cs);
        }

        /// <summary>
        /// Attempt to lookup the original key casing so it can be preserved during greedy updates which would otherwise lose
        /// the original casing in favor of the casing used in the config source.
        /// </summary>
        /// <param name="requestedKey">The key to find original casing for.</param>
        /// <returns>A string containing the key with original casing from the config section, or the key as passed in if no match
        /// can be found.</returns>
        public override string TryGetOriginalCase(string requestedKey)
        {
            if (!String.IsNullOrWhiteSpace(requestedKey))
            {
                var connStr = ConfigSection.ConnectionStrings[requestedKey];
                if (connStr != null)
                    return connStr.Name;
            }

            return base.TryGetOriginalCase(requestedKey);
        }
    }
}
