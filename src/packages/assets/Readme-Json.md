# SimpleJson ConfigBuilder

This package provides a config builder that draws its values from Json file. It comes without many frills, but does offer the ability to treat the entire Json file as a single flat config source or as a set of config sources broken into flat sections at the first level. More comprehensive documentation exists at [the MicrosoftConfigBuilders project](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#simplejsonconfigbuilder).

The basic usage of this builder is given below. Parameters inside `[]`s are optional. Parameters grouped in `()`s are mutually exclusive. Parameters beginning with `@` allow appSettings substitution. The first line of parameters are common to all builders and optional. Their meaning, usage, and defaults are [documented here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#introduction-to-keyvalue-config-builders). They are grouped on one line for brevity. When a builder uses a different default value than the project default, the differing value is also listed. Builder-specific settings are listed on each line thereafter followed by a brief explanation. 

```xml
<add name="SimpleJson"
    [@mode|@enabled="optional"|@charMap|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues]
    @jsonFile="~\config.json"
    [@jsonMode="(Flat*|Sectional)"]
    type="Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json" />
```

### V3 Updates:
A more complete list of updates [lives here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/README.md#v3-updates). These are the ones most relevant to this builder:
  * :warning: ***Breaking Change*** - `Expand` mode is gone. It has been [replaced by `Token` mode](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#mode).
  * Json use has migrated to use `System.Text.Json` instead of `Newtonsoft.Json`.
  * `optional` attribute is obsolete => [`enabled`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) attribute which provides more versatility. (The `optional` attribute is still parsed and recognized in the absence of the newer [`enabled`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) attribute, but builders should migrate to use the new attribute name when possible. Installation scripts should try to handle this automatically.)

### V2 Updates:
A more complete list of updates [lives here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/README.md#v2-updates). These are the ones most relevant to this builder:
  * ConfigBuilder Parameters from AppSettings - This has been one of the most asked for features of these config builders. With V2, it is now possible to read initialization parameters for config builders from `appSettings`. Read more about it [here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#appsettings-parameters).
  * **[[Obsolete]] This has been superceded by the [enabled](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) tag.** (~~Base Optional Tag - The `optional` tag that some of the builders in this project employed in V1 has been moved into the base class and is now available on all key/value config builders.~~)
