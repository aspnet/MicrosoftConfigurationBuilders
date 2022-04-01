# Implementing More Key/Value Config Builders

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
            // Use this initializer only for things that absolutely must be read from
            // the 'config' collection immediately upon creation.
            // AppSettings parameter substitution is not available at this point.
            // Use LazyInitialize(string, NameValueCollection) instead whenever possible.
        }

        protected override void LazyInitialize(string name, NameValueCollection config)
        {
            // Use this for things that don't need to be initialized until just before
            //   config values are retrieved.
            //
            // *First, set the default values for 'Enabled' and 'CharacterMap' if
            //   different from the base.
            //
            // *Second, be sure to call 'base.LazyInitialize(name, config)'. AppSettings
            //   parameter substitution via 'UpdateConfigSettingWithAppSettings(parameterName)'
            //   will be available after that call.
            //
            // *Third, check the value of 'Enabled' to see if there is any need to continue.
            //
            // *Lastly, read any additional parameters and do any other tasks needed
            //   in preparation for retrieving config values.
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
            // place for screening key names. It is particularly helpful in `Strict` and `Token` modes where
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
