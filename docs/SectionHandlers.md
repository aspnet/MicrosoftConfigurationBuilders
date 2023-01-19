# Section Handlers
*KeyValueConfigBuilders* are designed to operate on any config section that has a basic key/value structure. If that statement sounds somewhat vague, it probably because it is. These builders have been intentionally designed around a key/value-lookup paradigm. So, as the old saying goes, when all you have is a hammer, everything looks like a nail.

Magically, the `<appSettings>` and `<connectionStrings>` look like key/value config sections, so these builders can all be applied there. Of course it's not really magic. Those are the two most commonly used and obvious key/value-like config sections out there. So we have wired them up to work right out of the box. But developers can wire up additional sections by registering a new `SectionHandler<T>` for each type of section they want to process with a `KeyValueConfigBuilder`.

A `SectionHandler<T>` is the interface that these config builders use to interact with different configuration sections. That way builders only need to know how to work with `SectionHandler<T>` and not have custom code to handle each different type of config section it might be applied to. To implement a new section handler, there are only two required methods\*:
```CSharp
public class MySpecialSectionHandler : SectionHandler<MySpecialSection>
{
    // T ConfigSection;
    // public override void Initialize(string name, NameValueCollection config) {}
    // public override string TryGetOriginalCase(string requestedKey) {}

    public override IEnumerable<Tuple<string, string, object>> KeysValuesAndState() { /* key, value, state */ }

    public override void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null) {}

    // * - Version 2 used this method instead of the richer 'KeysValuesAndState()' method.
    //      Section handlers that were created by overriding this method will still work in V3,
    //      but without the additional sting 'value' in the enumerated tuple, builders will be unable
    //      to properly process sections in 'Token' mode since they will only see tokens that appear
    //      in config keys and not tokens that appear in value side of the config entry.
    //public override IEnumerator<KeyValuePair<string, object>> GetEnumerator() { /* key, state */ }
}
```
Keep in mind when implementing a section handler, that `InsertOrUpdate()` will be called while iterating over the enumerator supplied by `GetEnumerator()`. So the two methods must work in cooperation to make sure that the enumerator does not get confused while iterating. Ie, don't tamper with the collection that the enumerator is iterating over. Make a copy of the collection first and iterate over that if necessary.

A section handler is free to interpret the structure of a `ConfigurationSection` in any way it sees fit, so long it can be exposed as an enumerable list of key/value things. It can even handle situations where the each configuration record contains more than just a single key/value pair. Consider the implementation of `ConnectionStringsSectionHandler` - it uses the `ConnectionStringSettings` instance itself as the "state" of the tuple, so in 'InsertOrUpdate()' it can simply update the old connection string instance, (thereby preserving other existing properties like 'ProviderName') instead of creating a new one from scratch.

New section handlers are be introduced to the config system the same way config builders are... via config. Section handlers follow along with the old provider model introduced in .Net 2.0, so they require `name` and `type` attributes, but can additionally support any other attribute needed by passing them in a `NameValueCollection` to the `Initialize(name, config)` method.

Section handlers also have an optional virtual method `TryGetOriginalCase(string)` that attempts to preserve casing in config files when executing in `Greedy` mode. When operating in `Strict` mode, config builders in this repo have always preserved the case of the key when updating the value. This was easy because the original key was already at hand during lookup and replacement. In `Greedy` mode however, the original key from the config file was not used since it was not needed for a one-item lookup. Thus any greedy substitutions had their keys replaced with the keys from the external config source. Functionally they should be the same, since key/value config is supposed to be case-insensitive. But aesthetically, being a good citizen and not replacing the original key case is good.

