# Environment ConfigBuilder

This package offers the most basic of the config builders. It draws its values from Environment, and it does not have any additional configuration options. More comprehensive documentation exists at [the MicrosoftConfigBuilders project](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#environmentconfigbuilder).

The basic usage of this builder is given below. Parameters inside `[]`s are optional. Parameters grouped in `()`s are mutually exclusive. Parameters beginning with `@` allow appSettings substitution. The first line of parameters are common to all builders and optional. Their meaning, usage, and defaults are [documented here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#introduction-to-keyvalue-config-builders). They are grouped on one line for brevity. When a builder uses a different default value than the project default, the differing value is also listed. Builder-specific settings are listed on each line thereafter followed by a brief explanation. 

```xml
<add name="Environment"
    [@mode|@enabled="optional"|@charMap=":=__"|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues]
    type="Microsoft.Configuration.ConfigurationBuilders.EnvironmentConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Environment" />
```

### V3 Updates:
A more complete list of updates [lives here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/README.md#v3-updates). These are the ones most relevant to this builder:
  * :warning: ***Breaking Change*** - `Expand` mode is gone. It has been [replaced by `Token` mode](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#mode).
  * `optional` attribute is obsolete => [`enabled`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) attribute which provides more versatility. (The `optional` attribute is still parsed and recognized in the absence of the newer [`enabled`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) attribute, but builders should migrate to use the new attribute name when possible. Installation scripts should try to handle this automatically.)
  * Character Mapping - Some config builders have had an internal mapping of characters that might exist in keys in the config file but are illegal in keys at the source. As more scenarios come to light and individual prefrences are not always unanimous, V3 instead adds the [`charMap`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#charmap) attribute to allow this character mapping to work with all **KeyValueConfigBuilders** and to be handled in an easily configurable manner.

### V2 Updates:
A more complete list of updates [lives here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/README.md#v2-updates). These are the ones most relevant to this builder:
  * ConfigBuilder Parameters from AppSettings - This has been one of the most asked for features of these config builders. With V2, it is now possible to read initialization parameters for config builders from `appSettings`. Read more about it [here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#appsettings-parameters).
  * **[[Obsolete]] This has been superceded by the [enabled](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) tag.** (~~Base Optional Tag - The `optional` tag that some of the builders in this project employed in V1 has been moved into the base class and is now available on all key/value config builders.~~)
