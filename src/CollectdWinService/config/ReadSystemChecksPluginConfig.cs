using System;
using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    public enum CheckType
    {
        Service,
        Process
    };

    public sealed class ReadSystemChecksPluginConfig : CollectdPluginConfig
    {
        [ConfigurationProperty("Checks", IsRequired = false)]
        [ConfigurationCollection(typeof(SystemChecksCollection), AddItemName = "Check")]
        public SystemChecksCollection Checks
        {
            get { return (SystemChecksCollection)base["Checks"]; }
            set { base["Checks"] = value; }
        }

        [ConfigurationProperty("EnableAgentHeartbeat", IsRequired = true)]
        public Boolean EnableAgentHeartbeat
        {
            get { return (Boolean)base["EnableAgentHeartbeat"]; }
            set { base["EnableAgentHeartbeat"] = value; }
        }

        [ConfigurationProperty("HeartbeatIntervalMultiplier", IsRequired = false, DefaultValue = 1)]
        public int HeartbeatIntervalMultiplier
        {
            get { return (int)base["HeartbeatIntervalMultiplier"]; }
            set { base["HeartbeatIntervalMultiplier"] = value; }
        }
    }

    public sealed class SystemChecksCollection : ConfigurationElementCollection
    {

        protected override ConfigurationElement CreateNewElement()
        {
            return new SystemCheckConfig();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var checkConfig = (SystemCheckConfig)element;
            return (checkConfig.Type + "_" + checkConfig.Name);
        }
    }

    public sealed class SystemCheckConfig : ConfigurationElement
    {
        [ConfigurationProperty("Name", IsRequired = true)]
        public String Name
        {
            get { return (string)base["Name"]; }
            set { base["Name"] = value; }
        }

        [ConfigurationProperty("Type", IsRequired = true)]
        public CheckType Type
        {
            get { return (CheckType)base["Type"]; }
            set { base["Type"] = value; }
        }

        [ConfigurationProperty("Alias", IsRequired = false)]
        public String Alias
        {
            get { return (string)base["Alias"]; }
            set { base["Alias"] = value; }
        }

        [ConfigurationProperty("IntervalMultiplier", IsRequired = false, DefaultValue = 1)]
        public int IntervalMultiplier
        {
            get { return (int)base["IntervalMultiplier"]; }
            set { base["IntervalMultiplier"] = value; }
        }

        [ConfigurationProperty("UseRegex", IsRequired = false, DefaultValue = false)]
        public bool UseRegex
        {
            get { return (bool)base["UseRegex"]; }
            set { base["UseRegex"] = value; }
        }

    }

}
