<% @Page Language="C#" %>
<% @Import Namespace="System.Web.Configuration" %>

<script runat="server">

void Page_Load() {
	Response.Write("<table border=1><tr><th colspan=2><h2>Application Settings</h2></th></tr>");
	foreach (string appsetting in WebConfigurationManager.AppSettings.Keys) {
		Response.Write("<tr><td>" + appsetting + "</td><td>" + WebConfigurationManager.AppSettings[appsetting] + "</td></tr>");
	}
	Response.Write("</table><br/><br/>");

	Response.Write("<table border=1><tr><th colspan=2><h2>Connection Strings</h2></th></tr>");
	foreach (ConnectionStringSettings cs in WebConfigurationManager.ConnectionStrings) {
		Response.Write("<tr><td>" + cs.Name + "</td><td>" + cs.ConnectionString + "</td></tr>");
	}
	Response.Write("</table><br/><br/>");

	Response.Write("<table border=1><tr><th colspan=2><h2>Environment Variables</h2></th></tr>");
	foreach (DictionaryEntry ev in System.Environment.GetEnvironmentVariables()) {
		Response.Write("<tr><td>" + ev.Key + "</td><td>" + ev.Value + "</td></tr>");
	}
	Response.Write("</table><br/><br/>");
}

</script>

<% Response.Write("hello world! - " + DateTime.Now.ToString()); %>
