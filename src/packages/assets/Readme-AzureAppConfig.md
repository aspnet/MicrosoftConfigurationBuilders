# Azure AppConfiguration ConfigBuilder

This package provides a config builder that draws its values from an Azure App Configuration. The builder [uses `DefaultAzureCredential`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#azure-config-builders) for connecting with the App Configuration service. More comprehensive documentation exists at [the MicrosoftConfigBuilders project](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#azureappconfigurationbuilder).

The basic usage of this builder is given below. Parameters inside `[]`s are optional. Parameters grouped in `()`s are mutually exclusive. Parameters beginning with `@` allow appSettings substitution. The first line of parameters are common to all builders and optional. Their meaning, usage, and defaults are [documented here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#introduction-to-keyvalue-config-builders). They are grouped on one line for brevity. When a builder uses a different default value than the project default, the differing value is also listed. Builder-specific settings are listed on each line thereafter followed by a brief explanation. 

```xml
<add name="AzureAppConfig"
    [@mode|@enabled="enabled"|@charMap|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues]
    (@endpoint="https://your-appconfig-store.azconfig.io" | @connectionString="Endpoint=https://your-appconfig-store.azconfig.io;Id=XXXXXXXXXX;Secret=XXXXXXXXXX")
    [@snapshot="string"]
    [@keyFilter="string"]
    [@labelFilter="label"]
    [@acceptDateTime="DateTimeOffset"]
    [@useAzureKeyVault="bool"]
    [@preloadValues="bool"]
    type="Microsoft.Configuration.ConfigurationBuilders.AzureAppConfigurationBuilder, Microsoft.Configuration.ConfigurationBuilders.AzureAppConfiguration" />
```
>:information_source: [NOTE]
>
>When connecting to an Azure App Configuration store, the identity that is being used must be assigned either the `Azure App Configuration Data Reader` role or the `Azure App Configuration Data Owner` role. Otherwise the config builder will encounter a "403 Forbidden" response from Azure and throw an exception if not `optional`.

  * `endpoint` - This specifies the AppConfiguration store to connect to.
  * `connectionString` - The recommendation is to use `endpoint`. ~~This specifies the AppConfiguration store to connect to, along with the Id and Secret necessary to access the service. Be carefulnot to expose any secrets in your code, repos, or App Configuration stores if you use this method for connecting.~~
  * `snapshot` - Use this attribute to draw configuration values from the specific AppConfig snapshot named by the value of this attribute. **Setting this attribute will cause `keyFilter`, `labelFilter`, and `acceptDateTime` to be silently ignored.**
  * `keyFilter` - Use this to select a set of configuration values matching a certain key pattern.
  * `labelFilter` - Only retrieve configuration values that match a certain label.
  * `acceptDateTime` - Instead of versioning ala Azure Key Vault, AppConfiguration uses timestamps. Use this attribute to go back in time to retrieve configuration values from a past state.
  * `useAzureKeyVault` - Enable this feature to allow AzureAppConfigurationBuilder to connect to and retrieve secrets from Azure Key Vault for config values that are stored in Key Vault. The same managed service identity that is used for connecting to the AppConfiguration service will be used to connect to Key Vault. The Key Vault uri is retrieved as part of the data from AppConfiguration and does not need to be specific here. Default is `false`.
  * `preloadValues` - Enable this feature to have the builder pre-load all values from the AppConfiguration store into memory. Essentially the same as a 'Greedy' mode fetch of config values from the AppConfig store - but without dumping them all into the working config section.If you have a large cache of config values, or you have some values (that match key and label filters) that you don't want to pull into application memory - even if they don't get applied to existing config entries - then disable this. Otherwise, it is `true` by default because fetching as many config values as possible in one request is a much more scalable design and will help applications avoid throttling on the service end.

### V3.1 Updates:
  * Added Snapshot capabilities.
  * Added preloading all values - on by default.
  * Fixed `GetCredential()` and related option-overload issues.
  * Auth failures are "optional" for [Azure Config Builders](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#azure-config-builders).
  * Fixed bug with key filters in `Strict` mode.

### V3 Updates:
A more complete list of updates [lives here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/README.md#v3-updates). These are the ones most relevant to this builder:
  * :warning: ***Breaking Change*** - `Expand` mode is gone. It has been [replaced by `Token` mode](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#mode).
  * The [Azure Config Builders](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#azure-config-builders) have been updated to require a newer minimum version of `Azure.Identity` by default which allows for more methods of connecting to Azure, such as **User-Assigned Managed Identity**, or **Client Certificate-based** via environment variables. Also a pair of overloads (`GetCredential` and `GetSecretClientOptions/GetConfigurationClientOptions`) have been added for users who need something more than `DefaultAzureCredential` with default client options can provide.
  * `optional` attribute is obsolete => [`enabled`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) attribute which provides more versatility. (The `optional` attribute is still parsed and recognized in the absence of the newer [`enabled`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) attribute, but builders should migrate to use the new attribute name when possible. Installation scripts should try to handle this automatically.)
  * `AzureAppConfiguration` nuget package version is revved to match the rest of this suite of builders, rather than being 1 major version behind. (ie, `AzureAppConfiguration:3.0` now depends on `Base:3.0` rather than `AzureAppConfiguration:1.0` depending on `Base:2.0`)

  ### V1 Updates:
A more complete list of updates [lives here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/README.md#v2-updates). These are the ones most relevant to this builder:
  * Azure App Configuration Support - There is a [new builder](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#azureappconfigurationbuilder) for drawing values from the new Azure App Configuration service.
