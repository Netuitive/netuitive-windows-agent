using System;
using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    public sealed class WriteNetuitivePluginConfig : CollectdPluginConfig
    {
        [ConfigurationProperty("Url", IsRequired = true)]
        public String Url
        {
            get { return (string)base["Url"]; }
            set { base["Url"] = value; }
        }

        [ConfigurationProperty("Location", IsRequired = false)]
        public String Location
        {
            get { return (string)base["Location"]; }
            set { base["Location"] = value; }
        }

        [ConfigurationProperty("Type", IsRequired = false)]
        public String Type
        {
            get { return (string)base["Type"]; }
            set { base["Type"] = value; }
        }
        [ConfigurationProperty("PayloadSize", IsRequired = false)]
        public int PayloadSize
        {
            get { return (int)base["PayloadSize"]; }
            set { base["PayloadSize"] = value; }
        }

    }
}
