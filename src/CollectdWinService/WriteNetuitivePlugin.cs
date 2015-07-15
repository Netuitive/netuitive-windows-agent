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

        private const string NetuitiveMetricFormat = @"{{""id"":""{0}"", ""unit"":""{1}""}}";

        private const string NetuitiveSampleFormat = @"{{""metricId"":""{0}"", ""timestamp"":{1}, ""val"":{2}}}";


        private const string NetuitiveJsonFormat =
         @"[{{""type"": ""{0}"", ""id"":""{1}"", ""name"":""{2}"", ""location"":""{3}""" + 
         @", ""metrics"":[{4}]" +  
         @", ""samples"":[{5}]" +
         @", ""attributes"":[{6}]" + 
        // @", ""attributes"":[{""name"":""testattr"",""value"":""testattrval""}]" + 
        // @", ""tags"": [{""name"":""testtag"", ""value"":""testtagval""}]" + 
         @"}}] ";


        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string _url;
        private string _location;
        private string _elementType;

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : CollectdWinConfig");
            }

            string url = config.WriteNetuitive.url;
            Logger.Info("Posting to: {0}", url);

            _url = url;

            _location = config.WriteNetuitive.location;

            string type = config.WriteNetuitive.type;
            if (type == null || type.Trim().Length == 0)
            {
                _elementType = "WINSRV";
            }
            else
            {
                _elementType = type;
            }


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
            string payload = GeNetuitiveJsonStr(value);
            Logger.Debug("WriteNetuitive: {0}", payload);
            
            string result = "";
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    result = client.UploadString(_url, "POST", payload);
                }
            }
            catch (System.Net.WebException ex)
            {
                Exception baseex = ex.GetBaseException();
                if (baseex as ThreadInterruptedException != null) {
                    throw baseex;
                } else
                    Logger.Error("Error posting data: {0}", payload, ex);
            }
            Logger.Debug("response: {0}", result);
        }

        public string GeNetuitiveJsonStr(CollectableValue value)
        {
            var metricList = new List<string>();
            var sampleList = new List<string>();
            var attributeList = new List<string>();

            if (value is AttributeValue)
            {
                AttributeValue attr = (AttributeValue)value;
                attributeList.Add(attr.getJSON());
            }
            else if (value is MetricValue)
            {
                MetricValue metric = (MetricValue)value;
                string metricId = metric.PluginName;
                if (metric.PluginInstanceName.Length > 0)
                    metricId += "." + metric.PluginInstanceName.Replace(".", "_");
                if (metric.TypeInstanceName.Length > 0)
                    metricId += "." + metric.TypeInstanceName;

                if (metric.Values.Length == 1)
                {
                    // Simple case - just one metric in type
                    metricList.Add(string.Format(NetuitiveMetricFormat, metricId, metric.TypeName));
                    sampleList.Add(string.Format(NetuitiveSampleFormat, metricId, (long)metric.Epoch * 1000, metric.Values[0]));
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
                            metricList.Add(string.Format(NetuitiveMetricFormat, metricId, metric.TypeName));
                            sampleList.Add(string.Format(NetuitiveSampleFormat, metricId + "." + ds.Name, (long)metric.Epoch * 1000, metric.Values[ix++]));
                        }
                    }
                }
            }
            string metricsString = string.Join(",",  metricList.ToArray());
            string samplesString = string.Join(",", sampleList.ToArray());
            string attributesString = string.Join(",", attributeList.ToArray());

            string res = string.Format(NetuitiveJsonFormat, _elementType, value.HostName, value.HostName, _location,
                metricsString,
                samplesString, 
                attributesString);
            return (res);
        
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