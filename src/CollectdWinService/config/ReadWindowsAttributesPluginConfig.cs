﻿using System;
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
