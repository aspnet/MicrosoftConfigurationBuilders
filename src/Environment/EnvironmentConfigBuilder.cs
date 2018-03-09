// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    public class EnvironmentConfigBuilder : KeyValueConfigBuilder
    {
        public override string GetValue(string key)
        {
            return Environment.GetEnvironmentVariable(key);
        }

        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();

            foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
            {
                // This won't ever throw for duplicate entries since underlying environment is case-insensitive.
                if (de.Key.ToString()?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                    values.Add(de.Key.ToString(), de.Value.ToString());
            }

            return values;
        }
    }
}
