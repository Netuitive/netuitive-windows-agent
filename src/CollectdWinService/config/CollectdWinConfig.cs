using System;
using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    public abstract class CollectdPluginConfig : ConfigurationSection
    {

    }

    public sealed class CollectdWinConfig : CollectdPluginConfig
    {
        [ConfigurationProperty("GeneralSettings", IsRequired = false)]
        public GeneralSettingsConfig GeneralSettings
        {
            get { return (GeneralSettingsConfig)base["GeneralSettings"]; }
            set { base["GeneralSettings"] = value; }
        }

        [ConfigurationProperty("Plugins", IsRequired = true)]
        [ConfigurationCollection(typeof(PluginCollection), AddItemName = "Plugin")]

        public PluginCollection PluginRegistry
        {
            get { return (PluginCollection)base["Plugins"]; }
            set { base["Plugins"] = value; }
        }

        public static CollectdWinConfig GetConfig()
        {
            return (CollectdWinConfig)ConfigurationManager.GetSection("CollectdWinConfig") ?? new CollectdWinConfig();
        }
    }

    public sealed class GeneralSettingsConfig : ConfigurationElement
    {
        [ConfigurationProperty("Interval", IsRequired = true)]
        public int Interval
        {
            get { return (int)base["Interval"]; }
            set { base["Interval"] = value; }
        }

        [ConfigurationProperty("Timeout", IsRequired = true)]
        public int Timeout
        {
            get { return (int)base["Timeout"]; }
            set { base["Timeout"] = value; }
        }

        [ConfigurationProperty("StoreRates", IsRequired = true)]
        public bool StoreRates
        {
            get { return (bool)base["StoreRates"]; }
            set { base["StoreRates"] = value; }
        }

        [ConfigurationProperty("Hostname", IsRequired = false)]
        public string Hostname
        {
            get { return (string)base["Hostname"]; }
            set { base["Hostname"] = value; }
        }
    }

    public class PluginCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new PluginConfig();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (((PluginConfig)element).UniqueId);
        }
    }

    public sealed class PluginConfig : ConfigurationElement
    {
        public PluginConfig()
        {
            UniqueId = Guid.NewGuid();
        }

        internal Guid UniqueId { get; set; }

        [ConfigurationProperty("Name", IsRequired = true)]
        public string Name
        {
            get { return (string)base["Name"]; }
            set { base["Name"] = value; }
        }

        [ConfigurationProperty("Class", IsRequired = true)]
        public string Class
        {
            get { return (string)base["Class"]; }
            set { base["Class"] = value; }
        }

        [ConfigurationProperty("Enable", IsRequired = true)]
        public bool Enable
        {
            get { return (bool)base["Enable"]; }
            set { base["Enable"] = value; }
        }
    }
}