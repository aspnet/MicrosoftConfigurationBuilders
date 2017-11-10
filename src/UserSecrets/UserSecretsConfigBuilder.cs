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
        public const string ignoreMissingFileTag = "ignoreMissingFile";

        private ConcurrentDictionary<string, string> _secrets;

        public string UserSecretsId { get; protected set; }
        public string UserSecretsFile { get; protected set; }
        public bool IgnoreMissingFile { get; protected set; }

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);

            bool ignoreMissing;
            IgnoreMissingFile = (Boolean.TryParse(config?[ignoreMissingFileTag], out ignoreMissing)) ? ignoreMissing : true;

            // Explicit file reference takes precedence over an identifier.
            string secretsFile = config?[userSecretsFileTag];
            if (String.IsNullOrWhiteSpace(secretsFile))
            {
                string secretsId = config?[userSecretsIdTag];
                if (String.IsNullOrWhiteSpace(secretsId))
                {
                    throw new ArgumentException($"UserSecretsConfigBuilder '{name}': Secrets file must be specified with either the '{userSecretsFileTag}' or the '{userSecretsIdTag}' attribute.");
                }
                secretsFile = GetSecretsFileFromId(secretsId);
            }

            UserSecretsFile = Utils.MapPath(secretsFile);
            if (File.Exists(UserSecretsFile))
            {
                ReadUserSecrets(UserSecretsFile);
            }
            else if (!IgnoreMissingFile)
            {
                throw new ArgumentException($"UserSecretsConfigBuilder '{name}': Secrets file does not exist.");
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
            // Make sure the identifier is legal for file paths.
            int badCharIndex = secretsId.IndexOfAny(Path.GetInvalidFileNameChars());
            if (badCharIndex != -1)
            {
                throw new InvalidOperationException($"UserSecretsConfigBuilder '{Name}': Invalid character '{secretsId[badCharIndex]}' in '{userSecretsIdTag}'.");
            }

            string root = Environment.GetEnvironmentVariable("APPDATA") ?? Environment.GetEnvironmentVariable("HOME");
            
            if (!String.IsNullOrWhiteSpace(root))
                return Path.Combine(root, "Microsoft", "UserSecrets", secretsId, "secrets.xml");

            return Path.Combine(Utils.MapPath(@"~\App_Data"), "UserSecrets", secretsId, "secrets.xml");
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
