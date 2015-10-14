using System;
using System.Configuration;
namespace BloombergFLP.CollectdWin
{
    public sealed class WriteHTTPPluginConfig : CollectdPluginConfig
    {
        [ConfigurationProperty("Url", IsRequired = true)]
        public String Url
        {
            get { return (string)base["Url"]; }
            set { base["Url"] = value; }
        }
    }

}
