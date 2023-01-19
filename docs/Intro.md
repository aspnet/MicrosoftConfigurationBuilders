# Configuration Builders In The .Net Framework

Configuration Builders are a new feature of the full .Net Framework, introduced in .Net 4.7.1. You can read a quick overview of the concept in [this blog post](http://jeffreyfritz.com/2017/11/modern-configuration-for-asp-net-4-7-1-with-configurationbuilders/). The concept is pretty simple - the config system instantiates an object that gets a chance to craft configuration while loading each `ConfigurationSection`. The execution of the concept can get a little more complicated though, because of the old multi-layered and strongly-typed nature of the old .Net Framework config system.

This document will attempt to dig a little deeper into the mechanics of `ConfigurationBuilder` in the .Net Framework, in the hopes that it will help understand how the builders in this project work together to construct a more dynamic configuration environment for legacy apps in a modern landscape. The first step is to understand the order of execution when building a `ConfigurationSection`.


## ConfigurationBuilders Order of Execution
This section is about how the .Net Framework uses `ConfigurationBuilder`s to build configuration sections for app consumption. The mechanics of how this all works affects the order in which config builders are applied and what sources of information they have to draw on as well as the look of the underlying config section they are modifying. Indeed, even in this project, features like reading builder parameters from `AppSettings` are potentially constrained by the way the .Net config system works.

Its very easy to think of the .Net configuration system as simply reading the `.config` file that comes with your application. But there is much more involved in this task. To start with, the .Net Framework uses a multi-layered configuration system. Whenever code asks for a `ConfigurationSection`, wether it is directly via `GetSection()` or indirectly through `ConfigurationManager`, the .Net Framework starts by reading the machine-wide configuration in the machine.config file installed in `%WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319\Config`. The process then moves to the next level config file and modifies what it read from machine.config with updates from that next level. And so on and so forth. The process iterates through a well-known chain of config files before returning the resulting accumulation of configuration values from all levels. (There are actually *two* configuration systems in one within the .Net Framework. They are roughly the same with the most noticeable difference being the layers of config files being stacked together. The client configuration system consults user config files and app.exe.config files, while the web configuration system follows a chain of web.config files.) The following diagram demonstrates the high level concept for an ASP.Net application serving a page from a sub-directory.

```
GetSection()      +----------------+            +------------+            +------------+            +------------+
     |            |                |            |            |            |            |            |            |
     | {No CSO*}  |                |   {CSO*}   |   (root)   |   {CSO*}   |   (app)    |   {CSO*}   |  (subdir)  |
     +----------->| machine.config |----------->| web.config |----------->| web.config |----------->| web.config |---> { ConfigurationSection Object* }
                  |                |            |            |            |            |            |            |
                  +----------------+            +------------+            +------------+            +------------+
```

That is a simple explanation of layered config. Even from this simple concept you can see how each layer builds on previous layers... but does not directly influence other layers. Which brings us to the first point wrt config builder execution order. <u>***ConfigurationBuilders execute only in the layer in which they are applied.***</u> This refers to the _application_ of a config builder. The _definition_ of a config builder will carry forward to every layer beyond the one in which it was defined, just like applicationSettings and connectionStrings carry forward to the next layer. But the `configBuilders` tag that is used to apply a config builder is not carried forward. It is executed at each level and then forgotten. The results of the config builder execution are carried forward in the config object passed to the next level.

There is of course more detail within each layer. Let's take a look at what happens in the `(app) web.config` layer in the diagram above:

```
   {ConfigSection}
  (from prev layer)    +------------+  [XML]                 [XML]                 [XML]               
          |            |         B: |---------> {Builder1} ---------> {Builder2} ---------> {Builder3}---+
          |            |            |                                                                    |
          +----------->|   (app)    |<-------------------------------------------------------------------+
                       | web.config |
          +----------->|            |  {CSO*}                {CSO*}                {CSO*}              
          |        A:  |         C: |---------> {Builder1} ---------> {Builder2} ---------> {Builder3}---+
          |            |            |                                                                    |
          |            |     D:     |<-------------------------------------------------------------------+
   Raw XML fragment    +------------+
     only from               |
    (app) web.config         +------------> { ConfigurationSection Object* }
```

The first thing to notice is that this layer takes a `ConfigurationSection` object from the previous layer as input. All *.config* files and config builder output from previous layers are reflected in this object. From here, the first step (A:) taken at this layer is to *read and process* the raw xml for the requested configuration section. If the section is encrypted, it is decrypted here. If the section resides in a different file via the `configSource` attribute, then the raw xml is read from that source. The *last* step of retrieving/processing the raw xml content is passing it through the configuration builder chain. <u>***The builder chain executes in the order that builders appear in the `configBuilders` tag for the section.***</u> The processed xml then returns into the config system where magic is applied to combine it with the `ConfigurationSection` object from the previous layer to produce a new `ConfigurationSection` object that reflects the merged settings. Lastly, this `ConfigurationSection` object is passed through the config builder chain, which executes in the same order as before. The resulting object gets passed on to the next layer if it exists.

One aspect to note is that config builders are instantiated once per appearance in a `configBuilders` tag. This means that if you apply the "same" config builder to both your `<appSettings/>` and `<connectionStrings/>` sections, each section will use a separate instance for processing since each section was tagged with the builder separately. Similarly, if you define an 'Environment' config builder in machine.config and apply it to `<appSettings/>` both there and again in app.exe.config... two separate instances will be created to process configuration at each layer. However, the same instance is used for processing both the raw xml as well as the `ConfigurationSection` object within each layer/section.

Here is a brief pseudo-config example to demonstrate the order of execution:

| machine.config | root web.config | app web.config |
| -------------- | --------------- | -------------- |
| check 1 | check 2 | bananas |
| <pre>&lt;configBuilders&gt;<br/>&nbsp;&nbsp;&lt;add name="machine1" type=MachineA,..." /&gt;<br/>&nbsp;&nbsp;&lt;add name="machine2" type=MachineB,..." /&gt;<br/>&lt;/configBuilders&gt;<br/><br/>&lt;appSettings<br/>&nbsp;&nbsp;&nbsp;configBuilders="machine2, machine1" /&gt;</pre> | <pre>&lt;configBuilders&gt;<br/>&nbsp;&nbsp;&lt;remove name="machine2" /&gt;<br/>&nbsp;&nbsp;&lt;add name="machine2" type=WebTwo,..." /&gt;<br/>&nbsp;&nbsp;&lt;add name="web1" type=WebOne,..." /&gt;<br/>&lt;/configBuilders&gt;<br/><br/>&lt;appSettings<br/>&nbsp;&nbsp;&nbsp;configBuilders="web1" /&gt;</pre> | <pre>&lt;configBuilders&gt;<br/>&nbsp;&nbsp;&lt;add name="web3" type=WebThree,..." /&gt;<br/>&lt;/configBuilders&gt;<br/><br/>&lt;appSettings<br/>&nbsp;&nbsp;&nbsp;configBuilders="web3,machine2,web1" /&gt;</pre> |

With the config layers above, loading the application settings for your web app would result in config builders executing in the following order. (InstanceId is a made up Id just for this table to demonstrate when instances are reused.)

| Row&nbsp;# | BuilderName | Type | Instance&nbsp;# | Data Format | Data Source |
| ---: | ----------- | ---- | ---------: | ----------: | ----------- |
| 1 | machine1 | MachineA | 1 | xml | machine.config |
| 2 | machine2 | MachineB | 2 | xml | xml from row 1 |
| 3 | machine1 | MachineA | 1 | CSO | class defaults<br/>modified by<br/>xml from row 2 |
| 4 | machine2 | MachineB | 2 | CSO | CSO from row 3 |
| 5 | web1 | WebOne | 3 | xml | root web.config |
| 6 | web1 | WebOne | 3 | CSO | CSO from row 4<br/>modified by<br/>xml from row 5 |
| 7 | web3 | WebThree | 4 | xml | app web.config |
| 8 | machine2 | WebTwo | 5 | xml | xml from row 7 |
| 9 | web1 | WebOne | 6 | xml | xml from row 8 |
| 10 | web3 | WebThree | 4 | CSO | CSO from row 6<br/>modified by<br/>xml from row 9 |
| 11 | machine2 | WebTwo | 5 | CSO | CSO from row 10 |
| 12 | web1 | WebOne | 6 | CSO | CSO from row 11 |

The `ConfigurationSection` object that returns from row 12 is the end result of config building.

<a name="recursion"></a>
## Potential For Recursion/Overflow

The discussion above all centers on the loading of a single `ConfigurationSection.` This simple conceptual model of how sections are loaded has served the framework well over the years. But one potential pitfall that arises from the extensibility introduced by *ConfigurationBuilders* is that config sections may not remain neatly separated anymore. In fact, the *KeyValueConfigBuilders* provided in this project offer a feature that explicitly entangles the creation of different config sections by allowing builders to draw upon `appSettings` for input parameters.


When sections are conceptually siloed, there are no concerns about deadlocks or overflows because there are no cycles created when sections remain completely untangled. Traditionally, code that defines a `ConfigurationSection` is encouraged to not use other configuration sections while creating thier own. Creating dependencies across sections can create complications when the order in which the sections get loaded is not determinate. Crossing streams is bad. 

But the 'load from appSettings' feature in this project does just that. We also encourage folks who extend this package to use the coding skills they know to bring in information from external sources - and the classes/methods for doing so could also indirectly load another configuration section... or maybe even try to reload the section that is currently being loaded. The potential for deadlock and/or stack overflow is there if developers are not careful about what their config builders are doing.

Unfortunately, there is not much we can do to prevent or neatly handle this situation. We have [seen it crop up in the past](#61) and it can be difficult to diagnose. Users of config builders should be aware of the possibility they might end up in a recursive loop of sections loading sections. In version 3 of this project there is a new [RecursionGuard](#TODO) feature that is enabled by default. It does not 'fix' or stop the recursion, but throws an exception to explain what's happening. It can optionally be configured to stop the recursion and continue... but using this option could yield non-deterministic results.
