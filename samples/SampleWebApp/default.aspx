<% @Page Language="C#" %>
<% @Import Namespace="System.Configuration" %>
<% @Import Namespace="System.Web.Configuration" %>

<script runat="server">

    void Page_Load() {

        Response.Write("<table border=1><tr><th colspan=2><h2>Application Settings</h2></th></tr>");
        foreach (string appsetting in WebConfigurationManager.AppSettings.Keys)
        {
            Response.Write("<tr><td>" + HttpUtility.HtmlEncode(appsetting) + "</td>");
            Response.Write("<td>" + HttpUtility.HtmlEncode(WebConfigurationManager.AppSettings[appsetting]) + "</td></tr>");
        }
        Response.Write("</table><br/><br/>");

        Response.Write("<table border=1><tr><th colspan=2><h2>Connection Strings</h2></th></tr>");
        foreach (ConnectionStringSettings cs in WebConfigurationManager.ConnectionStrings) {
            Response.Write("<tr><td>" + HttpUtility.HtmlEncode(cs.Name) + "</td><td>" + HttpUtility.HtmlEncode(cs.ConnectionString) + "</td></tr>");
        }
        Response.Write("</table><br/><br/>");

        Response.Write("<table border=1><tr><th colspan=2><h2>Custom Settings Section</h2></th></tr>");
        var customSettings = WebConfigurationManager.GetSection("customSettings") as NameValueCollection;
        foreach (var configKey in customSettings.AllKeys) {
            Response.Write("<tr><td>" + HttpUtility.HtmlEncode(configKey) + "</td><td>" + HttpUtility.HtmlEncode(customSettings[configKey]) + "</td></tr>");
        }
        Response.Write("</table><br/><br/>");
    }

</script>

<% Response.Write("hello world! - " + DateTime.Now.ToString()); %>
