using System;
using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    public sealed class ReadWindowsEventPluginConfig : CollectdPluginConfig
    {
        [ConfigurationProperty("Events", IsRequired = false)]
        [ConfigurationCollection(typeof(WindowsEventCollection), AddItemName = "Event")]
        public WindowsEventCollection Events
        {
            get { return (WindowsEventCollection)base["Events"]; }
            set { base["Events"] = value; }
        }
    }

    public sealed class WindowsEventCollection : ConfigurationElementCollection
    {

        protected override ConfigurationElement CreateNewElement()
        {
            return new WindowsEventConfig();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var eventConfig = (WindowsEventConfig)element;
            return (eventConfig.Log + "_" + eventConfig.Provider + "_" +eventConfig.MaxLevel + "_" + eventConfig.FilterExp);
        }
    }

    public sealed class WindowsEventConfig : ConfigurationElement
    {
        [ConfigurationProperty("Log", IsRequired = true)]
        public String Log
        {
            get { return (string)base["Log"]; }
            set { base["Log"] = value; }
        }

        [ConfigurationProperty("Provider", IsRequired = true)]
        public String Provider
        {
            get { return (string)base["Provider"]; }
            set { base["Provider"] = value; }
        }

        [ConfigurationProperty("MaxLevel", IsRequired = true)]
        public string MaxLevel
        {
            get { return (string)base["MaxLevel"]; }
            set { base["MaxLevel"] = value; }
        }

        [ConfigurationProperty("FilterExp", IsRequired = false, DefaultValue=".*")]
        public string FilterExp
        {
            get { return (string)base["FilterExp"]; }
            set { base["FilterExp"] = value; }
        }
    }

}
