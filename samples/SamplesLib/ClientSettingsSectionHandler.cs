using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Xml;
using Microsoft.Configuration.ConfigurationBuilders;

namespace SamplesLib
{
    /// <summary>
    /// A class that can be used by <see cref="KeyValueConfigBuilder"/>s to apply key/value config pairs to <see cref="ClientSettingsSection"/>.
    /// </summary>
    public class ClientSettingsSectionHandler : SectionHandler<ClientSettingsSection>
    {
        private readonly XmlDocument _doc = new XmlDocument();

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
                // Make sure there are no entries using the old or new name other than this one
                SettingElement oldSetting = oldItem as SettingElement;
                if (oldSetting != null)
                    ConfigSection.Settings.Remove(oldSetting);
                SettingElement conflictSetting = ConfigSection.Settings.Get(newKey);
                if (conflictSetting != null)
                    ConfigSection.Settings.Remove(conflictSetting);

                // Create the new setting, preserving serializeAs type if possible
                SettingElement setting = oldSetting ?? new SettingElement();
                setting.Name = newKey;

                // Set the new value
                SettingValueElement v = new SettingValueElement();
                v.ValueXml = _doc.CreateElement("value");
                v.ValueXml.InnerXml = newValue;
                setting.Value = v;

                // Add the setting to the config section
                ConfigSection.Settings.Add(setting);
            }
        }

        /// <summary>
        /// Gets an <see cref="IEnumerable{T}"/> for iterating over the key/value pairs contained in the assigned <see cref="SectionHandler{T}.ConfigSection"/>. />
        /// </summary>
        /// <returns>An enumerator over tuples where the values of the tuple are the existing key for each setting, the old value for the
        /// setting, and the existing key for the setting again as the state which will be returned unmodified when updating.</returns>
        public override IEnumerable<Tuple<string, string, object>> KeysValuesAndState()
        {
            // Grab a copy of the settings collection since we are using 'yield' and the collection may change on us.
            SettingElement[] allSettings = new SettingElement[ConfigSection.Settings.Count];
            ConfigSection.Settings.CopyTo(allSettings, 0);

            foreach (SettingElement setting in allSettings)
                yield return Tuple.Create(setting.Name, setting.Value?.ValueXml?.InnerXml, (object)setting);
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
                var setting = ConfigSection.Settings.Get(requestedKey);
                if (setting != null)
                    return setting.Name;
            }

            return base.TryGetOriginalCase(requestedKey);
        }
    }
}
