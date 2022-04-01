# MicrosoftConfigurationBuilders FAQ

We get a lot of questions about `KeyValueConfigBuilders` - and Configuration Builders in general. Here are
some of the most frequent along with answers that are hopefully helpful.

<a name="expand"></a>
<details>
  <summary><b>Why did you get rid of 'Expand' mode?</b></summary>
  
>  Because 'Expand' mode operated in the 'ProcessRawXml' phase of configuration building, while the other
>  modes all operate in 'ProcessConfigurationSection.' It was a bit of a balancing act trying to develop
>  features that work across both phases - a challenge which is sometimes quite difficult given the lack of
>  information we have about the section we are processing in 'ProcessRawXml.'
>  
>  For example, V3 of these builders tries to accomodate 'ConfigurationManager.OpenConfiguration()'
>  scenarios where apps want to read a config file that is not their own. In these cases, we need to
>  know information about the file and section we are processing that we just can't know in the
>  'ProcessRawXml' phase. Another example is the [parameters from appSettings](KeyValueConfigBuilders.md#appsettings-parameters)
>  feature which was disabled in 'Expand' mode while processing the appSettings section, but can
>  still be used somewhat functionally when executing any of the modes that operate in 'ProcessConfigurationSection.'
>
>  To make things simpler across the board, 'Expand' mode was replaced with 'Token' mode which should
>  operate in a fairly similar manner with the added benefit of being less prone to producing invalid
>  XML to muck things up. :smiley:
</details>

<a name="newhandler"></a>
<details>
  <summary><b>Can you add a `SectionHandler` for section `XYZ?`</b></summary>
  
>  We have included default `SectionHandlers` for `<appSettings>` and `<connectionStrings>` because they
>  are by far the most commonly used "key/value" config sections. But we introduced the `SectionHandler<T>`
>  API to allow for more sections to be processed.
>
>  We don't currently feel that there are any other sections out there that have enough demand to
>  warrant including a default section handler in the base package that everybody is required to use.
>  That does not mean that section handlers for other sections is not ever a valid scenario, and you
>  are of course welcome and encouraged to leverage the section handler feature if it suits your needs.
>  That is why we introduced the feature afterall.
</details>

<a name="applicationsettings"></a>
<details>
  <summary><b>Can you add a `SectionHandler` for the client `ApplicationSettings` section?</b></summary>
  
>  See [above](#newhandler). `ApplicationSettings` is less commonly used. But more problematically, it
>  isn't really a standard .Net configuration section like it appears to be on first glance. The classes
>  that support ApplicationSettings provide a strict and strongly typed window into what looks like a
>  standard configuration section in your app.config file. While we can easily write a section handler
>  for the `ClientSettingsSection` ([example](../samples/SampleSectionHandlers/ClientSettingsSectionHandler.cs))
>  it won't integrate into the ApplicationSettings framework seamlessly like one might expect. The
>  ApplicationSetting framework has already determined the number and names (including casing, which
>  is problematic in 'Greedy' mode) of all the settings it will present before the base configuration
>  system even gets a crack at reading from the config file. So you can't *add* new values with 'Greedy'
>  mode, and you can't override existing values in 'Greedy' mode if you don't properly match
>  casing - despite the fact that ApplicationSettings is supposed to be case-insensitive.
>
>  If you wish, you can use the [sample section handler](../samples/SampleSectionHandlers/ClientSettingsSectionHandler.cs)
>  to process ApplicationSettings in your application, but know that the use case is rather limited.
>  It will work in 'Strict' mode... and maybe require some prodding to force the ApplicationSettings
>  framework to forget the settings it's seen before and decide to look back into the config file to
>  get new values.
>
>  You can read more about the architecture of the AppliationSettings framework [here](https://docs.microsoft.com/en-us/dotnet/desktop/winforms/advanced/application-settings-architecture?view=netframeworkdesktop-4.8)
>  to see how it builds layers on top of the standard config system that often obscure any changes or
>  additional settings that appear in the `ClientSettingsSection` but won't be seen in
>  `MyApp.Properties.Settings`. That set of articles is also a good starting point for learning
>  about `SettingsProvider` and how that might be leveraged to accomplish configuration injection
>  through a different mechanism in the case when applications must use ApplicationSettings.
</details>

<a name="azureappservices"></a>
<details>
  <summary><b>Do ConfigBuilders break the 'Application Settings' feature of Azure AppServices?</b></summary>
  
>  Maybe a little? It does appear that adding a 'configBuilders' tag to your 'appSettings' or 'connectionStrings'
>  sections confuses the injection logic for the Azure AppServices "Application Settings" feature. I do not
>  have any insight as to why that is other than to say that the two features "grew up" contemporaneously, so
>  they were probably not aware that configBuilders could exist.
>
>  But all is not lost. The "Application Settings" feature injects all it's values into the environment of
>  the service. So while using ConfigBuilders might interfere with the automatic injection of those values,
>  you can also use ConfigBuilders to pull those values back in. See [this comment on issue #133](https://github.com/aspnet/MicrosoftConfigurationBuilders/issues/133#issuecomment-1049520479)
>  for more details.
</details>

<a name="iisschema"></a>
<details>
  <summary><b>Why does IIS/inetmgr complain about configBuilders?</b></summary>
  
>  Because IIS config tools are old and cranky, just like the old .Net config system wanted them to be. :smiling_imp:
>
>  The old .Net config system is supposed to be quite rigid and super-strongly typed. So when IIS developed
>  tools to work with config, they took steps to ensure they didn't break folks by creating invalid configuration.
>  In particular, they decided to use XML schema's to ensure the XML they save is on the up-and-up. (Just
>  like Visual Studio does. But Visual Studio gets updated quite a bit more frequently than IIS tools and
>  has a lower bar for fixing nagging bugs that have a workaround - and was therefore better equipped to
>  change with the times when .Net config added new features and sections. Also, failing schema validation
>  in Visual Studio simply resulted in red squiggles instead of error dialogs. :frowning:)
>
>  The workaround is really quite simple, but it isn't something we can do in these packages. As suggested
>  in #126, simply add a schema file for IIS to help it understand that configBuilders are ok on some
>  sections.
>
>  `%systemroot%\system32\inetsrv\config\schema\configBuilders_schema.xml`
>  ```xml
>  <configSchema>
>    <sectionSchema name="appSettings">
>      <attribute name="configBuilders" type="string"/>
>    </sectionSchema>
>    <sectionSchema name="connectionStrings">
>      <attribute name="configBuilders" type="string"/>
>    </sectionSchema>
>  </configSchema>
>  ```

</details>

<a name="windowscontainers"></a>
<details>
  <summary><b>My config builder isn't working in my Windows container.</b></summary>
  
>  That's a statement, not a question. But here's a likely explanation.
>
>  Windows containers only modify the environment block of the EntryPoint process. So if your application
>  is running as a service (like IIS/ASP.Net apps) or some other process not directly created by the
>  EntryPoint, any environment variables set when starting the container will not be visible to your
>  app.
>
>  To work around this issue, [ASP.Net](https://github.com/microsoft/dotnet-framework-docker/tree/main/src/aspnet)
>  and [IIS](https://github.com/microsoft/iis-docker) container images rely on a `ServiceMonitor.exe`
>  utility to be the entry point for the container, and this utility proactively modifies the environment
>  of the worker process with any additional environment variables passed to docker run.
>
>  For IIS/ASP.Net workloads, do try to use an IIS/ASP.Net derived container that uses `ServiceMonitor.exe.`
>  For other workloads, try making your app the EntryPoint, or try a similar approach to how IIS/ASP.Net
>  handle this... possibly even leveraging [ServiceMonitor.exe](https://github.com/Microsoft/IIS.ServiceMonitor)
>  itself.
</details>

<a name="vstyperes"></a>
<details>
  <summary><b>Why do I get an error in Visual Studio when I switch my web app to use IIS instead of IISExpress?</b></summary>
  
>  Many reasons. The gist of the situation is this... When you switch your web application to run in IIS
>  instead of IISExpress, Visual Studio tries to read your config file to parse connection strings. I
>  believe it's looking for 'LocalDB', but that's not really important. Your web app's config file is
>  obviously not part of the process configuration for devenv.exe, so VS opens it via
>  `ConfigurationManager.OpenConfiguration()` or something similar. Prior to V3, this was likely to
>  result in failures in many of these key/value config builders if they were applied to the
>  `<connectionStrings>` section.
>
>  In V3, we handle the `ConfigurationManager.OpenConfiguration()` scenario better, but we can still
>  get tripped up by the insanely complicated way Visual Studio manages reference binding. As a result,
>  there may be version mis-matches when trying to load some builders. The Azure builders seem particularly
>  vulnerable to this. I haven't found a good way to deal with this.
>
>  **However,** even though the error appears in a scary dialog box, it does not affect the behavior of
>  your application. When running/debugging your app on local IIS, the config builders are still able to
>  execute as expected.
</details>
