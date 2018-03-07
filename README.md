# Configuration Builders

Configuration Builders are a new feature of the full .Net Framework, introduced in .Net 4.7.1. You can read about the concept in [this blog post](http://jeffreyfritz.com/2017/11/modern-configuration-for-asp-net-4-7-1-with-configurationbuilders/).
With this project, Microsoft is providing a basic set of Configuration Builders that should make it easy for developers to get started with the new feature. They
are also intended to address some of the basic needs of applications as they move into a container and cloud focused environment.

## Key/Value Config Builders

If you read the blog post linked above, you probably recognize that Configuration Builders can be quite flexible. Applications can use the Configuration Builder
concept to construct incredibly complex configuration on the fly. But for the most common usage scenarios, a simple key/value replacement mechanism is all that
is needed. Most of the config builders in this project are such key/value builders.

The basic concept of these config builders is to draw on an external source of key/value information to populate parts of the config system that are key/value in
nature. Specifically, the `appSettings` and `connectionStrings` sections receive special treatment from these key/value config builders. These builders can be
set to run in three different modes:
  * `Strict` - This is the default. In this mode, the config builder will only operate on well-known key/value-centric configuration sections. It will enumerate each
		key in the section, and if a matching key is found in the external source, it will replace the value in the resulting config section with the value from the
		external source.
  * `Greedy` - This mode is closely related to `Strict` mode, but instead of being limited to keys that already exist in the original configuration, the config builders
		will dump all key/value pairs from the external source into the resulting config section.
  * `Expand` - This last mode operates on the raw xml before it gets parsed into a config section object. It can be thought of as a simple expansion of tokens in a
		string. Any part of the raw xml string that matches the pattern __`${token}`__ is a candidate for token expansion. If no corresponding value is found in the
		external source, then the token is left alone.

Another feature of these key/value Configuration Builders is prefix handling. Because full-framework .Net configuration is complex and nested, and external key/value
sources are by nature quite simple and flat, leveraging key prefixes can be useful. For example, if you want to inject both App Settings and Connection Strings into
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

One final setting that is common among all of these key/value builders is `stripPrefix`. The code above does a good job of separating app settings from connection
strings... but now all the keys in AppSettings start with "AppSetting_". Maybe this is fine for code you wrote. Chances are that prefix is better off stripped from the
key name before being inserted into AppSettings. `stripPrefix` is a simple boolean value, and accomplishes just that. It's default value is `false`.

## Config Builders In This Project

### EnvironmentConfigBuilder
```xml
<add name="Environment"
    [mode|prefix|stripPrefix]
    type="Microsoft.Configuration.ConfigurationBuilders.EnvironmentConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Environment" />
```
This is the simplest of the config builders. It draws its values from Environment, and it does not have any additional configuration options.

### UserSecretsConfigBuilder
```xml
<add name="UserSecrets"
    [mode|prefix|stripPrefix]
    (userSecretsId="12345678-90AB-CDEF-1234-567890" | userSecretsFile="~\secrets.file")
    [optional="true"]
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
  attribute, the `UserSecretsConfigBuilder` will look in a well-known local location for a secrets file belonging to this identifier. In MSBuild environments,
  the value of this attribute will be replaced with the project property $(UserSecretsId) in the output directory iff the initial value is '${UserSecretsId}'.
  One of this attribute or the 'userSecretsFile' attribute is required.
  * `userSecretsFile` - An optional attribute specifying the file containing the secrets. The '~' character can be used at the start to reference the app root.
  One of this attribute or the 'userSecretsId' attribute is required. If both are specified, 'userSecretsFile' takes precedence.
  * `optional` - A simple boolean to avoid throwing exceptions if the secrets file cannot be found. The default is `true`.

### AzureKeyVaultConfigBuilder
```xml
<add name="AzureKeyVault"
    [mode|prefix|stripPrefix]
    (vaultName="MyVaultName" |
     uri="https://MyVaultName.vault.azure.net")
    [connectionString="connection string"]
    type="Microsoft.Configuration.ConfigurationBuilders.AzureKeyVaultConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Azure" />
```
If your secrets are kept in Azure Key Vault, then this config builder is for you. There are three additional attributes for this config builder. The `vaultName` is
required. The other attributes allow you some manual control about which vault to connect to, but are only necessary if the application is not running in an
environment that works magically with `Microsoft.Azure.Services.AppAuthentication`. The Azure Services Authentication library is used to automatically pick
up connection information from the execution environment if possible, but you can override that feature by providing a connection string instead.
  * `vaultName` - This is a required attribute. It specifies the name of the vault in your Azure subscription from which to read key/value pairs.
  * `connectionString` - A connection string usable by [AzureServiceTokenProvider](https://docs.microsoft.com/en-us/azure/key-vault/service-to-service-authentication#connection-string-support)
  * `uri` - Connect to other Key Vault providers with this attribute. If not specified, Azure is the assumed Vault provider. If the uri _is_specified, then `vaultName` is no longer a required parameter.

### SimpleJsonConfigBuilder
```xml
<add name="SimpleJson"
    [mode|prefix|stripPrefix]
    jsonFile="~\config.json"
    [optional="true"]
    [jsonMode="(Flat|Sectional)"]
    type="Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json" />
```
Because .Net Core projects can rely heavily on json files for configuration, it makes some sense to allow those same files to be used in full-framework
configuration as well. You can imagine that the heirarchical nature of json might enable some fantastic capabilities for building complex configuration sections.
But this config builders is meant to be a simple mapping from a flat key/value source into specific key/value areas of full-framework configuration. Thus its name
begins with 'Simple.' Think of the backing json file as a simple dictionary, rather than a comlex heirarchical object.

(A multi-level heirarchical file can be used. This provider will simply 'flatten' the depth by appending the property name at each level using ':' as a delimiter.)

There are three additional attributes that can be used to configure this builder:
  * `jsonFile` - A required attribute specifying the json file to draw from. The '~' character can be used at the start to reference the app root.
  * `optional` - A simple boolean to avoid throwing exceptions if the json file cannot be found. The default is `true`.
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

## Implementing More Key/Value Config Builders

If you don't see a config builder here that suits your needs, you can write your own. Referencing the `Basic` nuget package for this project will get you the base upon which
all of these builders inherit. Most of the heavy-ish lifting and consistent behavior across key/value config builders comes from this base. Take a look at the code for more
detail, but in many cases implementing a custom key/value config builder in this same vein is as simple as inheriting the base, and implementing two simple methods.
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

## Blog Posts
[.Net Framework 4.7.1 ASP.NET and Configuration features](https://blogs.msdn.microsoft.com/dotnet/2017/09/13/net-framework-4-7-1-asp-net-and-configuration-features/)
[Modern Configuration for ASP.NET 4.7.1 with ConfigurationBuilders](http://jeffreyfritz.com/2017/11/modern-configuration-for-asp-net-4-7-1-with-configurationbuilders/)  
[Service-to-service authentication to Azure Key Vault using .NET](https://docs.microsoft.com/en-us/azure/key-vault/service-to-service-authentication#connection-string-support)
