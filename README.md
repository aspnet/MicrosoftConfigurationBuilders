# Configuration Builders

Configuration Builders are a new feature of the full .Net Framework, introduced in .Net 4.7.1. You can read about the concept in [this blog post](http://jeffreyfritz.com/2017/11/modern-configuration-for-asp-net-4-7-1-with-configurationbuilders/).
With this project, Microsoft is providing a basic set of Configuration Builders that should make it easy for developers to get started with the new feature. They
are also intended to address some of the basic needs of applications as they move into a container and cloud focused environment.

### V2 Update:
Version 2 is here with some new features:
  * Azure App Configuration Support - There is a [new builder](#azureappconfigurationbuilder) for drawing values from the new Azure App Configuration service.
  * ConfigBuilder Parameters from AppSettings - This has been one of the most asked for features of these config builders. With V2, it is now possible to
		read initialization parameters for config builders from `appSettings`. Read more about it [here](#appsettings-parameters).
  * Lazy Initialization - As part of the work to enable pulling config parameters from `appSettings`, these key/value configuration builders now support a
		lazy initialization model. Things that must happen immediately can be left in the existing `Initialize(name, config)` method, or builders can leverage
		the `LazyInitialize(name, config)` method for things that can happen just before retrieving values. All builders in this project have been updated to
		be lazy whenever possible.
  * Updateable Keys - Builders can now massage key names before inserting into config. The [AzureKeyVaultConfigBuilder](#azurekeyvaultconfigbuilder) has been
		updated to use this feature to allow embedding 'version' tags in key names instead of applying a single 'version' tag to the builder.  (Note: This is
		seperate from, and performed *after* prefix stripping.)
  * Base Optional Tag - The [optional](#optional) tag that some of the builders in this project employed in V1 has been moved into the base class and is now available
		on all key/value config builders.
  * Section Handlers - This feature allows users to develop extensions that will apply key/value config to sections other than `appSettings` and `connectionStrings`
		if desired. Read more about this feature in the [Section Handlers](#section-handlers) segment below.

## Key/Value Config Builders

If you read the blog post linked above, you probably recognize that Configuration Builders can be quite flexible. Applications can use the Configuration Builder
concept to construct incredibly complex configuration on the fly. But for the most common usage scenarios, a basic key/value replacement mechanism is all that
is needed. Most of the config builders in this project are such key/value builders.

#### mode
The basic concept of these config builders is to draw on an external source of key/value information to populate parts of the config system that are key/value in
nature. By default, the `appSettings` and `connectionStrings` sections receive special treatment from these key/value config builders. These builders can be
set to run in three different modes:
  * `Strict` - This is the default. In this mode, the config builder will only operate on well-known key/value-centric configuration sections. It will enumerate each
		key in the section, and if a matching key is found in the external source, it will replace the value in the resulting config section with the value from the
		external source.
  * `Greedy` - This mode is closely related to `Strict` mode, but instead of being limited to keys that already exist in the original configuration, the config builders
		will dump all key/value pairs from the external source into the resulting config section.
  * `Expand` - This last mode operates on the raw xml before it gets parsed into a config section object. It can be thought of as a basic expansion of tokens in a
		string. Any part of the raw xml string that matches the pattern __`${token}`__ is a candidate for token expansion. If no corresponding value is found in the
		external source, then the token is left alone.

#### prefix
Another feature of these key/value Configuration Builders is prefix handling. Because full-framework .Net configuration is complex and nested, and external key/value
sources are by nature quite basic and flat, leveraging key prefixes can be useful. For example, if you want to inject both App Settings and Connection Strings into
your configuration via environment variables, you could accomplish this in two ways. Use the `EnvironmentConfigBuilder` in the default `Strict` mode and make sure you
have the appropriate key names already coded into your config file. __OR__ you could use two `EnvironmentConfigBuilder`s in `Greedy` mode with distinct prefixes
so they can slurp up any setting or connection string you provide without needing to update the raw config file in advance. Like this:
```xml
<configBuilders>
  <builders>
    <add name="AS_Environment" mode="Greedy" prefix="AppSetting_" type="Microsoft.Configuration.ConfigurationBuilders.EnvironmentConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Environment" />
    <add name="CS_Environment" mode="Greedy" prefix="ConnStr_" type="Microsoft.Configuration.ConfigurationBuilders.EnvironmentConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Environment" />
  </builders>
</configBuilders>

<appSettings configBuilders="AS_Environment" />

<connectionStrings configBuilders="CS_Environment" />
```
This way the same flat key/value source can be used to populate configuration for two different sections.

#### stripPrefix
A related setting that is common among all of these key/value builders is `stripPrefix`. The code above does a good job of separating app settings from connection
strings... but now all the keys in AppSettings start with "AppSetting_". Maybe this is fine for code you wrote. Chances are that prefix is better off stripped from the
key name before being inserted into AppSettings. `stripPrefix` is a boolean value, and accomplishes just that. It's default value is `false`.

#### optional
This setting is a boolean that specified whether to avoid throwing exceptions when the backing configuration source cannot be found or connected.
The default default value is `true`, though some config builders (such as the Azure-based builders) will use a different default.

#### escapeExpandedValues
.Net configuration is XML-based in it's raw form. While these config builders work on `ConfigurationSection` objects in `Strict` and `Greedy` modes,
when operating in `Expand` mode, tokens in the raw XML input are directly replaced with values. Applications that use `Expand` mode may do so because
they need to inject additional XML rather than just a string value. But for the cases when a simple string replacement is the goal, unescaped XML
characters in replacement values may result in invalid XML. In these cases, simply set the `escapeExpandedValues` attribute to `true` and the
config builder will escape special XML characters before replacing tokens in `Expand` mode. The default value is `false`.

#### tokenPattern
This is a setting that is shared between all KeyValueConfigBuilder-derived builders is `tokenPattern`. When describing the `Expand` behavior of these builders
above, it was mentioned that the raw xml is searched for tokens that look like __`${token}`__. This is done with a regular expression. `@"\$\{(\w+)\}"` to be exact.
The set of characters that matches `\w` is more strict than xml and many sources of config values allow, and some applications may need to allow more exotic characters
in their token names. Additionally there might be scenarios where the `${}` pattern is not acceptable.

`tokenPattern` allows developers to change the regex that is used for token matching. It is a string argument, and no validation is done to make sure it is
a well-formed non-dangerous regex - so use it wisely. The only real restriction is that is must contain a capture group. The entire regex must match the entire token,
and the first capture must be the token name to look up in the config source.

#### AppSettings Parameters
Starting with version 2, initialization parameters for key/value config builders can be drawn from `appSettings` instead of being hard-coded in the
config file. This should allow for greater flexibility when deploying solutions with config builders where connection strings need to be kept secure,
or deployment environments are swappable. Eg:

```xml
<configBuilders>
  <builders>
    <add name="Env" type="Microsoft.Configuration.ConfigurationBuilders.EnvironmentConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Environment" />
    <add name="KeyVault" vaultName="${KeyVaultName}" mode="Greedy" prefix="Settings_" type="Microsoft.Configuration.ConfigurationBuilders.AzureKeyVaultConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Azure" />
  </builders>
</configBuilders>

<appSettings configBuilders="Env,KeyVault">
  <add key="KeyVaultName" value="First filled in by 'Env'. Could be a dev, test, staging, or production vault." />
  <!-- Other settings from KeyVault will come in from the 'KeyVaultName' specified in the environment. -->
</appSettings>
```

The way this feature works is that instead of directly reading an initialization parameter, each `KeyValueConfigBuilder` class has the option to run it through
`UpdateConfigSettingWithAppSettings(string)` the first time they read it. If this happens, the parameter value will go through a token-replacement that reads from
`appSettings` and return the updated value. This updated value is also kept in the underlying 'config' dictionary, so subsequent direct reads will get the updated
value as well. (For config builders taking advantage of this feature, note that this method only works *after* base lazy initialization has started. Be sure to call
`base.LazyInitialize()` before using this method.)

Because this feature is somewhat recursive (afterall, it is reading configuration values for builders - that are used to build configuration sections - from a
configuration section) there are limitations when using this feature on the `appSettings` section itself. If a builder is in `Expand` mode *and* processing the
`appSettings` section, then this feature is __disabled__. If a builder is in `Strict` or `Greedy` modes *and* processing the `appSettings` section, then the feature
is enabled - __but__ it can only draw on values that were hard-coded, or inserted into `appSettings` by config builders that execute before it in the builder chain.

Although most initialization parameters can take advantage of this flexibility, it might not always make sense to apply this technique to all parameters. Of the
base parameters defined for all key/value config builders, `mode` and `tokenPrefix` stand out as the two that *do not allow* reading from `appSettings`. *To make
it easier to identify parameters that do allow appSettings substitution, in the definitions below of builders available in this project, such parameter names are
preceded with an '@' symbol.

## Config Builders In This Project

### EnvironmentConfigBuilder
```xml
<add name="Environment"
    [mode|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues|@optional=true]
    type="Microsoft.Configuration.ConfigurationBuilders.EnvironmentConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Environment" />
```
This is the most basic of the config builders. It draws its values from Environment, and it does not have any additional configuration options.
  * __NOTE:__ In a Windows container environment, variables set at run time are only injected into the EntryPoint process environment. 
  Applications that run as a service or a non-EntryPoint process will not pick up these variables unless they are otherwise injected through
  some mechanism in the container. For [IIS](https://github.com/Microsoft/iis-docker/pull/41)/[ASP.Net](https://github.com/Microsoft/aspnet-docker)-based
  containers, the current version of [ServiceMonitor.exe](https://github.com/Microsoft/iis-docker/pull/41) handles this in the *DefaultAppPool*
  only. Other Windows-based container variants may need to develop their own injection mechanism for non-EntryPoint processes.

### UserSecretsConfigBuilder
```xml
<add name="UserSecrets"
    [mode|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues|@optional=true]
    (@userSecretsId="12345678-90AB-CDEF-1234-567890" | @userSecretsFile="~\secrets.file")
    type="Microsoft.Configuration.ConfigurationBuilders.UserSecretsConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.UserSecrets" />
```
To enable a feature similar to .Net Core's user secrets you can use this config builder. Microsoft is adding better secrets management in future releases
of Visual Studio, and this config builder will be a part of that plan. Web Applications are the initial target for this work in Visual Studio, but this
configuration builder can be used in any full-framework project if you specify your own secrets file. (Or define the 'UserSecretsId' property in your
project file and create the raw secrets file in the correct location for reading.) In order to keep external dependencies out of the picture, the
actual secret file will be xml formatted - though this should be considered an implementation detail, and the format should not be relied upon.
(If you need to share a secrets.json file with Core projects, you could consider using the `SimpleJsonConfigBuilder` below... but as with this
builder, the json format for Core secrets is technically an implementation detail subject to change as well.)

There are three additional configuration attributes for this config builder:
  * `userSecretsId` - This is the preferred method for identifying an xml secrets file. It works similar to .Net Core, which uses a 'UserSecretsId' project
  property to store this identifier. (The string does not have to be a Guid. Just unique. The VS "Manage User Secrets" experience produces a Guid.) With this
  attribute, the `UserSecretsConfigBuilder` will look in a well-known local location (%APPDATA%\Microsoft\UserSecrets\&lt;userSecretsId&gt;\secrets.xml in
  Windows environments) for a secrets file belonging to this identifier.
  * `userSecretsFile` - An optional attribute specifying the file containing the secrets. The '~' character can be used at the start to reference the app root.
  One of this attribute or the 'userSecretsId' attribute is required. If both are specified, 'userSecretsFile' takes precedence.

The next Visual Studio update will include "Manage User Secrets..." support for WebForms projects. When using this feature, Visual Studio will create an
empty secrets file outside of the solution folder and allow editing the raw content to add/remove secrets. This is similar to the .Net Core experience,
and currently exposes the format of the file which, as mentioned above, should be considered an implementation detail. A non-empty secrets file would look like this:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<root>
  <secrets ver="1.0">
    <secret name="secretFoo" value="valueBar" />
  </secrets>
</root>
```

### AzureAppConfigurationBuilder
```xml
<add name="AzureAppConfig"
    [mode|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues|@optional=false]
    (@endpoint="https://your-appconfig-store.azconfig.io" | @connectionString="Endpoint=https://your-appconfig-store.azconfig.io;Id=XXXXXXXXXX;Secret=XXXXXXXXXX")
    [@keyFilter="string"]
    [@labelFilter="label"]
    [@preferredDateTime="DateTimeOffset"]
    type="Microsoft.Configuration.ConfigurationBuilders.AzureAppConfigurationBuilder, Microsoft.Configuration.ConfigurationBuilders.AzureAppConfig" />
```
[AppConfiguration](https://docs.microsoft.com/en-us/azure/azure-app-configuration/overview) is a new offering from Azure, currently in preview. If you
wish to use this new service for managing your configuration, then use this AzureAppConfigurationBuilder. Either `endpoint` or `connectionString` are
required, but all other attributes are optional. If both `endpoint` and `connectionString` are used, then preference is given to the connection string.
It is however, __strongly__ encouraged to use `endpoint` with a managed service identity in Azure.
  * `endpoint` - This specifies the AppConfiguration store to connect to.
  * `connectionString` - This specifies the AppConfiguration store to connect to, along with the Id and Secret necessary to access the service.
  * `keyFilter` - Use this to select a set of configuration values matching a certain key pattern.
  * `labelFilter` - Only retrieve configuration values that match a certain label.
  * `preferredDateTime` - Instead of versioning ala Azure Key Vault, AppConfiguration uses timestamps. Use this attribute to go back in time
  to retrieve configuration values from a past state.

### AzureKeyVaultConfigBuilder
```xml
<add name="AzureKeyVault"
    [mode|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues|@optional=false]
    (@vaultName="MyVaultName" | @uri="https://MyVaultName.vault.azure.net")
    [@connectionString="connection string"]
    [@version="secrets version"]
    [@preloadSecretNames="true"]
    type="Microsoft.Configuration.ConfigurationBuilders.AzureKeyVaultConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Azure" />
```
If your secrets are kept in Azure Key Vault, then this config builder is for you. There are three additional attributes for this config builder. The `vaultName` is
required. The other attributes allow you some manual control about which vault to connect to, but are only necessary if the application is not running in an
environment that works magically with `Microsoft.Azure.Services.AppAuthentication`. The Azure Services Authentication library is used to automatically pick
up connection information from the execution environment if possible, but you can override that feature by providing a connection string instead.
  * `vaultName` - This is a required attribute. It specifies the name of the vault in your Azure subscription from which to read key/value pairs.
  * `connectionString` - A connection string usable by [AzureServiceTokenProvider](https://docs.microsoft.com/en-us/azure/key-vault/service-to-service-authentication#connection-string-support)
  * `uri` - Connect to other Key Vault providers with this attribute. If not specified, Azure is the assumed Vault provider. If the uri _is_specified, then `vaultName` is no longer a required parameter.
  * `version` - Azure Key Vault provides a versioning feature for secrets. If this is specified, the builder will only retrieve secrets matching this version.
  * `preloadSecretNames` - By default, this builder will query __all__ the key names in the key vault when it is initialized. If this is a concern, set
  this attribute to 'false', and secrets will be retrieved one at a time. This could also be useful if the vault allows "Get" access but not
  "List" access. (NOTE: Disabling preload is incompatible with Greedy mode.)
Tip: Azure Key Vault uses random per-secret Guid assignments for versioning, which makes specifying a secret `version` tag on this builder rather
limiting, as it will only ever update one config value. To make version handling more useful, V2 of this builder takes advantage of the new key-updating
feature to allow users to specify version tags in key names rather than on the config builder declaration. That way, the same builder can handle multiple
keys with specific versions instead of needing to redefine multiple builders.
When requesting a specific version for a particular key, the key name in the original config file should look like __`keyName/versionId`__. The
AzureKeyVaultConfigBuilder will only substitue values for 'keyName' if the specified 'versionId' exists in the vault. When that happens, the
AzureKeyVaultConfigBuilder will remove the `/versionId` from the original key, and the resulting config section will only contain `keyName`.
For example:
```xml
<appSettings configBuilders="AzureKeyVault">
  <add key="item1" value="Replaced with latest value from the key vault." />
  <add key="item2/0123456789abcdefdeadbeefbadf00d" value="Replaced with specific version only." />
</appSettings>
```
Assuming both of these items exist in the vault, and the version tag for `item2` is valid, this would result in an collection of appSettings with two
entries: `item1` and `item2`.

### KeyPerFileConfigBuilder
```xml
<add name="KeyPerFile"
    [mode|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues|@optional=false]
	(@directoryPath="PathToSourceDirectory")
    [@ignorePrefix="ignore."]
    [keyDelimiter=":"]
    type="Microsoft.Configuration.ConfigurationBuilders.KeyPerFileConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.KeyPerFile" />
```
This config builder uses a directory's files as a source of values. A file's name is the key, and the contents are the value. This
config builder can be useful when running in an orchestrated container environment, as systems like Docker Swarm and Kubernetes provide 'secrets' to
their orchestrated windows containers in this key-per-file manner.
  * `directoryPath` - This is a required attribute. It specifies a path to the source directory to look in for values. Docker for Windows secrets
  are stored in the 'C:\ProgramData\Docker\secrets' directory by default.
  * `ignorePrefix` - Files that start with this prefix will be excluded. Defaults to "ignore.".
  * `keyDelimiter` - If specified, the config builder will traverse multiple levels of the directory, building key names up with this delimeter. If
  this value is left `null` however, the config builder only looks at the top-level of the directory. `null` is the default.

### SimpleJsonConfigBuilder
```xml
<add name="SimpleJson"
    [mode|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues|@optional=true]
    @jsonFile="~\config.json"
    [@jsonMode="(Flat|Sectional)"]
    type="Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json" />
```
Because .Net Core projects can rely heavily on json files for configuration, it makes some sense to allow those same files to be used in full-framework
configuration as well. You can imagine that the heirarchical nature of json might enable some fantastic capabilities for building complex configuration sections.
But this config builders is meant to be a simple mapping from a flat key/value source into specific key/value areas of full-framework configuration. Thus its name
begins with 'Simple.' Think of the backing json file as a basic dictionary, rather than a comlex heirarchical object.

(A multi-level heirarchical file can be used. This provider will simply 'flatten' the depth by appending the property name at each level using ':' as a delimiter.)

There are three additional attributes that can be used to configure this builder:
  * `jsonFile` - A required attribute specifying the json file to draw from. The '~' character can be used at the start to reference the app root.
  * `jsonMode` - `[Flat|Sectional]`. 'Flat' is the default.
    - This attribute requires a little more explanation. It says above to think of the json file as a single flat key/value source. This is the usual that applies to other key/value config builders like `EnvironmentConfigBuilder` and `AzureKeyVaultConfigBuilder` because those sources provide no other option. If the `SimpleJsonConfigBuilder` is configured in 'Sectional' mode, then the json file is conceptually divided just at the top level into multiple simple dictionaries. Each one of those dictionaries will only be applied to the config section that matches the top-level property name attached to them. For example:
```json
    {
        "appSettings" : {
            "setting1" : "value1",
            "setting2" : "value2",
            "complex" : {
                "setting1" : "complex:value1",
                "setting2" : "complex:value2",
            }
        },

        "connectionStrings" : {
            "mySpecialConnectionString" : "Dont_check_connection_information_into_source_control"
        }
    }
```

## Section Handlers
By default, `KeyValueConfigBuilder`s can only be applied to `<appSettings>` and `<connectionStrings>` in non-Expand modes. If an application has
a need to apply them to other section types however - and `Expand` mode is not suitable - then developers can write a new `SectionHandler<T>` that
will allow any `KeyValueConfigBuilder` to operate specifically on a section of type `T`. There are only two required methods:
```CSharp
public class MySpecialSectionHandler : SectionHandler<MySpecialSection>
{
    // T ConfigSection;
    // public override void Initialize(string name, NameValueCollection config) {}

    public override IEnumerator<KeyValuePair<string, object>> GetEnumerator() {}

    public override void InsertOrUpdate(string newKey, string newValue, string oldKey = null, object oldItem = null) {}
}
```
Keep in mind when implementing a section handler, that `InsertOrUpdate()` will be called while iterating over the enumerator
supplied by `GetEnumerator()`. So the two methods must work in cooperation to make sure that the enumerator does not get confused
while iterating.

A section handler is free to interpret the structure of a `ConfigurationSection` in any way it sees fit, so long it can be exposed as an
enumerable list of key/value things. The 'value' side of that pair doesn't even have to be a string. Consider the implementation of
`ConnectionStringsSectionHandler` - it uses `ConnectionStringSettings` objects as the value of the pair, so in 'InsertOrUpdate()' it
can simply update the old connection string (thereby preserving other existing properties like 'ProviderName') instead of creating a
new one from scratch.

New section handlers can be introduced to the config system... via config. Section handlers do follow along with the old 
provider model introduced in .Net 2.0, so they require `name` and `type` attributes, but can additionally support any other
attribute needed by passing them in a `NameValueCollection` to the `Initialize(name, config)` method.


The `AppSettingsSectionHandler` and `ConnectionStringsSectionHandler`
are implicitly added at the root level config, but they can be clear/removed just like any other item in an add/remove/clear configuration
collection. As an example, here is what their explicit declaration would look like:
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
When adding additional handlers, the name of this section must be 'Microsoft.Configuration.ConfigurationBuilders.SectionHandlers' so key/value config builders can find it.
Also note that a more qualified type will be required so the runtime can determine which assembly contains the new handler type. When working
with ASP.Net applications, it is hit and miss regarding whether its able to define new section handlers in `App_Code` or not. Some configuration
sections (such as `appSettings`) get loaded by ASP.Net before `App_Code` is compiled, so handlers for those sections will need to be
compiled in a separate assembly in the 'bin' directory. For example:
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

## Implementing More Key/Value Config Builders

If you don't see a config builder here that suits your needs, you can write your own. Referencing the `Basic` nuget package for this project will get you the base upon which
all of these builders inherit. Most of the heavy-ish lifting and consistent behavior across key/value config builders comes from this base. Take a look at the code for more
detail, but in many cases implementing a custom key/value config builder in this same vein is as easy as inheriting the base, and implementing two basic methods.
```CSharp
using Microsoft.Configuration.ConfigurationBuilders;

public class CustomConfigBuilder : KeyValueConfigBuilder
{
    public override string GetValue(string key)
    {
        // Key lookup should be case-insensitive, because most key/value collections in .Net config sections are as well.
        return "Value for given key, or null.";
    }

    public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
    {
        // Populate the return collection a little more smartly. ;)
        return new Dictionary<string, string>() { { "one", "1" }, { "two", "2" } };
    }
}
```

Additionally, there are a few virtual methods that you can take advantage of for more advanced techniques.
```CSharp
public class CustomConfigBuilder : KeyValueConfigBuilder
{
        public override void Initialize(string name, NameValueCollection config)
        {
            // Use this initializer for things that must be read from 'config' immediately upon creation.
            // AppSettings parameter substitution is not available at this point.
            // Try using LazyInitialize(string, NameValueCollection) instead when possible.
        }

        protected override void LazyInitialize(string name, NameValueCollection config)
        {
            // Use this for things that don't need to be initialized until just before
            // config values are retrieved.
            // Be sure to begin with 'base.LazyInitialize(name, config)'. AppSettings
            // parameter substitution via 'UpdateConfigSettingWithAppSettings(parameterName)'
            // will be available after that call.
        }

        public override bool MapKey(string key)
        {
            // If you know the format of the key pulled from *.config files is going to be invalid, but you
            // are able to translate the bad format to a known good format to get a value from your
            // config source, use this method.
            // Ex) AppSettings are commonly named things like "area:feature", but the ':' is not a legal
            //    character for key names in Azure Key Vault. MapKey() can help translate the ':' to a
            //    '-' in this case, which will allow the ability to look up a config value for this appSetting
            //    in Key Vault, even though it's original key name is not valid in Key Vault.
        }

        public override bool ValidateKey(string key)
        {
            // A no-op by default. If your backing storage cannot handle certain characters, this is a one-stop
            // place for screening key names. It is particularly helpful in `Strict` and `Expand` modes where
            // key names for lookup are taken from *.config files and could potentially contain invalid
            // characters that cause exceptions in the backing config store.
        }

        public override string UpdateKey(string rawKey)
        {
            // Just before replacing retrieved values in a config section, this method gets called.
            // 'AzureKeyVaultConfigBuilder' uses this override to strip version tags from keys.
        }
}
```

## How to contribute

Information on contributing to this repo is in the [Contributing Guide](CONTRIBUTING.md).

## Blog Posts
[Announcing .NET 4.7.1 Tools for the Cloud](https://blogs.msdn.microsoft.com/webdev/2017/11/17/announcing-net-4-7-1-tools-for-the-cloud/)  
[.Net Framework 4.7.1 ASP.NET and Configuration features](https://blogs.msdn.microsoft.com/dotnet/2017/09/13/net-framework-4-7-1-asp-net-and-configuration-features/)  
[Modern Configuration for ASP.NET 4.7.1 with ConfigurationBuilders](http://jeffreyfritz.com/2017/11/modern-configuration-for-asp-net-4-7-1-with-configurationbuilders/)  
[Service-to-service authentication to Azure Key Vault using .NET](https://docs.microsoft.com/en-us/azure/key-vault/service-to-service-authentication#connection-string-support)  
