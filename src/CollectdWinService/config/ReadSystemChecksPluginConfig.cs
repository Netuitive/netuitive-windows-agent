using System;
using System.Configuration;
using System.Xml;

namespace BloombergFLP.CollectdWin
{
    public enum CheckType
    {
        Service,
        Process,
        Port
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

        [ConfigurationProperty("HeartbeatTTLMultiplier", IsRequired = false, DefaultValue = 2.0)]
        public double HeartbeatTTLMultiplier
        {
            get { return (double)base["HeartbeatTTLMultiplier"]; }
            set { base["HeartbeatTTLMultiplier"] = value; }
        }
    }

    public class SystemChecksCollection : ConfigurationElementCollection
    {

        protected override ConfigurationElement CreateNewElement()
        {
            return null;
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var checkConfig = (SystemCheckConfig)element;
            return (checkConfig.GetType().Name + "_" + checkConfig.Name);
        }

        protected override ConfigurationElement CreateNewElement(string elementName)
        {
            switch (elementName)
            {
                case "PortCheck":
                    return new PortCheckConfig();
                case "ServiceCheck":
                    return new ServiceCheckConfig();
                case "ProcessCheck":
                    return new ProcessCheckConfig();
                case "HttpCheck":
                    return new HttpCheckConfig();
            }

            throw new ConfigurationErrorsException("Unregognised check type: " + elementName);
        }

        protected override bool OnDeserializeUnrecognizedElement(string elementName, XmlReader reader)
        {
            if (elementName.Equals("PortCheck") || elementName.Equals("ServiceCheck") || elementName.Equals("ProcessCheck") || elementName.Equals("HttpCheck"))
            {
                var element = (SystemCheckConfig)CreateNewElement(elementName);
                element.Deserialize(reader);
                BaseAdd(element);

                return true;
            }

            return base.OnDeserializeUnrecognizedElement(elementName, reader);
        }

    }

    public class ServiceCheckConfig : SystemCheckConfig {
        [ConfigurationProperty("UseRegex", IsRequired = false, DefaultValue = false)]
        public bool UseRegex
        {
            get { return (bool)base["UseRegex"]; }
            set { base["UseRegex"] = value; }
        }

        public override String Name
        {
            get
            {
                if (this.UseRegex)
                    return (string)base["Name"];
                else
                    return "^" + (string)base["Name"] + "$";
            }
            set { base["Name"] = value; }
        }
    }

    public class ProcessCheckConfig : SystemCheckConfig {
        [ConfigurationProperty("UseRegex", IsRequired = false, DefaultValue = false)]
        public bool UseRegex
        {
            get { return (bool)base["UseRegex"]; }
            set { base["UseRegex"] = value; }
        }

        public override String Name
        {
            get
            {
                if (this.UseRegex)
                    return (string)base["Name"];
                else
                    return "^" + (string)base["Name"] + "$";
            }
            set { base["Name"] = value; }
        }
    }

    public class PortCheckConfig : SystemCheckConfig
    {
        [ConfigurationProperty("Host", IsRequired = false, DefaultValue="localhost")]
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

        public override String Name
        {
            get {return (string)base["Name"];}
            set { base["Name"] = value; }
        }

    }

    public class HttpCheckConfig : SystemCheckConfig
    {
        [ConfigurationProperty("Url", IsRequired = true)]
        public String Url
        {
            get { return (string)base["Url"]; }
            set { base["Url"] = value; }
        }

        public override String Name
        {
            get { return (string)base["Name"]; }
            set { base["Name"] = value; }
        }

        [ConfigurationProperty("AuthHeader", IsRequired = false, DefaultValue = "")]
        public String AuthHeader
        {
            get { return (string)base["AuthHeader"]; }
            set { base["AuthHeader"] = value; }
        }

        [ConfigurationProperty("StatusMatches", IsRequired = false, DefaultValue = "2..")]
        public String StatusMatches
        {
            get { return (string)base["StatusMatches"]; }
            set { base["StatusMatches"] = value; }
        }
    }

    public abstract class SystemCheckConfig : ConfigurationElement
    {
        [ConfigurationProperty("Name", IsRequired = true)]
        public abstract String Name
        {
            get;
            set;
        }

        [ConfigurationProperty("Alias", IsRequired = false)]
        public String Alias
        {
            get { return (string)base["Alias"]; }
            set { base["Alias"] = value; }
        }

        [ConfigurationProperty("TTLMultiplier", IsRequired = false, DefaultValue = 1.2)]
        public double IntervalMultiplier
        {
            get { return (double)base["TTLMultiplier"]; }
            set { base["TTLMultiplier"] = value; }
        }

        internal void Deserialize(XmlReader reader)
        {
            base.DeserializeElement(reader, false);
        }

        public int GetTTL(int interval)
        {
            return (int)Math.Ceiling(Math.Max(1.0, IntervalMultiplier * interval));
        }

    }

}
