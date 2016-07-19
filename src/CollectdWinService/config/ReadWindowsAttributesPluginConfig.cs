using System;
using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    public sealed class ReadWindowsAttributesPluginConfig : CollectdPluginConfig
    {
        [ConfigurationProperty("EnvironmentVariables", IsRequired = false)]
        [ConfigurationCollection(typeof(WindowsEnvironmentVariableCollection), AddItemName = "EnvironmentVariable")]
        public WindowsEnvironmentVariableCollection EnvironmentVariables
        {
            get { return (WindowsEnvironmentVariableCollection)base["EnvironmentVariables"]; }
            set { base["EnvironmentVariables"] = value; }
        }

        [ConfigurationProperty("ReadEC2InstanceMetadata", IsRequired = false, DefaultValue = false)]
        public Boolean ReadEC2InstanceMetadata
        {
            get { return (Boolean)base["ReadEC2InstanceMetadata"]; }
            set { base["ReadEC2InstanceMetadata"] = value; }
        }

        [ConfigurationProperty("ReadIPAddress", IsRequired = false, DefaultValue = true)]
        public Boolean ReadIPAddress
        {
            get { return (Boolean)base["ReadIPAddress"]; }
            set { base["ReadIPAddress"] = value; }
        }

    }

    public class WindowsEnvironmentVariableCollection : ConfigurationElementCollection
    {

        protected override ConfigurationElement CreateNewElement()
        {
            return new EnvironmentVariableConfig();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var envVariableConfig = (EnvironmentVariableConfig)element;
            return (envVariableConfig.Name + "_" + envVariableConfig.Value);
        }
    }
    
    public sealed class EnvironmentVariableConfig : ConfigurationElement
    {
        [ConfigurationProperty("Name", IsRequired = true)]
        public String Name
        {
            get { return (string)base["Name"]; }
            set { base["Name"] = value; }
        }

        [ConfigurationProperty("Value", IsRequired = true)]
        public String Value
        {
            get { return (string)base["Value"]; }
            set { base["Value"] = value; }
        }

    }

}
