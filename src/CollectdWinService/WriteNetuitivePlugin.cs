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



        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string _ingestUrl;
        private string _location;
        private string _elementType;
        private int _payloadSize;

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("WriteNetuitive") as WriteNetuitivePluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : WriteNetuitive");
            }

            string url = config.Url;
            Logger.Info("Posting to: {0}", url);

            _ingestUrl = url;


            _location = config.Location;

            string type = config.Type;
            if (type == null || type.Trim().Length == 0)
            {
                _elementType = "WINSRV";
            }
            else
            {
                _elementType = type;
            }
            Logger.Info("Element type: {0}", _elementType);

            _payloadSize = config.PayloadSize;
            if (_payloadSize < 0)
                _payloadSize = 99999;
            else if (_payloadSize == 0)
                _payloadSize = 25;

            Logger.Info("Maximum payload size: {0}", _payloadSize);

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

            List<string> jsonList = new List<string>();
            int ix = 0;
            foreach (CollectableValue value in values) {
                ix++;
                if (value is MetricValue)
                {
                    jsonList.Add(GetMetricJsonStr((MetricValue)value));
                }
                else if (value is AttributeValue)
                {
                    jsonList.Add(GetAttributeJsonStr((AttributeValue)value));
                }
                else
                {
                    // Collectable value type not handled by this adapter
                }

                // Send payload if reached end or max size
                if (jsonList.Count == _payloadSize || (ix == values.Count && jsonList.Count > 0))
                {
                    string payload = "[" + string.Join(",", jsonList.ToArray()) + "]";
                    string res = Util.PostJson(_ingestUrl, payload);
                    if (res.Length > 0)
                    {
                        Logger.Error("Error posting metrics/attributes: {0}", res);
                    }

                    jsonList.Clear();
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

            string res = string.Format(NETUITIVE_INGEST_JSON_FORMAT, _elementType, value.HostName, value.HostName, _location,
                metricsString,
                samplesString,
                "");
            return res;
        }

        public string GetAttributeJsonStr(AttributeValue value)
        {
            string res = string.Format(NETUITIVE_INGEST_JSON_FORMAT, _elementType, value.HostName, value.HostName, _location,
                "",
                "",
                value.getJSON());
            return res;
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