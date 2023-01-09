# KeyValueConfigBuilders

<a name="intro"></a>
## Introduction to Key/Value Config Builders

Introduced in .Net 4.7.1, `ConfigurationBuilders` is a feature that can be quite flexible. Applications can use the Configuration Builder
concept to construct incredibly complex configuration on the fly. But for the most common usage scenarios, a basic key/value replacement mechanism is all that
is needed. The config builders in this project are designed to be such simple key/value builders.

#### mode
The basic concept of these config builders is to draw on an external source of key/value information to populate parts of the config system that are key/value in
nature. By default, the `appSettings` and `connectionStrings` sections receive special treatment from these key/value config builders. These builders can be
set to run in three different modes:
  * `Strict` - This is the default. In this mode, the config builder will only operate on well-known key/value-centric configuration sections. It will enumerate each
		key in the section, and if a matching key is found in the external source, it will replace the value in the resulting config section with the value from the
		external source.
  * `Greedy` - This mode is closely related to `Strict` mode, but instead of being limited to keys that already exist in the original configuration, the config builders
		will dump all key/value pairs from the external source into the resulting config section.
  * `Token` - This last mode operates on both the key and the value read from a config section. It can be thought of as a basic expansion of tokens in a
		string. Any part of the key or value string that matches the pattern __`${token}`__ is a candidate for token expansion. If no corresponding value is found in the
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

#### enabled
This is a setting that controls whether a config builder gets executed or not, and if it fails silently or not. Supported values are
`enabled`, `disabled`, and `optional`. (As well as `true` and `false` as equivallents of enabled/disabled.) If 'enabled', then the builder will execute and throw
exceptions when it fails. When 'disabled', builders will not execute. When set to 'optional', builders will execute but failures will be silent. This can be useful in
cases like "User Secrets" where the backing secrets file is only likely to exist on a dev machine and not in a staging or test environment.
The default value is `optional`, though some config builders (such as the KeyPerFile and Azure-based builders) will use `enabled`.

