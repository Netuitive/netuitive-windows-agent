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
            return (checkConfig.Name + "_" + checkConfig.Interval);
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

        [ConfigurationProperty("Interval", IsRequired = false, DefaultValue=1)]
        public int Interval
        {
            get { return (int)base["Interval"]; }
            set { base["Interval"] = value; }
        }

    }

}
