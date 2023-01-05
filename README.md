# Configuration Builders

[ConfigurationBuilder](https://docs.microsoft.com/en-us/dotnet/api/system.configuration.configurationbuilder?view=netframework-4.7.1)s are a feature of the full .Net
Framework that were introduced in .Net 4.7.1. You can read about the concept in [this blog post](http://jeffreyfritz.com/2017/11/modern-configuration-for-asp-net-4-7-1-with-configurationbuilders/).
While the framework for executing configuration injection now exists in .Net as of 4.7.1 with that feature, the framework does not ship with any pre-made builders in box.
The goal of this project is for Microsoft to provide a basic set of Configuration Builders that should make it easy for developers to leverage this feature in their apps. They
are also intended to address some of the basic dynamic/non-local configuration needs of applications as they move into a container and cloud focused environment.

The set of builders produced here are styled as "Key/Value Config Builders." The architecture of `ConfigurationBuilder` in the framework is actually quite flexible and can
be leveraged to handle a great number of unique situations. To keep things as easy and as broadly applicable as possible though, this project focuses on simple key/value
scenarios.

For more information about Configuration Builders and the features and builders in this project in particular, please refer to the docs
linked here:

  * [Introduction to Configuration Builders in .Net](docs/Intro.md)
  * [Key/Value Config Builders](docs/KeyValueConfigBuilders.md)
  * [Implementing Custom Key/Value Config Builders](docs/CustomConfigBuilders.md)
  * [Section Handlers](docs/SectionHandlers.md)
  * [FAQ](docs/FAQ.md)


<a name="updates"></a>
### V3 Updates:
  * :warning: ***Breaking Change*** - `Expand` mode is gone. It has been [replaced by `Token` mode](docs/KeyValueConfigBuilders.md#token).#TODO verify link (and #enabled and #charmap link)
  * `Utils.MapPath` - This was somewhat broken in ASP.Net scenarios previously. It should now reliably go against `Server.MapPath()` in ASP.Net scenarios. It has
        also been updated to fall back against the directory of the config file being processed when resolving the app root in the case of a `Configuration`
        object being created by `ConfigurationManager.OpenConfiguration*` API's rather than being part of a fully-initialized runtime configuration stack.
  * Json use has migrated to use `System.Text.Json` instead of `Newtonsoft.Json`.
  * The [Azure Config Builders](#azure-config-builders) have been updated to require a newer minimum version of `Azure.Identity` by default which allows for more
        methods of connecting to Azure, such as **User-Assigned Managed Identity**, or **Client Certificate-based** via environment variables. An overload named
        `GetCredential` has also been added for users who need something more custom than upgrading `Azure.Identity` can easily provide.
  * Added *RecursionGuard* to help detect and prevent situations where a `ConfigurationBuilder` accessing values from a `ConfigurationSection` other than the one
        which it is currently processing could result in stack overflow.
  * `optional` attribute is obsolete => [`enabled`](docs/KeyValueConfigBuilders.md#enabled) attribute which provides more versatility. (The `optional` attribute is still parsed and recognized in the absence
        of the newer [`enabled`](docs/KeyValueConfigBuilders.md#enabled) attribute, but builders should migrate to use the new attribute name when possible. Installation scripts should try to handle this
        automatically.)
  * Character Mapping - Some config builders have had an internal mapping of characters that might exist in keys in the config file but are illegal in keys at the
        source. As more scenarios come to light and individual prefrences are not always unanimous, V3 instead adds the [`charMap`](docs/KeyValueConfigBuilders.md#charmap) attribute to allow this character
        mapping to work with all **KeyValueConfigBuilders** and to be handled in an easily configurable manner.

### V2 Updates:
  * Azure App Configuration Support - There is a [new builder](docs/KeyValueConfigBuilders.md#azureappconfigurationbuilder) for drawing values from the new Azure App Configuration service.
  * ConfigBuilder Parameters from AppSettings - This has been one of the most asked for features of these config builders. With V2, it is now possible to
		read initialization parameters for config builders from `appSettings`. Read more about it [here](docs/KeyValueConfigBuilders.md#appsettings-parameters).
  * Lazy Initialization - As part of the work to enable pulling config parameters from `appSettings`, these key/value configuration builders now support a
		lazy initialization model. Things that must happen immediately can be left in the existing `Initialize(name, config)` method, or builders can leverage
		the new `LazyInitialize(name, config)` method for things that can happen just before retrieving values. All builders in this project have been updated to
		be lazy whenever possible.
  * Updateable Keys - Builders can now massage key names before inserting into config. The [AzureKeyVaultConfigBuilder](docs/KeyValueConfigBuilders.md#azurekeyvaultconfigbuilder) has been
		updated to use this feature to allow embedding 'version' tags in key names instead of applying a single 'version' tag to the builder.  (Note: This is
		seperate from, and performed *after* prefix stripping.)
  * **[[Obsolete]] This has been superceded by the [enabled](docs/KeyValueConfigBuilders.md#enabled) tag.** (~~Base Optional Tag - The `optional` tag that some of the builders in
        this project employed in V1 has been moved into the base class and is now available on all key/value config builders.~~)
  * Escaping Expanded Values - It is possible to xml-escape inserted values in ~~`Expand`~~`Token`(as of V3) mode now using the new [escapeExpandedValues](#escapeexpandedvalues) attribute.
  * Section Handlers - This feature allows users to develop extensions that will apply key/value config to sections other than `appSettings` and `connectionStrings`
		if desired. Read more about this feature in the [Section Handlers](docs/SectionHandlers.md) segment below.

## How to contribute

Information on contributing to this repo is in the [Contributing Guide](CONTRIBUTING.md).

## Blog Posts
[Announcing .NET 4.7.1 Tools for the Cloud](https://blogs.msdn.microsoft.com/webdev/2017/11/17/announcing-net-4-7-1-tools-for-the-cloud/)  
[.Net Framework 4.7.1 ASP.NET and Configuration features](https://blogs.msdn.microsoft.com/dotnet/2017/09/13/net-framework-4-7-1-asp-net-and-configuration-features/)  
[Modern Configuration for ASP.NET 4.7.1 with ConfigurationBuilders](http://jeffreyfritz.com/2017/11/modern-configuration-for-asp-net-4-7-1-with-configurationbuilders/)  
[Service-to-service authentication to Azure Key Vault using .NET](https://docs.microsoft.com/en-us/azure/key-vault/service-to-service-authentication#connection-string-support)  
