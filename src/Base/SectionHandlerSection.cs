// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Configuration;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// Provides programmatic access to the 'sectionHandlers' config section. This class can't be inherited.
    /// </summary>
    public sealed class SectionHandlersSection : ConfigurationSection
    {
        static readonly string handlerSectionName = "Microsoft.Configuration.ConfigurationBuilders.SectionHandlers";

        /// <summary>
        /// Gets the collection of <see cref="SectionHandler{T}" />s defined for processing config sections with <see cref="KeyValueConfigBuilder"/>s./>
        /// </summary>
        [ConfigurationProperty("handlers", IsDefaultCollection = true, Options = ConfigurationPropertyOptions.IsDefaultCollection)]
        public ProviderSettingsCollection Handlers
        {
            get { return (ProviderSettingsCollection)base["handlers"]; }
        }

        /// <summary>
        /// Used to initialize a default set of section handlers. (For the appSettings and connectionStrings sections.)
        /// </summary>
        protected override void InitializeDefault()
        {
            // This only runs once at the top "parent" level of the config stack. If there is already an
            // existing parent in the stack to inherit, then this does not get called.
            base.InitializeDefault();
            if (Handlers != null)
            {
                Handlers.Add(new ProviderSettings("DefaultAppSettingsHandler", "Microsoft.Configuration.ConfigurationBuilders.AppSettingsSectionHandler"));
                Handlers.Add(new ProviderSettings("DefaultConnectionStringsHandler", "Microsoft.Configuration.ConfigurationBuilders.ConnectionStringsSectionHandler"));
            }
        }

        static internal ISectionHandler GetSectionHandler<T>(T configSection) where T : ConfigurationSection
        {
            if (configSection == null)
                return null;

            SectionHandlersSection handlerSection = ConfigurationManager.GetSection(handlerSectionName) as SectionHandlersSection;

            if (handlerSection == null)
            {
                handlerSection = new SectionHandlersSection();
                handlerSection.InitializeDefault();
            }

            // Look at each handler to see if it works on this section. Reverse order so last match wins.
            // .IsSubclassOf() requires an exact type match. So SectionHandler<BaseConfigSectionType> won't work.
            Type sectionHandlerGenericTemplate = typeof(SectionHandler<>);
            Type sectionHandlerDesiredType = sectionHandlerGenericTemplate.MakeGenericType(configSection.GetType());
            for (int i = handlerSection.Handlers.Count; i-- > 0; )
            {
                Type handlerType = Type.GetType(handlerSection.Handlers[i].Type);
                if (handlerType != null && handlerType.IsSubclassOf(sectionHandlerDesiredType))
                {
                    ISectionHandler handler = Activator.CreateInstance(handlerType) as ISectionHandler;
                    if (handler != null)
                    {
                        sectionHandlerDesiredType.GetProperty("ConfigSection").SetValue(handler, configSection);
                    }
                    return handler;
                }
            }

            return null;
        }
    }
}
