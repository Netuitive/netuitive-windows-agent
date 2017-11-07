using System;
using System.Configuration;

namespace BloombergFLP.CollectdWin
{
    public sealed class ReadWindowsTagsPluginConfig : CollectdPluginConfig
    {
        [ConfigurationProperty("Tags", IsRequired = false)]
        [ConfigurationCollection(typeof(TagsCollection), AddItemName = "Tag")]
        public TagsCollection Tags
        {
            get { return (TagsCollection)base["Tags"]; }
            set { base["Tags"] = value; }
        }

    }

    public class TagsCollection : ConfigurationElementCollection
    {

        protected override ConfigurationElement CreateNewElement()
        {
            return new TagConfig();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var tag = (TagConfig)element;
            return (tag.Name + "_" + tag.Value);
        }
    }
    
    public sealed class TagConfig : ConfigurationElement
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
