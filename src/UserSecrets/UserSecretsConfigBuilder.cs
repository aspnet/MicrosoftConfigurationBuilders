// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    public class UserSecretsConfigBuilder : KeyValueConfigBuilder
    {
        public const string userSecretsFileTag = "userSecretsFile";
        public const string userSecretsIdTag = "userSecretsId";
        public const string optionalTag = "optional";

        private ConcurrentDictionary<string, string> _secrets;

        public string UserSecretsId { get; protected set; }
        public string UserSecretsFile { get; protected set; }
        public bool Optional { get; protected set; }

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);

            bool optional;
            Optional = (Boolean.TryParse(config?[optionalTag], out optional)) ? optional : true;

            // Explicit file reference takes precedence over an identifier.
            string secretsFile = config?[userSecretsFileTag];
            if (String.IsNullOrWhiteSpace(secretsFile))
            {
                string secretsId = config?[userSecretsIdTag];
                secretsFile = GetSecretsFileFromId(secretsId);
            }

            UserSecretsFile = Utils.MapPath(secretsFile);
            if (File.Exists(UserSecretsFile))
            {
                ReadUserSecrets(UserSecretsFile);
            }
            else if (!Optional)
            {
                throw new ArgumentException($"UserSecretsConfigBuilder '{name}': Secrets file does not exist.");
            }
            else
            {
                // If the file was optional and not found, create an empty collection to effectively no-op GetValue.
                _secrets = new ConcurrentDictionary<string, string>();
            }
        }

        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            return _secrets?.Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public override string GetValue(string key)
        {
            if (_secrets != null && _secrets.TryGetValue(key, out string val))
                return val;

            return null;
        }

        // This method is based heavily on .Net Core's Config.UserSecrets project in an attempt to keep similar conventions. 
        private string GetSecretsFileFromId(string secretsId)
        {
            // The common VS scenario will leave this Id attribute empty, or as a place-holding token. In that case,
            // go look up the user secrets id from the magic file.
            if (String.IsNullOrWhiteSpace(secretsId) || secretsId.Equals("${UserSecretsId}", StringComparison.InvariantCultureIgnoreCase))
            {
                // The magic file should be deployed in our codebase
                string codebase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                string localpath = new Uri(codebase).LocalPath;
                string magicId = File.ReadAllText(localpath + ".UserSecretsId.txt");

                if (!String.IsNullOrWhiteSpace(magicId))
                {
                    secretsId = magicId.Trim();
                }
            }

            // Make sure the identifier is legal for file paths.
            int badCharIndex = secretsId.IndexOfAny(Path.GetInvalidFileNameChars());
            if (badCharIndex != -1)
            {
                throw new InvalidOperationException($"UserSecretsConfigBuilder '{Name}': Invalid character '{secretsId[badCharIndex]}' in '{userSecretsIdTag}'.");
            }

            // Try Windows-style first
            string root = Environment.GetEnvironmentVariable("APPDATA");
            if (!String.IsNullOrWhiteSpace(root))
                return Path.Combine(root, "Microsoft", "UserSecrets", secretsId, "secrets.xml");

            // Then try unix-style
            root = Environment.GetEnvironmentVariable("HOME");
            if (!String.IsNullOrWhiteSpace(root))
                return Path.Combine(root, ".microsoft", "usersecrets", secretsId, "secrets.xml");

            return null;
        }

        // This is an implementation detail and subject to change - but the secrets file is xml-based and fits this format:
        //
        //  <root>
        //      <secrets ver="1.0">
        //          <secret name="secret1" value="foo" />
        //          <secret name="secret2" value="foo" />
        //      </secrets>
        //  </root>
        //
        // Of course, this is always subject to change. We don't currently look at the version obviously, but that might come
        // in handy for schema changes in the future.
        private void ReadUserSecrets(string secretsFile)
        {
            XDocument xdoc = XDocument.Load(secretsFile);
            XElement xmlSecrets = xdoc.Descendants("secrets").First();
            ConcurrentDictionary<string, string> secrets = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (XElement e in xmlSecrets.Descendants("secret"))
            {
                secrets[(string)e.Attribute("name")] = (string)e.Attribute("value");
            }

            _secrets = secrets;
        }
    }
}
