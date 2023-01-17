# Azure KeyVault ConfigBuilder

This package provides a config builder that draws its values from an Azure Key Vault. The builder [uses `DefaultAzureCredential`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#azure-config-builders) for connecting with the Key Vault service. More comprehensive documentation exists at [the MicrosoftConfigBuilders project](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#azurekeyvaultconfigbuilder).

The basic usage of this builder is given below. Parameters inside `[]`s are optional. Parameters grouped in `()`s are mutually exclusive. Parameters beginning with `@` allow appSettings substitution. The first line of parameters are common to all builders and optional. Their meaning, usage, and defaults are [documented here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#introduction-to-keyvalue-config-builders). They are grouped on one line for brevity. When a builder uses a different default value than the project default, the differing value is also listed. Builder-specific settings are listed on each line thereafter followed by a brief explanation. 

```xml
<add name="AzureKeyVault"
    [@mode|@enabled="enabled"|@charMap=":=-,_=-,.=-,+=-,\=-"|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues]
    (@vaultName="MyVaultName" | @uri="https://MyVaultName.vault.azure.net")
    [@version="secrets version"]
    [@preloadSecretNames="true"]
    type="Microsoft.Configuration.ConfigurationBuilders.AzureKeyVaultConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Azure" />
```

  * `vaultName` - This (or `uri`) is a required attribute. It specifies the name of the vault in your Azure subscription from which to read key/value pairs.
  * `uri` - Connect to non-Azure Key Vault providers with this attribute. If not specified, Azure is the assumed Vault provider. If the uri _is_ specified, then `vaultName` is no longer a required parameter.
  * `version` - Azure Key Vault provides a versioning feature for secrets. If this is specified, the builder will only retrieve secrets matching this version.
  * `preloadSecretNames` - By default, this builder will query __all__ the key names in the key vault when it is initialized to improve performance. If this is a concern, set this attribute to 'false', and secrets will be retrieved one at a time. This could also be useful if the vault allows "Get" access but not "List" access. (NOTE: Disabling preload is incompatible with Greedy mode.)

__Tip:__ To use versioned secrets, it is _not_ recommended to use the `version` attribute on the builder. Rather, include the version in the key-name and this builder will know what to do. For example:
```xml
<appSettings configBuilders="AzureKeyVault">
  <add key="item1" value="Replaced with latest value from the key vault." />
  <add key="item2/0123456789abcdefdeadbeefbadf00d" value="Replaced with specific version only, and resulting key is simply 'item2'." />
</appSettings>
```

### V3 Updates:
A more complete list of updates [lives here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/README.md#v3-updates). These are the ones most relevant to this builder:
  * :warning: ***Breaking Change*** - `Expand` mode is gone. It has been [replaced by `Token` mode](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#mode).
  * The [Azure Config Builders](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#azure-config-builders) have been updated to require a newer minimum version of `Azure.Identity` by default which allows for more methods of connecting to Azure, such as **User-Assigned Managed Identity**, or **Client Certificate-based** via environment variables. Also a pair of overloads (`GetCredential` and `GetSecretClientOptions/GetConfigurationClientOptions`) have been added for users who need something more than `DefaultAzureCredential` with default client options can provide.
  * `optional` attribute is obsolete => [`enabled`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) attribute which provides more versatility. (The `optional` attribute is still parsed and recognized in the absence of the newer [`enabled`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) attribute, but builders should migrate to use the new attribute name when possible. Installation scripts should try to handle this automatically.)

### V2 Updates:
A more complete list of updates [lives here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/README.md#v2-updates). These are the ones most relevant to this builder:
  * ConfigBuilder Parameters from AppSettings - This has been one of the most asked for features of these config builders. With V2, it is now possible to read initialization parameters for config builders from `appSettings`. Read more about it [here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#appsettings-parameters).
  * Updateable Keys - Builders can now massage key names before inserting into config. The [AzureKeyVaultConfigBuilder](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#azurekeyvaultconfigbuilder) has been updated to use this feature to allow embedding 'version' tags in key names instead of applying a single 'version' tag to the builder.  (Note: This is seperate from, and performed *after* prefix stripping.)
  * **[[Obsolete]] This has been superceded by the [enabled](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) tag.** (~~Base Optional Tag - The `optional` tag that some of the builders in this project employed in V1 has been moved into the base class and is now available on all key/value config builders.~~)
