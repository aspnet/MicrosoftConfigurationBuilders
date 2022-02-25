<% @Page Language="C#" %>
<% @Import Namespace="System.Configuration" %>
<% @Import Namespace="System.Web.Configuration" %>

<script runat="server">

    void Page_Load() {
        string regulartd = "<td>{0}</td>";
        string boldtd = "<td bgcolor='yellow'><b>{0}</b></td>";
        bool isMatch;
        bool azureEnabled = (WebConfigurationManager.AppSettings["AzureBuildersEnabled"] == "enabled")
                            || (WebConfigurationManager.AppSettings["AzureBuildersEnabled"] == "true")
                            || (WebConfigurationManager.AppSettings["AzureBuildersEnabled"] == "optional");

        var expectedAppsettings = GetExpectedAppSettings(azureEnabled);
        Response.Write("<table border=1><tr><th colspan=3><h2>Application Settings</h2></th></tr>");
        Response.Write("<tr><th></th><th>Actual</th><th>Expected</th></tr>");
        foreach (string appsetting in WebConfigurationManager.AppSettings.Keys)
        {
            Response.Write("<tr><td>" + HttpUtility.HtmlEncode(appsetting) + "</td>");
            Response.Write("<td>" + HttpUtility.HtmlEncode(WebConfigurationManager.AppSettings[appsetting]) + "</td>");
            if (expectedAppsettings.ContainsKey(appsetting))
            {
                isMatch = expectedAppsettings[appsetting] == WebConfigurationManager.AppSettings[appsetting];
                Response.Write(String.Format(isMatch ? regulartd : boldtd, expectedAppsettings[appsetting]) + "</tr>");
                expectedAppsettings.Remove(appsetting);
            }
            else
                Response.Write(String.Format(boldtd, "<No Value Expected>"));
        }
        foreach (var leftoversetting in expectedAppsettings)
            if (leftoversetting.Value != null)
                Response.Write("<tr><td>" + HttpUtility.HtmlEncode(leftoversetting.Key) + "</td><td></td>" + String.Format(boldtd, leftoversetting.Value) + "</tr>");
        Response.Write("</table><br/><br/>");

        Response.Write("<table border=1><tr><th colspan=2><h2>AppConfigTest</h2></th></tr>");
        var appConfigTestSection = WebConfigurationManager.GetSection("appConfigTest") as NameValueCollection;
        foreach (var configKey in appConfigTestSection.AllKeys) {
            Response.Write("<tr><td>" + HttpUtility.HtmlEncode(configKey) + "</td><td>" + HttpUtility.HtmlEncode(appConfigTestSection[configKey]) + "</td></tr>");
        }
        Response.Write("</table><br/><br/>");

        Response.Write("<table border=1><tr><th colspan=3><h2>Connection Strings</h2></th></tr>");
        Response.Write("<tr><th></th><th>Actual</th><th>Expected</th></tr>");
        foreach (ConnectionStringSettings cs in WebConfigurationManager.ConnectionStrings) {
            Response.Write("<tr><td>" + HttpUtility.HtmlEncode(cs.Name) + "</td><td>" + HttpUtility.HtmlEncode(cs.ConnectionString) + "</td>");
            if (expectedConnectionStrings.ContainsKey(cs.Name))
            {
                isMatch = expectedConnectionStrings[cs.Name] == cs.ConnectionString;
                Response.Write(String.Format(isMatch ? regulartd : boldtd, expectedConnectionStrings[cs.Name]) + "</tr>");
                expectedConnectionStrings.Remove(cs.Name);
            }
            else
                Response.Write(String.Format(boldtd, "<No Value Expected>"));
        }
        foreach (var leftoversetting in expectedConnectionStrings)
            if (leftoversetting.Value != null)
                Response.Write("<tr><td>" + HttpUtility.HtmlEncode(leftoversetting.Key) + "</td><td></td>" + String.Format(boldtd, leftoversetting.Value) + "</tr>");
        Response.Write("</table><br/><br/>");

        Response.Write("<table border=1><tr><th colspan=2><h2>Environment Variables</h2></th></tr>");
        foreach (DictionaryEntry ev in System.Environment.GetEnvironmentVariables()) {
            Response.Write("<tr><td>" + HttpUtility.HtmlEncode(ev.Key) + "</td><td>" + HttpUtility.HtmlEncode(ev.Value) + "</td></tr>");
        }
        Response.Write("</table><br/><br/>");
    }

    public Dictionary<string, string> GetExpectedAppSettings(bool azureEnabled)
    {
        return new Dictionary<string, string>()
        {
            { "AzureBuildersEnabled", "disabled"},
            { "app~Settings_Colon-and$friends@super+duper,awesome#cool:Test.", "optional" },
            { "Optional", "optional" },
            { "Value_Replaced_By_Environment_In_Token_Mode", @"C:\WINDOWS" },
            { "Key_Replaced_By_Windows_NT_Environment_In_Token_Mode", @"Should be Windows_NT or similar. May need to update per machine." },
            { "ARCHITECTURE", "Will be replaced by 'Environment' in Strict/Greedy modes IFF prefix='PROCESSOR_' AND stripPrefix='true'" },
            { "Value_Replaced_By_Json_In_Token_Mode", "${jsonSetting1}" },
            { "jsonSubSetting2", "Will be replaced by 'Json' in 'Sectional' jsonMode." },
            { "jsonInteger", "Will be replaced by 'Json' in 'Sectional' jsonMode." },
            { "jsonSub:subSetting3", "Will be replaced by 'Json' in 'Sectional' jsonMode." },
            { "jsonConnectionString2", "WILL NOT be replaced by 'Json' in 'Sectional' jsonMode." },
            { "jsonCustomSetting1", "Will be replaced by 'Json' with prefix='customSettings:' and stripPrefix='true'." },
            { "ignore.secretfromfile3", "This value should be left alone." },
            { "WINDIR", "C:\\WINDOWS" },
            { "PROCESSOR_ARCHITECTURE", "x86" },
            { "ConfigBuilderTestKeyVaultName", "MSConfigBuilderTestVault" },
            { "AppConfigTestEndpoint", "https://smolloy-appconfigtest.azconfig.io" },
            { "Boolean", "true" },
            { "usersecret2", "secretbar" },
            { "usersecret1", "secretfoo" },
            { "JSONConfigFile", "~/App_Data/settings.json" },
            { "connectionString1", "secretConnectionString" },
            { "SYSTEMDRIVE", "X:" },
            { "jsonSetting1", "From Json Root" },
            { "appSettings:jsonSubSetting2", "From AppSettings Json object" },
            { "appSettings:jsonInteger", "42" },
            { "jsonConnectionString1", "CS From Json Root" },
            { "SecretFromFile1", "I might have been put here by a container orchestrator." },
            { "SECRETFROMFILE2", "I am definitely just a test secret." },
            { "SubFeature--FeatureSecretA", "This might be a subfeature setting." },
            { "appConfigTest2", azureEnabled ? "V2 from KeyVault" : null },
            { "Secret3", azureEnabled ? "Latest3" : null },
            { "appConfigTest1", azureEnabled ? "V1 from KeyVault" : null},
            { "Secret1", azureEnabled ? "Latest1" : "Last writer wins. KV1:Latest1, KV2:First1, KV3:Latest1, (KV4-untouched because it's version doesn't exist for this secret.)" },
            { "Integration-AppSetting-ApiScopeSecret", azureEnabled ? "This is a secret scope API" : null },
            { "Integration-ConnectionString-TestCS", azureEnabled ? "This is a test connection string" : null },
            { "Secret2", azureEnabled ? "Second2" : null },
            { "Secret2/63e11b20b386418d81618da23482534e", azureEnabled ? null : "KV1 and KV3 get one chance to update this to 'Secret2:First2'. KV2 and KV4 don't touch ever due to mismatched versions."},
        };
    }

    Dictionary<string, string> expectedConnectionStrings = new Dictionary<string, string>()
    {
        { "LocalSqlServer", "data source=.\\SQLEXPRESS;Integrated Security=SSPI;AttachDBFilename=|DataDirectory|aspnetdb.mdf;User Instance=true" },
        // In the old 'Expand' mode that manipulated raw XML, this escaped string would become unescaped when reading the xml into the config system.
        //{ "tokenTest", "A & really ' bad \" unescaped < connection > string." },
        // Now it get injected during PCS, so it does not get unescaped when reading in the raw xml.
        { "tokenTest", "A &amp; really &apos; bad &quot; unescaped &lt; connection &gt; string." },
        { "expandTestCS", "Only replaced in Strict/Greedy modes. Not Token." },
        { "jsonConnectionString1", "CS From Json Root" },
        { "connectionStrings:jsonConnectionString1", "CS1 From ConnectionStrings Json object" },
        { "jsonConnectionString2", "Will only be replaced by 'Json' in 'Sectional' jsonMode." },
        { "customSettings:jsonConnectionString2", "CS2 From ConnectionStrings Json object" },
    };

</script>

<% Response.Write("hello world! - " + DateTime.Now.ToString()); %>
