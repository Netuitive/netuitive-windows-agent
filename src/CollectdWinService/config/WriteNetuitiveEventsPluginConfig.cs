using System;
using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    public sealed class WriteNetuitiveEventsPluginConfig : CollectdPluginConfig
    {
        [ConfigurationProperty("Url", IsRequired = true)]
        public String Url
        {
            get { return (string)base["Url"]; }
            set { base["Url"] = value; }
        }

        [ConfigurationProperty("PayloadSize", IsRequired = false, DefaultValue=25)]
        public int PayloadSize
        {
            get { return (int)base["PayloadSize"]; }
            set { base["PayloadSize"] = value; }
        }

        [ConfigurationProperty("MaxLength", IsRequired = false, DefaultValue = 100)]
        public int MaxLength
        {
            get { return (int)base["MaxLength"]; }
            set { base["MaxLength"] = value; }
        }
    }
}
