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

        [ConfigurationProperty("IntervalMultiplier", IsRequired = false, DefaultValue = 5)]
        public int IntervalMultiplier
        {
            get { return (int)base["IntervalMultiplier"]; }
            set { base["IntervalMultiplier"] = value; }
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
            return (eventConfig.Log + "_" + eventConfig.Source + "_" +eventConfig.MaxLevel + "_" + eventConfig.FilterExp);
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

        [ConfigurationProperty("Source", IsRequired = true)]
        public String Source
        {
            get { return (string)base["Source"]; }
            set { base["Source"] = value; }
        }

        [ConfigurationProperty("MaxLevel", IsRequired = true)]
        public int MaxLevel
        {
            get { return (int)base["MaxLevel"]; }
            set { base["MaxLevel"] = value; }
        }

        [ConfigurationProperty("MinLevel", IsRequired = false, DefaultValue = 1)]
        public int MinLevel
        {
            get { return (int)base["MinLevel"]; }
            set { base["MinLevel"] = value; }
        }

        [ConfigurationProperty("FilterExp", IsRequired = false, DefaultValue=".*")]
        public string FilterExp
        {
            get { return (string)base["FilterExp"]; }
            set { base["FilterExp"] = value; }
        }

        [ConfigurationProperty("Title", IsRequired = true)]
        public string Title
        {
            get { return (string)base["Title"]; }
            set { base["Title"] = value; }
        }

        [ConfigurationProperty("MaxEventsPerCycle", IsRequired = false, DefaultValue=1)]
        public int MaxEventsPerCycle
        {
            get { return (int)base["MaxEventsPerCycle"]; }
            set { base["MaxEventsPerCycle"] = value; }
        }

        [ConfigurationProperty("MinEventId", IsRequired = false, DefaultValue = 0)]
        public int MinEventId
        {
            get { return (int)base["MinEventId"]; }
            set { base["MinEventId"] = value; }
        }

        [ConfigurationProperty("MaxEventId", IsRequired = false, DefaultValue = 65535)]
        public int MaxEventId
        {
            get { return (int)base["MaxEventId"]; }
            set { base["MaxEventId"] = value; }
        }
    }

}
