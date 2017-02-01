using System;
using NLog;
using System.Configuration;
using System.Net;
using BloombergFLP.CollectdWin;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Netuitive.CollectdWin
{
    internal class WriteNetuitivePlugin : ICollectdWritePlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string _ingestUrl;
        private string _eventIngestUrl;
        private int _maxEventTitleLength;

        private string _location;
        private string _defaultElementType;
        private int _payloadSize;
        private bool _enabled;

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("WriteNetuitive") as WriteNetuitivePluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : WriteNetuitive");
            }

            _ingestUrl = config.Url;
            _eventIngestUrl = _ingestUrl.Replace("/ingest/", "/ingest/events/");
            Logger.Info("Posting metrics/attributes to:{0}, events to:{1}", _ingestUrl, _eventIngestUrl);

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

            // Split into separate lists for each ingest point
            List<CollectableValue> metricsAttributesAndRelations = null;
            List<EventValue> events = null;
            GetSortedValueLists(values, out metricsAttributesAndRelations, out events);

            // Convert metrics and attributes into list of IngestElements
            List<IngestElement> ingestElementList = ConvertMetricsAttributesAndRelationsToIngestElements(metricsAttributesAndRelations);

            // Merge metrics and attributes for the same element together subject to max payload size
            List<IngestElement> mergedIngestElementList = MergeIngestElements(ingestElementList);

            // Post metrics and attributes
            PostMetricsAndAttributes(mergedIngestElementList);

            // Convert events into list of IngestEvents
            List<IngestEvent> eventList = ConvertEventsToIngestEvents(events);

            // Send event payloads
            if (eventList.Count > 0)
                PostEvents(eventList);

            double writeEnd = Util.GetNow(); 
            Logger.Info("Write took {0:0.00}s", (writeEnd - writeStart));
        }

        protected List<IngestElement> ConvertMetricsAttributesAndRelationsToIngestElements(List<CollectableValue> metricsAttributes)
        {
            List<IngestElement> ieList = new List<IngestElement>();
            foreach (CollectableValue value in metricsAttributes)
            {
                string elementType = (value.ElementType == null) ? _defaultElementType : value.ElementType;
                IngestElement ie = new IngestElement(value.HostName, value.HostName, elementType, _location);

                if (value is MetricValue)
                {
                    List<IngestMetric> outMetrics = null;
                    List<IngestSample> outSamples = null;
                    GetIngestMetrics((MetricValue)value, out outMetrics, out outSamples);

                    ie.addMetrics(outMetrics);
                    ie.addSamples(outSamples);
                }
                else if (value is AttributeValue)
                {
                    List<IngestAttribute> outAttributes = null;
                    GetIngestAttributes((AttributeValue)value, out outAttributes);
                    ie.addAttributes(outAttributes);
                }
                else if (value is RelationValue)
                {
                    List<IngestRelation> outRelation = null;
                    GetIngestRelations((RelationValue)value, out outRelation);
                    ie.addRelations(outRelation);

                }
                ieList.Add(ie);
            }

            return ieList;
        }

        protected List<IngestEvent> ConvertEventsToIngestEvents(List<EventValue> events)
        {
            List<IngestEvent> eventList = new List<IngestEvent>();
            foreach (EventValue value in events)
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

                IngestEvent ie = new IngestEvent("INFO", "", title, value.Timestamp * 1000);

                IngestEventData data = new IngestEventData(value.HostName, level, message);
                ie.setData(data);
                //TODO - tags

                eventList.Add(ie);
            }

            return eventList;
        }

        protected List<IngestElement> MergeIngestElements(List<IngestElement> ieList)
        {
            // Merge elements of the same Id together subject to max payload size
            List<IngestElement> mergedList = new List<IngestElement>();
            IngestElement current = null;
            int payloadSize = 0;
            int counter = 0;
            foreach (IngestElement element in ieList)
            {
                counter++;
                if (current != null && element.id.Equals(current.id) && payloadSize < _payloadSize && counter < ieList.Count)
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
            return mergedList;
        }

        private void PostEvents(List<IngestEvent> eventList)
        {
            // Note - assumes that there are never so many event that we want to split into separate payloads
            List<string> eventPayloads = new List<string>();
            foreach (IngestEvent ingestEvent in eventList)
            {
                eventPayloads.Add(SerialiseJsonObject(ingestEvent, typeof(IngestEvent)));
            }
            
            string eventPayload = "[" + string.Join(",", eventPayloads.ToArray()) + "]";
            KeyValuePair<int, string> res = Util.PostJson(_eventIngestUrl, eventPayload);

            bool isOK = ProcessResponseCode(res.Key);
            if (!isOK)
            {
                Logger.Warn("Error posting events: {0}, {1}", res.Key, res.Value);
                Logger.Warn("Payload: {0}", eventPayload);
            }
        }

        private void PostMetricsAndAttributes(List<IngestElement> mergedIngestElementList)
        {
            // Send metric and attribute payloads
            foreach (IngestElement ingestElement in mergedIngestElementList)
            {
                string payload = "[" + SerialiseJsonObject(ingestElement, typeof(IngestElement)) + "]";
                KeyValuePair<int, string> res = Util.PostJson(_ingestUrl, payload);
                bool isOK = ProcessResponseCode(res.Key);
                if (!isOK)
                {
                    Logger.Warn("Error posting metrics/attributes: {0}, {1}", res.Key, res.Value);
                    Logger.Warn("Payload: {0}", payload);
                }
            }
        }

        protected string SerialiseJsonObject(Object obj, Type type)
        {
            // finish off element
            MemoryStream stream = new MemoryStream();
            DataContractJsonSerializer ser = new DataContractJsonSerializer(type);
            ser.WriteObject(stream, obj);
            string json = Encoding.Default.GetString(stream.ToArray());
            return json;

        }
        protected void GetSortedValueLists(Queue<CollectableValue> values, out List<CollectableValue> metricsAttributesAndRelations, out List<EventValue> events)
        {
            metricsAttributesAndRelations = new List<CollectableValue>();
            events = new List<EventValue>();

            foreach (CollectableValue value in values)
            {
                if (value is MetricValue || value is AttributeValue || value is RelationValue)
                {
                    metricsAttributesAndRelations.Add(value);
                }
                else if (value is EventValue)
                {
                    events.Add((EventValue)value);
                }
                else
                {
                    // Collectable value type not handled by this adapter
                }
            }

            // Sort the lists by hostname so we can group them in the payload
            metricsAttributesAndRelations.Sort((p1, p2) => p1.HostName.CompareTo(p2.HostName));
            events.Sort((p1, p2) => p1.HostName.CompareTo(p2.HostName));
        }


        public void GetIngestMetrics(MetricValue metric, out List<IngestMetric> metrics, out List<IngestSample> samples)
        {

            metrics = new List<IngestMetric>();
            samples = new List<IngestSample>();

            string metricId = metric.PluginName;
            if (metric.PluginInstanceName.Length > 0)
                metricId += "." + metric.PluginInstanceName.Replace(".", "_");
            if (metric.TypeInstanceName.Length > 0)
                metricId += "." + metric.TypeInstanceName;

            metricId = Regex.Replace(metricId, "[ ]", "_"); // Keep spaces as underscores
            metricId = Regex.Replace(metricId, "[^a-zA-Z0-9\\._-]", ""); // Remove punctuation
            if (metric.Values.Length == 1)
            {
                // Simple case - just one metric in type
                metrics.Add(new IngestMetric(metricId, metric.FriendlyNames[0], metric.TypeName));
                samples.Add(new IngestSample(metricId, (long)metric.Epoch * 1000, metric.Values[0]));
            }
            else if (metric.Values.Length > 1)
            {
                // Compound type with multiple metrics
                IList<DataSource> dsList = DataSetCollection.Instance.GetDataSource(metric.TypeName);
                if (dsList == null)
                {
                    Logger.Debug("Invalid type : {0}, not found in types.db", metric.TypeName);
                }
                else
                {
                    int ix = 0;
                    foreach (DataSource ds in dsList)
                    {
                        // Include the Types.db suffix in the metric name
                        metrics.Add(new IngestMetric(metricId + "." + ds.Name, metric.FriendlyNames[ix], metric.TypeName));
                        samples.Add(new IngestSample(metricId + "." + ds.Name, (long)metric.Epoch * 1000, metric.Values[ix]));
                        ix++;
                    }
                }
            }
        }

        protected void GetIngestAttributes(AttributeValue value, out List<IngestAttribute> attributes)
        {
            attributes = new List<IngestAttribute>();
            attributes.Add(new IngestAttribute(value.Name, value.Value));
        }

        protected void GetIngestRelations(RelationValue value, out List<IngestRelation> relations)
        {
            relations = new List<IngestRelation>();
            relations.Add(new IngestRelation(value.Fqn));
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


        public IngestElement(string id, string name, string type, string location)
        {
            this.id = id;
            this.name = name;
            this.type = type;
            this.location = location;
        }

        public void addMetrics(List<IngestMetric> metrics)
        {
            this.metrics.AddRange(metrics);
        }

        public void addSamples(List<IngestSample> samples)
        {
            this.samples.AddRange(samples);
        }

        public void addAttributes(List<IngestAttribute> attributes)
        {
            this.attributes.AddRange(attributes);
        }

        public void addRelations(List<IngestRelation> relations)
        {
            this.relations.AddRange(relations);
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

        public IngestMetric(string id, string name, string unit)
        {
            this.id = id;
            this.name = name;
            this.unit = unit;
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