// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// Possible modes (or behaviors) for key/value substitution.
    /// </summary>
    public enum KeyValueMode
    {
        /// <summary>
        /// Replaces 'value' if 'key' is matched. Only operates on known key/value config sections.
        /// </summary>
        Strict,
        /// <summary>
        /// Inserts all 'values' regardless of the previous existence of the 'key.' Only operates on known key/value config sections.
        /// </summary>
        Greedy,
        /// <summary>
        /// Replace 'key'-specifying tokens in raw xml with 'values.' Operates on any config section.
        /// </summary>
        Expand
    }
}
