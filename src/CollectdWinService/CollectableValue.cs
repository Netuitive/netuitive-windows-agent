using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BloombergFLP.CollectdWin
{
    internal abstract class CollectableValue
    {
        public string HostName { get; set; }
        public double Timestamp { get; set; }
        public string ElementType { get; set; }
        public string PluginName { get; set; }
        public string PluginInstanceName { get; set; }
        public string TypeName { get; set; }
        public string TypeInstanceName { get; set; }

        public int Interval { get; set; }

        abstract public string getJSON();
    }


    internal class MetricValue: CollectableValue
    {
        private const string MetricJsonFormat =
            @"{{""host"":""{0}"", ""plugin"":""{1}"", ""plugin_instance"":""{2}""," +
            @" ""type"":""{3}"", ""type_instance"":""{4}"", ""time"":{5}, " +
            @" ""dstypes"":[{6}], ""dsnames"":[{7}], ""values"":[{8}]}}";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


        public double[] Values { get; set; }
        public string[] FriendlyNames { get; set; }

        public string Key()
        {
            return (HostName + "." + PluginName + "." + PluginInstanceName + "." + TypeName + "." + TypeInstanceName);
        }

        public MetricValue DeepCopy()
        {
            var other = (MetricValue)MemberwiseClone();
            other.HostName = String.Copy(HostName);
            other.PluginName = String.Copy(PluginName);
            other.PluginInstanceName = String.Copy(PluginInstanceName);
            other.TypeName = String.Copy(TypeName);
            other.TypeInstanceName = String.Copy(TypeInstanceName);
            other.Values = (double[])Values.Clone();
            return (other);
        }


        override public string getJSON() 
        {
            IList<DataSource> dsList = DataSetCollection.Instance.GetDataSource(TypeName);
            var dsNames = new List<string>();
            var dsTypes = new List<string>();
            if (dsList == null)
            {
                Logger.Debug("Invalid type : {0}, not found in types.db", TypeName);
            }
            else
            {
                foreach (DataSource ds in dsList)
                {
                    dsNames.Add(ds.Name);
                    dsTypes.Add(ds.Type.ToString().ToLower());
                }
            }
            string dsTypesStr = string.Join(",", dsTypes.ConvertAll(m => string.Format("\"{0}\"", m)).ToArray());
            string dsNamesStr = string.Join(",", dsNames.ConvertAll(m => string.Format("\"{0}\"", m)).ToArray());
            string valStr = string.Join(",", Array.ConvertAll(Values, val => val.ToString(CultureInfo.InvariantCulture)));

            string res = string.Format(MetricJsonFormat, HostName, PluginName,
                PluginInstanceName, TypeName, TypeInstanceName, Timestamp,
                dsTypesStr, dsNamesStr, valStr);
            return (res);
        }

    }

    internal class AttributeValue : CollectableValue
    {
        public string Name { get; set; }
        public string Value { get; set; }
        private const string JSON_FORMAT = @"{{""name"":""{0}"", ""value"":""{1}""}}";

        public AttributeValue(string hostname, string name, string value)
        {
            Name = name;
            Value = value;
            HostName = hostname;

            PluginName = "WindowsAttributes";
            PluginInstanceName = "";
            TypeName = "";
            TypeInstanceName = "";
        }

        public override string getJSON()
        {
            return string.Format(JSON_FORMAT, Name, Value);
        }

    }

    internal class RelationValue : CollectableValue
    {
        public string Fqn { get; set; }

        private const string JSON_FORMAT = @"{{""fqn"":""{0}""}}";

        public RelationValue(string hostname, string fqn)
        {
            Fqn = fqn;
            HostName = hostname;

            PluginName = "WindowsAttributes";
            PluginInstanceName = "";
            TypeName = "";
            TypeInstanceName = "";
        }

        public override string getJSON()
        {
            return string.Format(JSON_FORMAT, Fqn);
        }
    }


    internal class EventValue : CollectableValue
    {
        public long Id { get; set; }
        public string Message { get; set; }
        public string Level { get; set; }
        public string Title { get; set; }
        public long Timestamp { get; set; }
        private const string JSON_FORMAT = @"{{""level"": ""{0}"", ""source"":""{1}"", ""title"":""{2}"", ""message"":""{3}"", ""timestamp"": {4} }}";

        public EventValue(string hostname, long timestamp, int nLevel, string title, string message, long id)
        {
            Level = EventValue.levelToString(nLevel);
            Timestamp = timestamp;
            Title = title;
            Message = message;
            HostName = hostname;
            Id = id;

            PluginName = "WindowsEvent";
            PluginInstanceName = "";
            TypeName = "";
            TypeInstanceName = "";
        }

        public static string levelToString(int level)
        {
            switch (level)
            {
                case 1:
                    return "CRITICAL";
                case 2:
                    return "ERROR";
                case 3:
                    return "WARN";
                case 4:
                    return "INFO";
                case 5:
                    return "DEBUG";
                default:
                    return "";
            }
        }
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != this.GetType())
                return false;

            EventValue that = (EventValue)obj;
            return this.Id == that.Id;
        }


        public override string getJSON()
        {
            return string.Format(JSON_FORMAT, Level, HostName, Title, Message, Timestamp*1000);
        }

    }
}