#### recur
As processing of multiple configuration sections becomes entwined, the chances of [falling into a recursive cycle
of config loading](Intro.md#recursion) increases. Especially due to the [appSettings as parameters](#appsettings-parameters) feature, 
[this issue](#61) has a higher likelihood of striking and can be difficult to diagnose. This is where 'RecrusionGuard'
comes in. It will try to keep track of config sections that are being processed to detect any
potential loops. When a loop is detected, it can either _throw_ an exception to call attention to the bad
configuration, or it can _stop_ the loop and silently unwind, or it can do nothing and _allow_ the loop
to continue. The latter two options is not recommended as it
may result in non-deterministic results and overflows. Options for this setting are `Throw`, `Stop`, or `Allow`. The default is `Throw`.

#### charMap
This is a setting that defines which characters from a config key need to be replaced with which alternate characters
when querying for a value in a particular config source. This feature actually maps strings, and is not restricted to
single character replacement. The format for charMap parameter is a comma-separated list of `string=string`. To include
comma or equals in your mappings, you can escape them by doubling. The base default value is to not map any characters, but a
number of builders such as the Environment and Azure builders do populate this mapping by default.

#### escapeExpandedValues
Somewhat of a remnant from earlier versions that allowed operating on raw XML, this flag can be used to specify
whether or not to XML-escape values when replacing tokens in 'Token' mode. The default value is `false`.

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
configuration section) there are limitations when using this feature on the `appSettings` section itself. If a builder is currently processing the `appSettings` section
when using this feature, then the feature is still enabled - __but__ it can only draw on values that were hard-coded, or inserted into `appSettings` by config builders that
execute before it in the builder chain.

Although most initialization parameters can take advantage of this flexibility, it might not always make sense to apply this technique to all parameters. Of the
base parameters defined for all key/value config builders, `tokenPrefix` stands out as the common setting that *does not allow* reading from `appSettings`. 


## Config Builders In This Project
*Note about following codeboxes: Parameters inside `[]`s are optional. Parameters grouped in `()`s are mutually exclusive. Parameters beginning with `@` allow appSettings substitution. The first line of parameters are common to all builders and optional. Their meaning and use are [documented above](#keyvalue-config-builders) and they are grouped on one line for brevity.
Whenever a builder has a different default value for a given parameter, the differing default is also listed.

### EnvironmentConfigBuilder
```xml
<add name="Environment"
    [@mode|@enabled="optional"|@charMap=":=__"|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues]
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
    [@mode|@enabled="optional"|@charMap|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues]
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
  attribute, the `UserSecretsConfigBuilder` will look in a well-known local location (%APPDATA%\Microsoft\UserSecrets\\&lt;userSecretsId&gt;\secrets.xml in
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

### Azure Config Builders
This repo contains two different configuration builders that draw from two different Azure stores: Key Vault and App Configuration.
These stores have differences and were developed to accommodate different usage scenarios. But they both draw on some similar
capabilities from the Azure SDK - Authentication in particular.

Both builders make use of [`DefaultAzureCredential`](https://docs.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
for connecting to their respective service. This is a powerful yet easy-to-use method for connecting to Azure services and it provides
these builders with quite a bit of flexibility. `DefaultAzureCredential` searches through a list of different types of Azure credential
sources until it finds one to use, which gives applications that use these providers quite a few options. For **User-Assigned Managed
Identity**, or **Client Certificate-based** authorization, applications can take advantage of the abilities of [`EnvironmentalCredential`](https://docs.microsoft.com/en-us/dotnet/api/azure.identity.environmentcredential)
to read the necessary options for these methods via environment variables. Builders who require more complexity in the selection of
their Azure credential for these builders can extend and override the `GetCredential()` method of either builder to supply the correct
`TokenCredential` for connecting to Azure.

In a similar vein to `GetCredential()`, builders who need finer control over the way it connects to Azure, the `GetConfigurationClientOptions()`
and `GetSecretClientOptions()` virtual methods have been added to the Azure config builders to support special scenarios. (For
example, connecting to Azure through a proxy.)

NOTE: These packages both currently depend on version ***1.2*** of the `Azure.Identity` nuget package. This version was chosen because
it has a fairly comprehensive list of capabilities for `DefaultAzureCredential` but also is a relatively early version of this SDK package.
This way these builders won't be responsible for forcing unwanted package upgrades when not necessary. However, `DefaultAzureCredential`
[frequently picks up new capabilies](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/CHANGELOG.md), and
users are encouraged to manually update the version of `Azure.Identity` their application uses if they want to take advantage of new
features.

#### AzureAppConfigurationBuilder
```xml
<add name="AzureAppConfig"
    [@mode|@enabled="enabled"|@charMap|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues]
    (@endpoint="https://your-appconfig-store.azconfig.io" | @connectionString="Endpoint=https://your-appconfig-store.azconfig.io;Id=XXXXXXXXXX;Secret=XXXXXXXXXX")
    [@keyFilter="string"]
    [@labelFilter="label"]
    [@acceptDateTime="DateTimeOffset"]
    [@useAzureKeyVault="bool"]
    type="Microsoft.Configuration.ConfigurationBuilders.AzureAppConfigurationBuilder, Microsoft.Configuration.ConfigurationBuilders.AzureAppConfiguration" />
```
>:information_source: [NOTE]
>
>When connecting to an Azure App Configuration store, the identity that is being used must be assigned either the `Azure App Configuration Data Reader` role or the `Azure App Configuration Data Owner` role. Otherwise the config builder will encounter a "403 Forbidden" response from Azure and throw an exception if not `optional`.

[AppConfiguration](https://docs.microsoft.com/en-us/azure/azure-app-configuration/overview) is a new offering from Azure. If you
wish to use this new service for managing your configuration, then use this AzureAppConfigurationBuilder. Either `endpoint` or `connectionString` are
required, but all other attributes are optional. If both `endpoint` and `connectionString` are used, then preference is given to the connection string.
  * `endpoint` - This specifies the AppConfiguration store to connect to.
  * `connectionString` - This specifies the AppConfiguration store to connect to, along with the Id and Secret necessary to access the service. Be careful
	not to expose any secrets in your code, repos, or App Configuration stores if you use this method for connecting.
  * `keyFilter` - Use this to select a set of configuration values matching a certain key pattern.
  * `labelFilter` - Only retrieve configuration values that match a certain label.
  * `acceptDateTime` - Instead of versioning ala Azure Key Vault, AppConfiguration uses timestamps. Use this attribute to go back in time
  to retrieve configuration values from a past state.
  * `useAzureKeyVault` - Enable this feature to allow AzureAppConfigurationBuilder to connect to and retrieve secrets from Azure Key Vault for
  config values that are stored in Key Vault. The same managed service identity that is used for connecting to the AppConfiguration service will
  be used to connect to Key Vault. The Key Vault uri is retrieved as part of the data from AppConfiguration and does not need to be specified here.
  Default is `false`.

#### AzureKeyVaultConfigBuilder
```xml
<add name="AzureKeyVault"
    [@mode|@enabled="enabled"|@charMap=":=-,_=-,.=-,+=-,\=-"|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues]
    (@vaultName="MyVaultName" | @uri="https://MyVaultName.vault.azure.net")
    [@version="secrets version"]
    [@preloadSecretNames="true"]
    type="Microsoft.Configuration.ConfigurationBuilders.AzureKeyVaultConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Azure" />
```
If your secrets are kept in Azure Key Vault, then this config builder is for you. There are three additional attributes for this config builder. The `vaultName`
attribute (or `uri`) is required. Previous iterations of this config builder allowed for a `connectionString` as a way to supply credential information for connecting to
Azure Key Vault. This method is no longer allowed as it is not a supported scenario for the current `Azure.Identity` SDK which is used for connecting
to Azure services. Instead, this iteration of the config builder exclusively uses [DefaultAzureCredential](https://docs.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential)
from the `Azure.Identity` package to handle credentials for connecting to Azure Key Vault.
  * `vaultName` - This (or `uri`) is a required attribute. It specifies the name of the vault in your Azure subscription from which to read key/value pairs.
  * `uri` - Connect to non-Azure Key Vault providers with this attribute. If not specified, Azure is the assumed Vault provider. If the uri _is_ specified, then `vaultName` is no longer a required parameter.
  * `version` - Azure Key Vault provides a versioning feature for secrets. If this is specified, the builder will only retrieve secrets matching this version.
  * `preloadSecretNames` - By default, this builder will query __all__ the key names in the key vault when it is initialized to improve performance. If this is
  a concern, set this attribute to 'false', and secrets will be retrieved one at a time. This could also be useful if the vault allows "Get" access but not
  "List" access. (NOTE: Disabling preload is incompatible with Greedy mode.)

__Tip:__ Azure Key Vault uses random per-secret Guid assignments for versioning, which makes specifying a secret `version` tag on this builder rather
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
    [@mode|@enabled="enabled"|@charMap|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues]
    @directoryPath="PathToSourceDirectory"
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
    [@mode|@enabled="optional"|@charMap|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues]
    @jsonFile="~\config.json"
    [@jsonMode="(Flat*|Sectional)"]
    type="Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json" />
```
Because .Net Core projects can rely heavily on json files for configuration, it makes some sense to allow those same files to be used in full-framework
configuration as well. You can imagine that the heirarchical nature of json might enable some fantastic capabilities for building complex configuration sections.
But this config builder is meant to be just a simple mapping from a flat key/value source into specific key/value areas of full-framework configuration. Thus its name
begins with 'Simple.' Think of the backing json file as a basic dictionary, rather than a complex heirarchical object.

(A multi-level heirarchical file can be used. This provider will simply 'flatten' the depth by appending the property name at each level using ':' as a delimiter.)

There are three additional attributes that can be used to configure this builder:
  * `jsonFile` - A required attribute specifying the json file to draw from. The '~' character can be used at the start to reference the app root.
  * `jsonMode` - `[Flat|Sectional]`. 'Flat' is the default.
    - This attribute requires a little more explanation. It says above to think of the json file as a single flat key/value source. This is the usual that applies to
    other key/value config builders like `EnvironmentConfigBuilder` and `AzureKeyVaultConfigBuilder` because those sources provide no other option. If the
    `SimpleJsonConfigBuilder` is configured in 'Sectional' mode, then the json file is conceptually divided - at the top level only - into multiple simple dictionaries.
    Each one of those dictionaries will only be applied to the config section that matches the top-level property name attached to them. For example:
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
