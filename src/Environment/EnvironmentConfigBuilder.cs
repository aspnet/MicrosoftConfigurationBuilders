// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// A ConfigurationProvider that retrieves values from environment variables.
    /// </summary>
    public class EnvironmentConfigBuilder : KeyValueConfigBuilder
    {
        #pragma warning disable CS1591 // No xml comments for tag literals.
        public const string mapHierarchySeparatorTag = "mapHierarchySeparator";
        #pragma warning restore CS1591 // No xml comments for tag literals.

        private bool _mapHierarchySeparator;

        protected override void LazyInitialize(string name, NameValueCollection config)
        {
            base.LazyInitialize(name, config);

            if (!Boolean.TryParse(UpdateConfigSettingWithAppSettings(mapHierarchySeparatorTag), out _mapHierarchySeparator))
                _mapHierarchySeparator = false;
        }

        /// <summary>
        /// Looks up a single 'value' for the given 'key.'
        /// </summary>
        /// <param name="key">The 'key' to look up in the environment. (Prefix handling is not needed here.)</param>
        /// <returns>The value corresponding to the given 'key' or null if no value is found.</returns>
        public override string GetValue(string key)
        {
            try
            {
                return Environment.GetEnvironmentVariable(key);
            }
            catch (Exception)
            {
                if (!Optional)
                    throw;
            }

            return null;
        }

        /// <summary>
        /// Retrieves all known key/value pairs from the environment where the key begins with with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="prefix">A prefix string to filter the list of potential keys retrieved from the source.</param>
        /// <returns>A collection of key/value pairs.</returns>
        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();

            try
            {
                foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
                {
                    // This won't ever throw for duplicate entries since underlying environment is case-insensitive.
                    if (de.Key.ToString()?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                        values.Add(de.Key.ToString(), de.Value.ToString());
                }
            }
            catch (Exception)
            {
                if (!Optional)
                    throw;
            }

            return values;
        }

        /// <summary>
        /// Transform the given key to an intermediate format that will be used to look up values in backing store.
        /// </summary>
        /// <param name="key">The string to be mapped.</param>
        /// <returns>The key string to be used while looking up config values..</returns>
        public override string MapKey(string key)
        {
            // Colons are common in appSettings keys, but not allowed in environment variable names on some platforms.
            // It's likely that apps will want to lookup config values with these characters in their name.
            // More extensive key mapping can be done with subclasses. But let's handle the most
            // most common case here.
            if (_mapHierarchySeparator)
                key = key.Replace(":", "__");
            
            return key;
        }
    }
}
