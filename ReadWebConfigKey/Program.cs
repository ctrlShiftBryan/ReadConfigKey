using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace ReadWebConfigKey
{
    class Program
    {
        private static string[] _args;

        private static string Comment { get; set; }
        private static string WebConfig { get; set; }
        private static bool CommitChanges { get; set; }
        private static bool Force { get; set; }
        private static string SectionName { get; set; }
        private static string Key { get; set; }
        private static bool ReadOnly { get; set; }
        private static bool PushChanges { get; set; }

        private static bool WebConfigVersionNeedsUpdated { get; set; }

        static void Main(string[] args)
        {
            _args = args;
            ParseParameters();

            //find the web config
            var webConfig = "";
            webConfig = GetWebConfig();

            //get the current version in web config
            var webConfigVersion = ReadWebConfig(webConfig);

            //get the git version # for the git repo
            var versionInt = GetGitVersion();

            if (webConfigVersion == null || webConfigVersion != versionInt)
            {
                WebConfigVersionNeedsUpdated = true;
                versionInt = versionInt + 1;
            }

            var version = versionInt.ToString();

            if ((!ReadOnly && WebConfigVersionNeedsUpdated) || Force)
            {
                Console.WriteLine("Updating {0} version from {1} to {2}", webConfig, webConfigVersion, version);
                UpdateWebConfig(webConfig, version);
            }
               
            if (CommitChanges && WebConfigVersionNeedsUpdated)
            {
                ExecuteCommands(new[] {
                    String.Format("git add {0}", webConfig), 
                    String.Format("git commit -m\"{0} {1}\"", Comment, version)
                });

                if (PushChanges)
                {
                    ExecuteCommands(new[] { "git push" });
                }
            }
        }

        private static void ParseParameters()
        {
            Comment = GetPassedInParameter("Comment", "Updating config version to");
            WebConfig = GetPassedInParameter("WebConfig", "");
            CommitChanges = GetPassedInParameter("CommitChanges", "").ToLower() == "true";
            SectionName = GetPassedInParameter("SectionName", "appSettings");
            Key = GetPassedInParameter("Key", "BuildVersion");
            Force = GetPassedInParameter("Force", "").ToLower() == "true";
            ReadOnly = GetPassedInParameter("ReadOnly", "true").ToLower() == "true";
            PushChanges = GetPassedInParameter("PushChanges", "true").ToLower() == "true";
        }

        private static void ExecuteCommands(string[] commands)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var process = Process.Start(startInfo);
            using (StreamWriter sw = process.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    foreach (var command in commands)
                    {
                        sw.WriteLine(command);
                    }
                    sw.Close();
                }
            }

            using (StreamReader reader = process.StandardOutput)
            {
                string result = reader.ReadToEnd();
                Console.Write(result);
            }
        }

        private static string GetPassedInParameter(string parameter, string defaultValue)
        {
            var parameterString = String.Format("/p:{0}=", parameter);
            var passedInComment = _args.Where(x => x.Contains(parameterString));
            if (passedInComment.Any())
            {
                var result = passedInComment.First().Replace(parameterString, "");
                return result;
            }

            return defaultValue;
        }

        private static void UpdateWebConfig(string webConfig, string version)
        {
            var configFile = GetConfigFile(webConfig);


            var customSection = (AppSettingsSection)configFile.GetSection(SectionName);

            //add the version info to the web config
            if (customSection == null)
            {
                customSection = new AppSettingsSection();
                customSection.Settings.Add(Key, version);
                configFile.Sections.Add(SectionName, customSection);
            }
            else
            {
                var dateTime = DateTime.Now.ToString();

                var webConfigString = String.Format("{0} - {1}", version, dateTime);

                //build version
                if (customSection.Settings[Key] == null)
                {
                    customSection.Settings.Add(Key, webConfigString);
                }
                else
                {
                    customSection.Settings[Key].Value = webConfigString;
                }
            }
            configFile.Save(ConfigurationSaveMode.Modified);
        }

        private static int? ReadWebConfig(string webConfig)
        {
            var configFile = GetConfigFile(webConfig);

            var customSection = (AppSettingsSection)configFile.GetSection(SectionName);

            //add the version info to the web config
            if (customSection != null)
            {
                var fullKey = customSection.Settings[Key].Value;
                if (fullKey.Contains('-'))
                {
                    var parts = fullKey.Split('-');
                    if (ReadOnly)
                    Console.WriteLine(parts[0]);
                    
                    int value;
                    int.TryParse(parts[0], out value);
                    return value;

                }
                else
                {
                    if (ReadOnly)
                    Console.WriteLine("key {0} not found in section {1} of {2}", Key, SectionName, WebConfig);
                }
            }

            return null;
        }

        private static Configuration GetConfigFile(string webConfig)
        {
            var configFile = ConfigurationManager.OpenMappedExeConfiguration(new ExeConfigurationFileMap()
            {
                ExeConfigFilename = webConfig 
            }, ConfigurationUserLevel.None);
            return configFile;
        }

        private static int GetGitVersion()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = " rev-list --count HEAD",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var process = new Process { StartInfo = startInfo };
            process.Start();
            var version = process.StandardOutput.ReadLine();
            int versionInt = 0;
            int.TryParse(version, out versionInt);
            return versionInt;
        }

        private static string GetWebConfig()
        {
            var webConfig = WebConfig;
            var path = Environment.CurrentDirectory;
            //find the first web.config
            foreach (string file in Directory.EnumerateFiles(path, "web.config", SearchOption.AllDirectories).Take(1))
            {
                webConfig = file;
            }
            return webConfig;
        }
    }
}
