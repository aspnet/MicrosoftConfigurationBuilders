// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Configuration;
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
        /// <param name="configSection"></param>
        /// <returns></returns>
        public static string MapPath(string path, ConfigurationSection configSection = null)
        {
            if (String.IsNullOrWhiteSpace(path))
                return path;

            // Use 'as is' if the path is rooted.
            if (Path.IsPathRooted(path))
                return path;

            // First try Server.MapPath in ASP.Net
            if (IsAspNet)
            {
                try
                {
                    return ServerMapPath(path);
                }
                catch (Exception) { }
            }

            // Special case a '~' at the start should always use AppDomain.BaseDirectory
            if (path.StartsWith(@"~/") || path.StartsWith(@"~\"))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.Substring(2));

            // Otherwise, non "rooted" paths should try to be relative to the config file if possible
            string configFile = configSection?.ElementInformation?.Source;
            string root = (configFile != null) ? Path.GetDirectoryName(configFile) : AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(root, path));
        }

        private static bool IsAspNet
        {
            get
            {
                if (s_isAspNet != null)
                    return (bool)s_isAspNet;

                // Is System.Web already loaded? If not, we're not in Asp.Net.
                Assembly systemWeb = null;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.FullName.StartsWith("System.Web,"))
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
