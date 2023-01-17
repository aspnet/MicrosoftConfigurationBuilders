# UserSecrets ConfigBuilder

This package provides a config builder that draws its values from an `Xml` file - usually stored outside of source controll - containing a list of secrets. The secrets file can be configured directly, or it can be specified by a `userSecretsId` which helps to locate the file in a well-known 'UserSecrets' directory. More comprehensive documentation for using this builder exists at [the MicrosoftConfigBuilders project](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#usersecretsconfigbuilder).

The basic usage of this builder is given below. Parameters inside `[]`s are optional. Parameters grouped in `()`s are mutually exclusive. Parameters beginning with `@` allow appSettings substitution. The first line of parameters are common to all builders and optional. Their meaning and use are [documented at the MicrosoftConfigBuilders project](blob/main/docs/KeyValueConfigBuilders.md#keyvalue-config-builders). Tthey are grouped on one line for brevity. Builder-specific settings are listed on each line thereafter followed by a brief explanation at the end. When a builder uses a different default value than the [MicrosoftConfigBuilders](https://github.com/aspnet/MicrosoftConfigurationBuilders) project as a whole, the differing default is also listed.

```xml
<add name="UserSecrets"
    [@mode|@enabled="optional"|@charMap|@prefix|@stripPrefix|tokenPattern|@escapeExpandedValues]
    (@userSecretsId="12345678-90AB-CDEF-1234-567890" | @userSecretsFile="~\secrets.file")
    type="Microsoft.Configuration.ConfigurationBuilders.UserSecretsConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.UserSecrets" />
```

  * `userSecretsId` - This is the preferred method for identifying an xml secrets file. It works similar to .Net Core, which uses a 'UserSecretsId' project property to store this identifier. With this attribute, the `UserSecretsConfigBuilder` will look in a well-known local location (%APPDATA%\Microsoft\UserSecrets\\&lt;userSecretsId&gt;\secrets.xml in Windows environments) for a secrets file belonging to this identifier.
  * `userSecretsFile` - An optional attribute specifying the file containing the secrets. The '~' character can be used at the start to reference the app root. One of this attribute or the 'userSecretsId' attribute is required. If both are specified, 'userSecretsFile' takes precedence.

### V3 Updates:
A more complete list of updates [lives here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/README.md#v3-updates). These are the ones most relevant to this builder:
  * :warning: ***Breaking Change*** - `Expand` mode is gone. It has been [replaced by `Token` mode](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#mode).
  * `optional` attribute is obsolete => [`enabled`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) attribute which provides more versatility. (The `optional` attribute is still parsed and recognized in the absence of the newer [`enabled`](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) attribute, but builders should migrate to use the new attribute name when possible. Installation scripts should try to handle this automatically.)

### V2 Updates:
A more complete list of updates [lives here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/README.md#v2-updates). These are the ones most relevant to this builder:
  * ConfigBuilder Parameters from AppSettings - This has been one of the most asked for features of these config builders. With V2, it is now possible to read initialization parameters for config builders from `appSettings`. Read more about it [here](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#appsettings-parameters).
  * **[[Obsolete]] This has been superceded by the [enabled](https://github.com/aspnet/MicrosoftConfigurationBuilders/blob/main/docs/KeyValueConfigBuilders.md#enabled) tag.** (~~Base Optional Tag - The `optional` tag that some of the builders in this project employed in V1 has been moved into the base class and is now available on all key/value config builders.~~)
