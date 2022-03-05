// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// A ConfigurationProvider that uses a directory's files as a source of values. A file's name is the key, and the contents are the value.
    /// </summary>
    public class KeyPerFileConfigBuilder : KeyValueConfigBuilder
    {
        #pragma warning disable CS1591 // No xml comments for tag literals.
        public const string directoryPathTag = "directoryPath";
        public const string keyDelimiterTag = "keyDelimiter";
        public const string ignorePrefixTag = "ignorePrefix";
        #pragma warning restore CS1591 // No xml comments for tag literals.

        /// <summary>
        /// Gets or sets a path to the source directory to look in for values.
        /// </summary>
        public string DirectoryPath { get; protected set; }
        /// <summary>
        /// If specified, the config builder will traverse multiple levels of the directory, building key names with this delimeter.
        /// If null, the config builder only looks at the top-level of the directory. This is the default.
        /// </summary>
        public string KeyDelimiter { get; protected set; }
        /// <summary>
        /// Gets or sets a prefix string. Files that start with this prefix will be excluded.
        /// Defaults to "ignore.".
        /// </summary>
        public string IgnorePrefix { get; protected set; }

        private readonly ConcurrentDictionary<string, string> _allValues = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes the configuration builder lazily.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        protected override void LazyInitialize(string name, NameValueCollection config)
        {
            // Default to 'Enabled'. base.Initialize() will override if specified in config.
            Enabled = KeyValueEnabled.Enabled;

            base.LazyInitialize(name, config);

            // At this point, we have our 'Enabled' choice. If we are disabled, we can stop right here.
            if (Enabled == KeyValueEnabled.Disabled) return;

            string directoryPath = UpdateConfigSettingWithAppSettings(directoryPathTag);
            DirectoryPath = Utils.MapPath(directoryPath, CurrentSection);
            if (!IsOptional && (String.IsNullOrEmpty(DirectoryPath) || !Directory.Exists(DirectoryPath)))
            {
                throw new ArgumentException($"'directoryPath' does not exist.");
            }

            IgnorePrefix = UpdateConfigSettingWithAppSettings(ignorePrefixTag) ?? "ignore.";

            // The Core KeyPerFile config provider does not do multi-level.
            // If KeyDelimiter is null, do single-level. Otherwise, multi-level.
            // Empty string will do multi-level with basic non-delimited concatenation in greedy mode.
            // Empty string will be effectively single-level in other modes.
            KeyDelimiter = config[keyDelimiterTag];
        }

        /// <summary>
        /// Retrieves all known key/value pairs from the secrets file where the key begins with with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="prefix">A prefix string to filter the list of potential keys retrieved from the source.</param>
        /// <returns>A collection of key/value pairs.</returns>
        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            ReadAllValues(DirectoryPath, "", _allValues);
            return _allValues?.Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Looks up a single 'value' for the given 'key.'
        /// </summary>
        /// <param name="key">The 'key' to look up in the secrets file. (Prefix handling is not needed here.)</param>
        /// <returns>The value corresponding to the given 'key' or null if no value is found.</returns>
        public override string GetValue(string key)
        {
            if (String.IsNullOrEmpty(KeyDelimiter))
            {
                // Single level lookup
                return ShouldIgnore(key) ? null : ReadValueFromFile(Path.Combine(DirectoryPath, key));
            }

            // In multi-level, we have to break into path parts and search the directory tree to ensure no conflicts.
            string filename = FindValueFileRecursive(DirectoryPath, key);
            return ReadValueFromFile(filename);
        }

        private string FindValueFileRecursive(string dir, string key)
        {
            string filename = null;

            // Is there a matching file in the current directory?
            if (!ShouldIgnore(key))
            {
                string f = Path.Combine(dir, key);
                if (File.Exists(f))
                    filename = f;
            }

            // Can we find a file in some sub-directory that also matches the given key?
            int index = key.IndexOf(KeyDelimiter, 0);
            while (index > 0)   // Yes. '> 0'. We do not want to recurse on ourselves.
            {
                string subdirFile = FindValueFileRecursive(Path.Combine(dir, key.Substring(0, index)), key.Substring(index + KeyDelimiter.Length));
                if (subdirFile != null)
                {
                    if (filename != null)
                        throw new ArgumentException("Duplicate key found.");

                    filename = subdirFile;
                }
                index = key.IndexOf(KeyDelimiter, index + KeyDelimiter.Length);
            }

            return filename;
        }

        private IDictionary<string, string> ReadAllValues(string root, string prefix, IDictionary<string, string> values)
        {
            if (values == null)
                values = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            DirectoryInfo di = new DirectoryInfo(root);

            // Only do multi-level if KeyDelimiter is non-null.
            // When doing multi-level, go depth-first, to give priority to the root level in event of a collision.
            if (KeyDelimiter != null)
            {
                foreach (var sub in di.EnumerateDirectories())
                {
                    if (ShouldIgnore(sub.Name))
                        continue;

                    ReadAllValues(sub.FullName, sub.Name + KeyDelimiter, values);
                }
            }

            foreach (var file in di.EnumerateFiles())
            {
                if (ShouldIgnore(file.Name))
                    continue;

                string key = prefix + file.Name;
                string val = ReadValueFromFile(file.FullName);
                values.Add(key, val);
            }

            return values;
        }

        private string ReadValueFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            string val = File.ReadAllText(filePath);
            while (val.EndsWith(Environment.NewLine))
                val = val.Substring(0, val.Length - Environment.NewLine.Length);

            return val;
        }

        private bool ShouldIgnore(string key)
        {
            if (key == null)
                return true;

            return (!String.IsNullOrWhiteSpace(IgnorePrefix) && key.StartsWith(IgnorePrefix, StringComparison.OrdinalIgnoreCase));
        }
    }
}
