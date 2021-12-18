using System;
using System.Windows.Forms;
using MapAssist.Settings;
using MapAssist.Types;
using NLog;


namespace MapAssist
{
    public static class Plugin
    {
        private static readonly NLog.Logger _log = NLog.LogManager.GetCurrentClassLogger();

        public static bool _configurationOk = false;
        private static readonly object _lock = new object();

        public static D2ToolboxCore.IComponent NewComponent(uint pid)
        {
            lock (_lock)
            {
                try
                {
                    if (!_configurationOk)
                    {
                        _configurationOk = LoadLoggingConfiguration() && LoadMainConfiguration() && LoadLootLogConfiguration();
                    }
                    if (!_configurationOk)
                    {
                        return null;
                    }

                    return new MapAssist(pid);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static bool LoadMainConfiguration()
        {
            var configurationOk = false;
            try
            {
                MapAssistConfiguration.Load();
                MapAssistConfiguration.Loaded.RenderingConfiguration.InitialSize = MapAssistConfiguration.Loaded.RenderingConfiguration.Size;
                configurationOk = true;
            }
            catch (YamlDotNet.Core.YamlException e)
            {
                _log.Fatal(e);
                _log.Fatal(e, "Invalid yaml for configuration file");

                MessageBox.Show(e.Message, "Yaml parsing error occurred. Invalid MapAssist configuration.",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception e)
            {
                _log.Fatal(e, "Unknown error loading main configuration");

                MessageBox.Show(e.Message, "General error occurred!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return configurationOk;
        }

        private static bool LoadLootLogConfiguration()
        {
            var configurationOk = false;
            try
            {
                LootLogConfiguration.Load();
                Items.LoadLocalization();
                configurationOk = true;
            }
            catch (YamlDotNet.Core.YamlException e)
            {
                _log.Fatal("Invalid loot log yaml file");
                MessageBox.Show(e.Message, "Yaml parsing error occurred. Invalid loot filter configuration.",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception e)
            {
                _log.Fatal(e, $"Unable to initialize Loot Log configuration");
                MessageBox.Show(e.Message, "General error occurred!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return configurationOk;
        }

        private static bool LoadLoggingConfiguration()
        {
            var configurationOk = false;

            try
            {
                var config = new NLog.Config.LoggingConfiguration();

                var logfile = new NLog.Targets.FileTarget("logfile")
                {
                    FileName = "logs\\log.txt",
                    ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.DateAndSequence,
                    ArchiveOldFileOnStartup = true,
                    MaxArchiveFiles = 5
                };
                var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

                // Rules for mapping loggers to targets
                config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
                config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);

                // Apply config
                LogManager.Configuration = config;

                configurationOk = true;
            }
            catch (Exception e)
            {

                MessageBox.Show(e.Message, "General error occurred!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return configurationOk;
        }


    }
}
