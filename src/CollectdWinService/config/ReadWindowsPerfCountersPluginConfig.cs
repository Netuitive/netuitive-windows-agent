using System;
using System.Configuration;

namespace BloombergFLP.CollectdWin
{

    public sealed class ReadWindowsPerfCountersPluginConfig : CollectdPluginConfig
    {
        [ConfigurationProperty("Counters", IsRequired = false)]
        [ConfigurationCollection(typeof(CounterConfigCollection), AddItemName = "Counter")]
        public CounterConfigCollection Counters
        {
            get { return (CounterConfigCollection)base["Counters"]; }
            set { base["Counters"] = value; }
        }

        [ConfigurationProperty("ReloadInterval", IsRequired = false, DefaultValue = 3600)]
        public int ReloadInterval
        {
            get { return (int)base["ReloadInterval"]; }
            set { base["ReloadInterval"] = value; }
        }
    }

      public class CounterConfigCollection : ConfigurationElementCollection
        {
            protected override ConfigurationElement CreateNewElement()
            {
                return new CounterConfig();
            }

            protected override object GetElementKey(ConfigurationElement element)
            {
                var counterConfig = (CounterConfig) element;
                return (counterConfig.Category + "_" + counterConfig.Name + "_" + counterConfig.Instance);
            }
        }
        
    public sealed class CounterConfig : ConfigurationElement
        {
            [ConfigurationProperty("Category", IsRequired = true)]
            public string Category
            {
                get { return (string) base["Category"]; }
                set { base["Category"] = value; }
            }

            [ConfigurationProperty("Name", IsRequired = true)]
            public string Name
            {
                get { return (string) base["Name"]; }
                set { base["Name"] = value; }
            }


            [ConfigurationProperty("Instance", IsRequired = false)]
            public string Instance
            {
                get { return (string) base["Instance"]; }
                set { base["Instance"] = value; }
            }

            [ConfigurationProperty("CollectdPlugin", IsRequired = true)]
            public string CollectdPlugin
            {
                get { return (string) base["CollectdPlugin"]; }
                set { base["CollectdPlugin"] = value; }
            }

            [ConfigurationProperty("CollectdPluginInstance", IsRequired = false)]
            public string CollectdPluginInstance
            {
                get { return (string) base["CollectdPluginInstance"]; }
                set { base["CollectdPluginInstance"] = value; }
            }

            [ConfigurationProperty("CollectdType", IsRequired = true)]
            public string CollectdType
            {
                get { return (string) base["CollectdType"]; }
                set { base["CollectdType"] = value; }
            }

            [ConfigurationProperty("CollectdTypeInstance", IsRequired = true)]
            public string CollectdTypeInstance
            {
                get { return (string) base["CollectdTypeInstance"]; }
                set { base["CollectdTypeInstance"] = value; }
            }

            [ConfigurationProperty("Multiplier", IsRequired = false, DefaultValue=1.0)]
            public double Multiplier
            {
                get { return (double)base["Multiplier"]; }
                set { base["Multiplier"] = value; }
            }

            [ConfigurationProperty("DecimalPlaces", IsRequired = false, DefaultValue = -1)]
            public int DecimalPlaces
            {
                get { return (int)base["DecimalPlaces"]; }
                set { base["DecimalPlaces"] = value; }
            }        
        }


}
