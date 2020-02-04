using System;
using NLog;
using System.Configuration;
using BloombergFLP.CollectdWin;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net;

namespace Netuitive.CollectdWin
{
    internal class WriteNetuitivePlugin : ICollectdWritePlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string _ingestUrl;
        private string _eventIngestUrl;
        private string _checkIngestUrl;
        private int _maxEventTitleLength;

        private string _location;
        private string _defaultElementType;
        private int _payloadSize;
        private bool _enabled;
        private string _userAgent;

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("WriteNetuitive") as WriteNetuitivePluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : WriteNetuitive");
            }

            _ingestUrl = config.Url;
            _eventIngestUrl = _ingestUrl.Replace("/ingest/", "/ingest/events/");
            _checkIngestUrl = _ingestUrl.Replace("/ingest/", "/check/").Replace("/windows/", "/");
            Logger.Info("Posting metrics/attributes to:{0}, events to:{1}, chesk to:{2}", _ingestUrl, _eventIngestUrl, _checkIngestUrl);

            _location = config.Location;

            string type = config.Type;
            if (type == null || type.Trim().Length == 0)
            {
                _defaultElementType = "WINSRV";
            }
            else
            {
                _defaultElementType = type;
            }
            Logger.Info("Element type: {0}", _defaultElementType);

            _payloadSize = config.PayloadSize;
            if (_payloadSize < 0)
                _payloadSize = 99999;
            else if (_payloadSize == 0)
                _payloadSize = 25;

            Logger.Info("Maximum payload size: {0}", _payloadSize);

            _maxEventTitleLength = config.MaxEventTitleLength;

            _enabled = true;

            System.Reflection.AssemblyName assemblyName = System.Reflection.Assembly.GetEntryAssembly().GetName();
            _userAgent = assemblyName.Name + "-" + assemblyName.Version.ToString();

            // Default for .NET 4.5 is TLS1.0 and SSL3
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
        }

        public void Start()
        {
            Logger.Info("WriteNetuitive plugin started");
        }

        public void Stop()
        {
            Logger.Info("WriteNetuitive plugin stopped");
        }

        public void Write(CollectableValue value)
        {
            if (!_enabled)
                return;

            Queue<CollectableValue> entry = new Queue<CollectableValue>();
            entry.Enqueue(value);
            Write(entry);
        }

        public void Write(Queue<CollectableValue> values)
        {
            if (!_enabled)
                return;

            double writeStart = Util.GetNow();

            WriteElements(values);

            WriteEvents(values);

            WriteChecks(values);

            double writeEnd = Util.GetNow(); 
            Logger.Info("Write took {0:0.00}s", (writeEnd - writeStart));
        }

        public void WriteElements(Queue<CollectableValue> values)
        {
            List<IngestElement> elements = ExtractElementsFromCollectables(values);

            // Merge individual element payloads together where possible
            List<IngestElement> mergedElements = MergeElements(elements);

            PostElements(mergedElements);
        }



        /**
         * Merge elements of the same Id together subject to max payload size
         */
        protected List<IngestElement> MergeElements(List<IngestElement> ieList)
        {  
            List<IngestElement> mergedList = new List<IngestElement>();
            IngestElement current = null;
            int payloadSize = 0;
            foreach (IngestElement element in ieList)
            {
                if (current != null && element.id.Equals(current.id) && payloadSize < _payloadSize)
                {
                    // This element is the same as the current one - merge them
                    current.mergeWith(element);
                    payloadSize += element.getPayloadSize();
                }
                else
                {
                    // This is a different element - add this to the list and start a new
                    if (current != null)
                        mergedList.Add(current);

                    current = element;
                    payloadSize = element.getPayloadSize();
                }
            }
            // Add the final working element
            if (current != null)
                mergedList.Add(current);

            return mergedList;
        }


        private void PostElements(List<IngestElement> ingestElements)
        {
            foreach (IngestElement ingestElement in ingestElements)
            {
                string payload = "[" + Util.SerialiseJsonObject(ingestElement, typeof(IngestElement)) + "]";
                KeyValuePair<int, string> res = Util.PostJson(_ingestUrl, _userAgent, payload);
                bool isOK = ProcessResponseCode(res.Key);
                if (!isOK)
                {
                    Logger.Warn("Error posting to ingest endpoint: {0}, {1}", res.Key, res.Value);
                    Logger.Warn("Payload: {0}", payload);
                }
            }
        }

        public List<IngestElement> ExtractElementsFromCollectables(Queue<CollectableValue> values)
        {
            List<IngestElement> elements =
                values
                .OfType<IngestValue>()
                .Select(value =>
                {
                    string elementType = (value.ElementType == null) ? _defaultElementType : value.ElementType;
                    IngestElement element = new IngestElement(value.HostName, value.HostName, elementType, _location);

                    if (value is MetricValue)
                    {
                        AddMetrics((MetricValue)value, element);
                    }
                    else if (value is AttributeValue)
                    {
                        element.addAttribute(new IngestAttribute(((AttributeValue)value).Name, ((AttributeValue)value).Value));
                    }
                    else if (value is RelationValue)
                    {
                        element.addRelation(new IngestRelation(((RelationValue)value).Fqn));
                    }
                    else if (value is TagValue)
                    {
                        element.addTag(new IngestTag(((TagValue)value).Name, ((TagValue)value).Value));
                    }
                    return element;
                })
                .OrderBy(ingestElement => ingestElement.id).ToList();

            return elements;
        }

        public void AddMetrics(MetricValue metric, IngestElement element)
        {

            string metricId = metric.PluginName;
            if (metric.PluginInstanceName.Length > 0)
                metricId += "." + metric.PluginInstanceName;
            if (metric.TypeInstanceName.Length > 0)
                metricId += "." + metric.TypeInstanceName;

            IList<DataSource> dsList = DataSetCollection.Instance.GetDataSource(metric.TypeName);
            var dsNames = new List<string>();
            var dsTypes = new List<string>();
            if (dsList == null)
            {
                Logger.Error("Invalid type : {0}, not found in types.db", metric.TypeName);
                return;
            }
            else
            {
                foreach (DataSource ds in dsList)
                {
                    dsNames.Add(ds.Name);
                    dsTypes.Add(ds.Type.ToString().ToLower());
                }
            }

            metricId = Regex.Replace(metricId, "[ ]", "_"); // Keep spaces as underscores
            metricId = Regex.Replace(metricId, "[^a-zA-Z0-9\\._-]", ""); // Remove punctuation
            if (metric.Values.Length == 1)
            {
                // Simple case - just one metric in type
                string friendlyName = metric.FriendlyNames == null ? metricId : metric.FriendlyNames[0];
                element.addMetric(new IngestMetric(metricId, friendlyName, metric.TypeName, dsTypes[0]));
                element.addSample(new IngestSample(metricId, (long)metric.Timestamp * 1000, metric.Values[0]));
            }
            else if (metric.Values.Length > 1)
            {
                // Compound type with multiple metrics
                int ix = 0;
                foreach (DataSource ds in dsList)
                {
                    // Include the Types.db suffix in the metric name
                    string friendlyName = metric.FriendlyNames == null ? metricId : metric.FriendlyNames[ix];

                    element.addMetric(new IngestMetric(metricId + "." + ds.Name, friendlyName, metric.TypeName, dsTypes[ix]));
                    element.addSample(new IngestSample(metricId + "." + ds.Name, (long)metric.Timestamp * 1000, metric.Values[ix]));
                    ix++;
                }
            }
        }

        public void WriteEvents(Queue<CollectableValue> values)
        {
            List<IngestEvent> events = ExtractEventsFromCollectables(values);

            if (events.Count > 0)
                PostEvents(events);
        }

        protected List<IngestEvent> ExtractEventsFromCollectables(Queue<CollectableValue> values)
        {

            List<IngestEvent> events = values.OfType<EventValue>()
                .Select(value =>
                {
                    // Format title and message
                    string message = value.Message;
                    string title = value.Title;
                    if (title.Length > _maxEventTitleLength)
                        title = title.Substring(0, _maxEventTitleLength);

                    //Convert level to netuitive compatible levels
                    string level = "";
                    switch (value.Level)
                    {
                        case "CRITICAL":
                        case "ERROR":
                            level = "CRITICAL";
                            break;
                        case "WARNING":
                        case "WARN":
                            level = "WARNING";
                            break;
                        case "INFO":
                        case "DEBUG":
                        default:
                            level = "INFO";
                            break;
                    }

                    IngestEvent ie = new IngestEvent("INFO", value.Source, title, value.Timestamp * 1000);

                    IngestEventData data = new IngestEventData(value.HostName, level, message);
                    ie.setData(data);

                    return ie;
                }).ToList();

            return events;
        }

        private void PostEvents(List<IngestEvent> eventList)
        {
            // Note - assumes that there are never so many event that we want to split into separate payloads
            List<string> eventPayloads = new List<string>();
            foreach (IngestEvent ingestEvent in eventList)
            {
                eventPayloads.Add(Util.SerialiseJsonObject(ingestEvent, typeof(IngestEvent)));
            }
            
            string eventPayload = "[" + string.Join(",", eventPayloads.ToArray()) + "]";
            KeyValuePair<int, string> res = Util.PostJson(_eventIngestUrl, _userAgent, eventPayload);

            bool isOK = ProcessResponseCode(res.Key);
            if (!isOK)
            {
                Logger.Warn("Error posting events: {0}, {1}", res.Key, res.Value);
                Logger.Warn("Payload: {0}", eventPayload);
            }
        }

        public void WriteChecks(Queue<CollectableValue> values)
        {
            List<CheckValue> checks = values.OfType<CheckValue>().ToList();

            if (checks.Count > 0)
                PostChecks(checks);

        }

        private void PostChecks(List<CheckValue> checkList)
        {
            // Note - checks are sent one at a time
            List<string> eventPayloads = new List<string>();
            foreach (CheckValue check in checkList)
            {

                string url = string.Join("/", new string[] { _checkIngestUrl, check.Name, check.HostName,check.CheckInterval.ToString() });
                KeyValuePair<int, string> res = Util.PostJson(url, _userAgent, "", 3);

                bool isOK = ProcessResponseCode(res.Key);
                if (!isOK)
                {
                    Logger.Warn("Error posting check: {0}, {1}, {2}", url, res.Key, res.Value);
                }
            }
        }

        protected bool ProcessResponseCode(int responseCode)
        {
            if (responseCode == 410)
            {
                // shutdown this plugin
                Logger.Fatal("Received plugin shutdown code from server");
                _enabled = false;
                return false;
            } 
            else return 
                responseCode >=200 && responseCode < 300;
        }
    }

    // ******************** DataContract objects for JSON serialisation ******************** 
    [DataContract]
    class IngestElement
    {
        [DataMember(Order=1)]
        public string id;
        [DataMember(Order=2)]
        public string name;
        [DataMember(Order=3)]
        public string type;
        [DataMember(Order=4)]
        public string location;

        [DataMember(Order=5)]
        public List<IngestMetric> metrics = new List<IngestMetric>();

        [DataMember(Order=6)]
        public List<IngestSample> samples = new List<IngestSample>();

        [DataMember(Order=7)]
        public List<IngestAttribute> attributes = new List<IngestAttribute>();

        [DataMember(Order = 8)]
        public List<IngestRelation> relations = new List<IngestRelation>();

        [DataMember(Order = 9)]
        public List<IngestTag> tags = new List<IngestTag>();


        public IngestElement(string id, string name, string type, string location)
        {
            this.id = id;
            this.name = name;
            this.type = type;
            this.location = location;
        }

        public void addMetric(IngestMetric metric)
        {
            this.metrics.Add(metric);
        }

        public void addMetrics(List<IngestMetric> metrics)
        {
            this.metrics.AddRange(metrics);
        }

        public void addSample(IngestSample sample)
        {
            this.samples.Add(sample);
        }

        public void addSamples(List<IngestSample> samples)
        {
            this.samples.AddRange(samples);
        }

        public void addAttributes(List<IngestAttribute> attributes)
        {
            this.attributes.AddRange(attributes);
        }

        public void addAttribute(IngestAttribute attribute)
        {
            this.attributes.Add(attribute);
        }

        public void addTags(List<IngestTag> tags)
        {
            this.tags.AddRange(tags);
        }

        public void addTag(IngestTag tag)
        {
            this.tags.Add(tag);
        }

        public void addRelations(List<IngestRelation> relations)
        {
            this.relations.AddRange(relations);
        }

        public void addRelation(IngestRelation relation)
        {
            this.relations.Add(relation);
        }

        public int getPayloadSize()
        {
            return this.metrics.Count + this.attributes.Count;
        }

        public void mergeWith(IngestElement that)
        {
            if (!this.id.Equals(that.id))
            {   // shouldn't happen
                throw new Exception("Bad merge operation");
            }

            this.addMetrics(that.metrics);
            this.addSamples(that.samples);
            this.addAttributes(that.attributes);
            this.addTags(that.tags);
            this.addRelations(that.relations);
        }
    }

    [DataContract]
    class IngestMetric
    {
        [DataMember(Order=1)]
        string id;
        [DataMember(Order=2)]
        string unit;
        [DataMember(Order=3)]
        string name;
        [DataMember(Order = 4, EmitDefaultValue = false)]
        string type;

        public IngestMetric(string id, string name, string unit, string type)
        {
            this.id = id;
            this.name = name;
            this.unit = unit;
            this.type = "COUNTER".Equals(type, StringComparison.OrdinalIgnoreCase) ? "COUNTER" : null;
        }
    }

    [DataContract]
    class IngestSample
    {
        [DataMember(Order=1)]
        string metricId;
        [DataMember(Order=2)]
        long timestamp;
        [DataMember(Order=3)]
        double val;

        public IngestSample(string metricId, long timestamp, double val)
        {
            this.metricId = metricId;
            this.timestamp = timestamp;
            this.val = val;
        }
    }

    [DataContract]
    class IngestAttribute
    {
        [DataMember(Order=1)]
        string name;
        [DataMember(Order=2)]
        string value;

        public IngestAttribute(string name, string value)
        {
            this.name = name;
            this.value = value;
        }
    }

    [DataContract]
    class IngestTag
    {
        [DataMember(Order = 1)]
        string name;
        [DataMember(Order = 2)]
        string value;

        public IngestTag(string name, string value)
        {
            this.name = name;
            this.value = value;
        }
    }

    [DataContract]
    class IngestRelation
    {
        [DataMember]
        string fqn;

        public IngestRelation(string fqn)
        {
            this.fqn = fqn;
        }
    }

    [DataContract]
    class IngestEvent
    {
        [DataMember(Order = 1)]
        public string type;
        [DataMember(Order = 2)]
        public string source;
        [DataMember(Order = 3)]
        public IngestEventData data;

        [DataMember(Order = 4)]
        public List<IngestEventTag> tags;

        [DataMember(Order = 5)]
        public string title;

        [DataMember(Order = 6)]
        public long timestamp;

        public IngestEvent(string type, string source, string title, long timestamp)
        {
            this.type = type;
            this.source = source;
            this.title = title;
            this.timestamp = timestamp;
            this.tags = new List<IngestEventTag>();
        }

        public void setData(IngestEventData data)
        {
            this.data = data;
        }

        public void setTags(List<IngestEventTag> tags)
        {
            this.tags = tags;
        }

    }

    [DataContract]
    class IngestEventData
    {
        [DataMember(Order = 1)]
        string elementId;
        [DataMember(Order = 2)]
        string level;
        [DataMember(Order = 3)]
        string message;

        public IngestEventData(string elementId, string level, string message)
        {
            this.elementId = elementId;
            this.level = level;
            this.message = message;
        }
    }

    [DataContract]
    class IngestEventTag
    {
        [DataMember(Order = 1)]
        string name;
        [DataMember(Order = 2)]
        string value;

        public IngestEventTag(string name, string value)
        {
            this.name = name;
            this.value = value;
        }
    }


}

// ----------------------------------------------------------------------------
// Copyright (C) 2015 Netuitive Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// ----------------------------- END-OF-FILE ----------------------------------