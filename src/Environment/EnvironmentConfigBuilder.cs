// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// A ConfigurationProvider that retrieves values from environment variables.
    /// </summary>
    public class EnvironmentConfigBuilder : KeyValueConfigBuilder
    {
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
    }
}
