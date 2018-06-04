// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// Utility methods commonly used by KeyValueConfigBuilders. 
    /// </summary>
    public class Utils
    {
        private static bool? s_isAspNet = null;
        private static Type s_hostingEnvironmentType = null;

        /// <summary>
        /// Returns the physical file path that corresponds to the specified relative path. 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string MapPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                return path;

            // Use Server.MapPath in ASP.Net
            if (IsAspNet)
                return ServerMapPath(path);

            // Special case a '~' at the start
            if (path.StartsWith(@"~/") || path.StartsWith(@"~\"))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.Substring(2));
            else
                path = Path.GetFullPath(path);

            return path;
        }

        private static bool IsAspNet
        {
            get
            {
                if (s_isAspNet != null)
                    return (bool)s_isAspNet;

                // Is System.Web already loaded? If not, we're not in Asp.Net.
                Assembly mscorlib = Assembly.GetAssembly(typeof(string));
                string systemWebName = mscorlib.FullName.Replace("mscorlib", "System.Web");
                Assembly systemWeb = null;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.FullName == systemWebName)
                    {
                        systemWeb = a;
                        break;
                    }
                }
                if (systemWeb == null)
                {
                    s_isAspNet = false;
                    return (bool)s_isAspNet;
                }

                // Get HostingEnvironment
                s_hostingEnvironmentType = systemWeb.GetType("System.Web.Hosting.HostingEnvironment");
                if (s_hostingEnvironmentType == null)
                {
                    s_isAspNet = false;
                    return (bool)s_isAspNet;
                }

                // Check HostingEnvironment.IsHosted
                PropertyInfo isHosted = s_hostingEnvironmentType.GetProperty("IsHosted", BindingFlags.Public | BindingFlags.Static);
                if ((isHosted == null) || !(bool)isHosted.GetValue(null))
                {
                    s_hostingEnvironmentType = null;
                    s_isAspNet = false;
                    return (bool)s_isAspNet;
                }

                // If we got here, System.Web is loaded, and HostingEnvironment.IsHosted is true. Yay Asp.Net!
                s_isAspNet = true;
                return (bool)s_isAspNet;
            }
        }

        private static string ServerMapPath(string path)
        {
            if (s_hostingEnvironmentType == null)
                return path;

            MethodInfo mapPath = s_hostingEnvironmentType.GetMethod("MapPath", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(string) }, null);
            if (mapPath == null)
                return path;

            return (string)mapPath.Invoke(null, new object[] { path });
        }
    }
}
