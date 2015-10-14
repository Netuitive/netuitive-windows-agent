using System;
using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    public sealed class WriteStatsdPluginConfig : CollectdPluginConfig
    {
        [ConfigurationProperty("Host", IsRequired = true)]
        public String Host
        {
            get { return (string)base["Host"]; }
            set { base["Host"] = value; }
        }
        [ConfigurationProperty("Port", IsRequired = true)]
        public int Port
        {
            get { return (int)base["Port"]; }
            set { base["Port"] = value; }
        }

        [ConfigurationProperty("Prefix", IsRequired = false)]
        public String Prefix
        {
            get { return (string)base["Prefix"]; }
            set { base["Prefix"] = value; }
        }
    }

}
