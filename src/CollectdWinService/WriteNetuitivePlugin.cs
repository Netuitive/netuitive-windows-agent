using System;
using NLog;
using System.Configuration;
using System.Net;
using BloombergFLP.CollectdWin;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Netuitive.CollectdWin
{
    internal class WriteNetuitivePlugin : ICollectdWritePlugin
    {
        private const string NETUITIVE_METRIC_JSON_FORMAT = @"{{""id"":""{0}"", ""unit"":""{1}"", ""name"":""{2}""}}";

        private const string NETUITIVE_SAMPLE_JSON_FORMAT = @"{{""metricId"":""{0}"", ""timestamp"":{1}, ""val"":{2}}}";

        private const string NETUITIVE_INGEST_JSON_FORMAT =
         @"{{""type"": ""{0}"", ""id"":""{1}"", ""name"":""{2}"", ""location"":""{3}""" +
         @", ""metrics"":[{4}]" +
         @", ""samples"":[{5}]" +
         @", ""attributes"":[{6}]" +
            // @", ""tags"": [{""name"":""testtag"", ""value"":""testtagval""}]" + 
         @"}} ";

        private const string EVENT_JSON_FORMAT = @"{{""type"": ""{0}"", ""source"":""{1}"", ""data"":{{""elementId"":""{2}"", ""level"":""{3}"", ""message"":""{4}""}}, ""title"":""{5}"", ""timestamp"": {6} }}";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string _ingestUrl;
        private string _eventIngestUrl;
        private int _maxEventTitleLength;

        private string _location;
        private string _defaultElementType;
        private int _payloadSize;

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
        }

        public void Start()
        {
            Logger.Info("WriteNetuitive plugin started");
        }

        public void Stop()
        {
            Logger.Info("WriteNetuitive plugin stopped");
        }

        public void Write(Queue<CollectableValue> values)
        {
            // Split the complete list into configured batch size

            List<string> metricAttributeJsonList = new List<string>();
            List<string> eventJsonList = new List<string>();
            int counter = 0;
            foreach (CollectableValue value in values)
            {
                counter++;
                if (value is MetricValue)
                {
                    metricAttributeJsonList.Add(GetMetricJsonStr((MetricValue)value));
                }
                else if (value is AttributeValue)
                {
                    metricAttributeJsonList.Add(GetAttributeJsonStr((AttributeValue)value));
                }
                else if (value is EventValue)
                {
                    eventJsonList.Add(GetEventJsonStr((EventValue)value));
                }
                else
                {
                    // Collectable value type not handled by this adapter
                }

                // Send payload if reached end or max size
                if (metricAttributeJsonList.Count == _payloadSize || eventJsonList.Count == _payloadSize || counter == values.Count)
                {
                    if (metricAttributeJsonList.Count > 0)
                    {
                        string payload = "[" + string.Join(",", metricAttributeJsonList.ToArray()) + "]";
                        string res = Util.PostJson(_ingestUrl, payload);
                        if (res.Length > 0)
                        {
                            Logger.Warn("Error posting metrics/attributes: {0}", res);
                            Logger.Warn("Payload: {0}", payload);
                        }

                        metricAttributeJsonList.Clear();
                    }
                    if (eventJsonList.Count > 0)
                    {
                        string payload = "[" + string.Join(",", eventJsonList.ToArray()) + "]";
                        string res = Util.PostJson(_eventIngestUrl, payload);
                        if (res.Length > 0)
                        {
                            // Do not post as Error as this goes to event log by default and may result in loop
                            Logger.Warn("Error posting events: {0}", res);
                            Logger.Warn("Payload: {0}", payload);
                        }
                        eventJsonList.Clear();
                    }
                }
            }
        }

        public void Write(CollectableValue value)
        {
            Queue<CollectableValue> entry = new Queue<CollectableValue>();
            entry.Enqueue(value);
            Write(entry);
        }

        public string GetMetricJsonStr(MetricValue value)
        {
            var metricList = new List<string>();
            var sampleList = new List<string>();

            MetricValue metric = (MetricValue)value;
            string metricId = metric.PluginName;
            if (metric.PluginInstanceName.Length > 0)
                metricId += "." + metric.PluginInstanceName.Replace(".", "_");
            if (metric.TypeInstanceName.Length > 0)
                metricId += "." + metric.TypeInstanceName;

            if (metric.Values.Length == 1)
            {
                // Simple case - just one metric in type
                metricList.Add(string.Format(NETUITIVE_METRIC_JSON_FORMAT, metricId, metric.TypeName, metric.FriendlyNames[0]));
                sampleList.Add(string.Format(NETUITIVE_SAMPLE_JSON_FORMAT, metricId, (long)metric.Epoch * 1000, metric.Values[0]));
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
                        metricList.Add(string.Format(NETUITIVE_METRIC_JSON_FORMAT, metricId, metric.TypeName, metric.FriendlyNames[ix]));
                        sampleList.Add(string.Format(NETUITIVE_SAMPLE_JSON_FORMAT, metricId + "." + ds.Name, (long)metric.Epoch * 1000, metric.Values[ix]));
                        ix++;
                    }
                }
            }
            string metricsString = string.Join(",", metricList.ToArray());
            string samplesString = string.Join(",", sampleList.ToArray());

            string res = string.Format(NETUITIVE_INGEST_JSON_FORMAT, _defaultElementType, value.HostName, value.HostName, _location,
                metricsString,
                samplesString,
                "");
            return res;
        }

        public string GetAttributeJsonStr(AttributeValue value)
        {
            string res = string.Format(NETUITIVE_INGEST_JSON_FORMAT, _defaultElementType, value.HostName, value.HostName, _location,
                "",
                "",
                value.getJSON());
            return res;
        }

        public string GetEventJsonStr(EventValue value)
        {
            string message = value.Message;
            string title = value.Level + " - " + value.Message;
            if (title.Length > _maxEventTitleLength)
                title = title.Substring(0, _maxEventTitleLength);

            title = title.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "").Replace("\\", "\\\\");
            message = message.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "").Replace("\\", "\\\\");

            //Convert level to netuitive compatible levels
            string level = "";
            switch (value.Level)
            {
                case "CRITICAL":
                case "ERROR":
                    level = "CRITICAL";
                    break;
                case "WARNING":
                    level = "WARNING";
                    break;
                case "INFO":
                case "DEBUG":
                default:
                    level = "INFO";
                    break;
            }

            string json = String.Format(EVENT_JSON_FORMAT, "INFO", "", value.HostName, level, message, title, value.Timestamp * 1000);
            return json;
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