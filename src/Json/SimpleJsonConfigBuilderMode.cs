// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// Possible modes (or paradigms) for parsing a json source file.
    /// </summary>
    public enum SimpleJsonConfigBuilderMode
    {
        /// <summary>
        /// The whole file is flattened into a single key/value collection.
        /// </summary>
        Flat,
        /// <summary>
        /// Use top-level objects to separate into different dictionaries per config section.
        /// </summary>
        Sectional
    }
}