The `AppSettingsSectionHandler` and `ConnectionStringsSectionHandler` are implicitly added at the root level config, but they can be clear/removed just like any other item in an add/remove/clear configuration collection. As an example, here is what their explicit declaration would look like:
```xml
<configSections>
  <section name="Microsoft.Configuration.ConfigurationBuilders.SectionHandlers" type="Microsoft.Configuration.ConfigurationBuilders.SectionHandlersSection, Microsoft.Configuration.ConfigurationBuilders.Base" restartOnExternalChanges="false" requirePermission="false" />
</configSections>

<Microsoft.Configuration.ConfigurationBuilders.SectionHandlers>
  <handlers>
    <add name="DefaultAppSettingsHandler" type="Microsoft.Configuration.ConfigurationBuilders.AppSettingsSectionHandler" />
    <add name="DefaultConnectionStringsHandler" type="Microsoft.Configuration.ConfigurationBuilders.ConnectionStringsSectionHandler" />
  </handlers>
</Microsoft.Configuration.ConfigurationBuilders.SectionHandlers>
```
When adding additional handlers, the name of this section must be 'Microsoft.Configuration.ConfigurationBuilders.SectionHandlers' so key/value config builders can find it. Also note that a more qualified type name may be required so the runtime can determine which assembly contains the new handler type. When working with ASP.Net applications, it is hit and miss regarding whether its possible to define new section handlers in `App_Code` or not. Some configuration sections (such as `appSettings`) get loaded by ASP.Net before `App_Code` is compiled, so handlers for those sections will need to be compiled in a separate assembly in the 'bin' directory. For example:
```xml
<configSections>
  <section name="Microsoft.Configuration.ConfigurationBuilders.SectionHandlers" type="Microsoft.Configuration.ConfigurationBuilders.SectionHandlersSection, Microsoft.Configuration.ConfigurationBuilders.Base" restartOnExternalChanges="false" requirePermission="false" />
</configSections>

<Microsoft.Configuration.ConfigurationBuilders.SectionHandlers>
  <handlers>
    <remove name="DefaultAppSettingsHandler" />
    <add name="DefaultAppSettingsHandler" customAttr="foo" type="MyCustomAppSettingsSectionHandler, RefAssemblyInBin" />
    <remove name="DefaultConnectionStringsHandler" />
    <add name="DefaultConnectionStringsHandler" superCustomAttr="bar" superDuperCustomAttr="42" type="MyCustomConnectionStringsSectionHandler, App_Code" />
  </handlers>
</Microsoft.Configuration.ConfigurationBuilders.SectionHandlers>
```

## ConnectionStringsSectionHandler2
Version 3 of this package suite includes a new section handler for the `<connectionStrings>` section. For maximum compatibility, the old section handler is still used by default. Here we will explain how to use this new section handler and how to replace the old one.

The new `ConnectionStringsSectionHandler2` works pretty much like it's non-numbered predecessor. The main difference is, when it enumerates through the list of connection strings in the section, it will ask builders to look for values that match "&lt;name&gt;:connectionString" and "&lt;name&gt;:providerName" in addition to just "&lt;name&gt;". When updating or inserting new `ConnectionStringSettings` items into the configuration collection, values that come from tagged keys will go to the appropriate attribute. Values that are found without a tagged key update the 'connectionString' property as they did before.

```xml
    <connectionStrings configBuilders="StrictBuilder">
        <add name="strict-cs" connectionString="Will get the value of 'strict-cs' or 'strict-cs:connectionString'"
                              providerName="Will only get the value of 'strict-cs:providerName'" />

        <!-- Easy to imagine pulling these from a structured json file. -->
        <add name="token-cs1" connectionString="${tokenCS:connectionString}"
                              providerName="${tokenCS:providerName}" />
        <!-- But token mode can be messy. -->
        <add name="token-cs2" connectionString="${token-names-not-important}"
                              providerName="${they-can-even-be-tagged-wrong:connectionString}" />
    </connectionStrings>
```

One thing to note is that the mechanism for associating a key/value lookup with a specific attribute of connection string entries is a simple post-fix to the key. This makes this feature incompatible with versioned keys in Azure Key Vault. Most other cases should just work as they did before, with a little extra magic cleanliness when you start using this feature.

An example of this new behavior from `ConnectionStringsSectionHandler2` can be seen in the [SampleConsoleApp](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/1bdc2388f139c046e1c58bcc147c875d5c918785/samples/SampleConsoleApp/App.config#L37-L46). Or in the [test project](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/1bdc2388f139c046e1c58bcc147c875d5c918785/test/Microsoft.Configuration.ConfigurationBuilders.Test/ConnectionStringsSectionHandler2Tests.cs). The [SampleWebApp](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/1bdc2388f139c046e1c58bcc147c875d5c918785/samples/SampleWebApp/Web.config#L38-L41) does not use the new section handler, but it does include some notes about how it's resulting connection string collection would look different if it did.
