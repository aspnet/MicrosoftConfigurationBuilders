using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Configuration.ConfigurationBuilders;

namespace SamplesLib
{
    public class ExpandWrapper<T> : ConfigurationBuilder where T : KeyValueConfigBuilder, new()
    {
        private static MethodInfo _expandTokensMethod = typeof(KeyValueConfigBuilder).GetMethod("ExpandTokens", BindingFlags.NonPublic | BindingFlags.Instance);
        private T _underlyingBuilder;

        public ExpandWrapper() { _underlyingBuilder = new T(); }

        public override void Initialize(string name, NameValueCollection config) => _underlyingBuilder.Initialize(name, config);

        public override XmlNode ProcessRawXml(XmlNode rawXml)
        {
            rawXml = _underlyingBuilder.ProcessRawXml(rawXml);

            // !!!DO NOT APPLY TO APPSETTINGS!!!
            // AppSettings is special because it can be implicitly referenced when looking for config builder
            // settings, while it can simultaneously be processed by config builders. There used to be special
            // logic to help manage potential recursion in the base KeyValueConfigBuilder class, but that
            // protection is no more since 'Expand' mode and the use of _both_ ProcessRawXml() and ProcessConfigSection()
            // have been removed.
            if (rawXml.Name != "appSettings")    // System.Configuration hard codes this, so we might as well too.
            {
                // Checking Enabled will kick off LazyInit, so only do that if we are actually going to do work here.
                if (_underlyingBuilder.Mode == KeyValueMode.Token && _underlyingBuilder.Enabled != KeyValueEnabled.Disabled)
                {
                    // Old Expand-mode would do a recursion check here. We don't have internal access to RecursionGuard.
                    return ExpandTokens(rawXml);
                }
            }

            return rawXml;
        }

        public override ConfigurationSection ProcessConfigurationSection(ConfigurationSection configSection)
        {
            // We have overridden the meaning of "Token" mode for this class. Don't do any processing in that mode.
            if (_underlyingBuilder.Mode == KeyValueMode.Token)
                return configSection;

            return _underlyingBuilder.ProcessConfigurationSection(configSection);
        }

        private XmlNode ExpandTokens(XmlNode rawXml)
        {
            string rawXmlString = rawXml.OuterXml;
            if (String.IsNullOrEmpty(rawXmlString))
                return rawXml;

            string updatedXmlString = (string)_expandTokensMethod.Invoke(_underlyingBuilder, new object[] { rawXmlString });

            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.LoadXml(updatedXmlString);
            return doc.DocumentElement;
        }
    }
}
