using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using NLog;
using System.Text.RegularExpressions;

namespace BloombergFLP.CollectdWin
{
    internal struct Metric
    {
        public string Category;
        public string CollectdPlugin, CollectdPluginInstance, CollectdType, CollectdTypeInstance;
        public string CounterName;
        public IList<PerformanceCounter> Counters;
        public string Instance;
        public uint ScaleDownFactor;
        public uint ScaleUpFactor;
    }

    internal class WindowsPerformanceCounterPlugin : ICollectdReadPlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IList<Metric> _metrics;
        private string _hostName;

        public WindowsPerformanceCounterPlugin()
        {
            _metrics = new List<Metric>();
        }

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : CollectdWinConfig");
            }

            _hostName = Util.GetHostName();

            _metrics.Clear();

            foreach (CollectdWinConfig.CounterConfig counter in config.WindowsPerformanceCounters.Counters)
            {

                if (counter.Instance == "")
                {
                    // Instance not specified 
                    AddPerformanceCounter(counter.Category, counter.Name,
                        counter.Instance, counter.ScaleUpFactor,
                        counter.ScaleDownFactor, counter.CollectdPlugin,
                        counter.CollectdPluginInstance, counter.CollectdType,
                        counter.CollectdTypeInstance);
                }
                else
                {
                    // Match instance with regex
                    string[] instances = new string[0];
                    try
                    {
                        Regex regex = new Regex(counter.Instance, RegexOptions.None);

                        var cat = new PerformanceCounterCategory(counter.Category);
                        instances = cat.GetInstanceNames();
                        List<string> instanceList = new List<string>();
                        foreach (string instance in instances)
                        {
                            if (regex.IsMatch(instance))
                            {
                                instanceList.Add(instance);
                            }
                        }
                        instances = instanceList.ToArray();

                    }
                    catch (ArgumentException ex)
                    {
                        Logger.Error(string.Format("Failed to parse instance regular expression: category={0}, instance={1}, counter={2}", counter.Category, counter.Instance, counter.Name), ex);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(string.Format("Could not initialise performance counter category: {0}", counter.Category), ex);
                    }
                    if (instances.Length == 0)
                    {
                        Logger.Warn("No instances found for category: {0}", counter.Category);
                    }

                    foreach (string instance in instances)
                    {
                        // Replace collectd_plugin_instance with the Instance got from counter
                        AddPerformanceCounter(counter.Category, counter.Name,
                            instance, counter.ScaleUpFactor,
                            counter.ScaleDownFactor, counter.CollectdPlugin,
                            instance, counter.CollectdType,
                            counter.CollectdTypeInstance);
                    }
                }
            }
            Logger.Info("WindowsPerformanceCounter plugin configured");
        }

        public void Start()
        {
            Logger.Info("WindowsPerformanceCounter plugin started");
        }

        public void Stop()
        {
            Logger.Info("WindowsPerformanceCounter plugin stopped");
        }

        public IList<CollectableValue> Read()
        {
            var metricValueList = new List<CollectableValue>();
            foreach (Metric metric in _metrics)
            {
                try
                {
                    var vals = new List<double>();
                    foreach (PerformanceCounter ctr in metric.Counters)
                    {
                        double val = ctr.NextValue();
                        if (metric.ScaleUpFactor > 0)
                        {
                            val = val * metric.ScaleUpFactor;
                        }
                        else
                        {
                            if (metric.ScaleDownFactor > 0)
                            {
                                val = val / metric.ScaleDownFactor;
                            }
                        }
                        vals.Add(val);
                    }

                    var metricValue = new MetricValue
                    {
                        HostName = _hostName,
                        PluginName = metric.CollectdPlugin,
                        PluginInstanceName = metric.CollectdPluginInstance,
                        TypeName = metric.CollectdType,
                        TypeInstanceName = metric.CollectdTypeInstance,
                        Values = vals.ToArray()
                    };

                    TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
                    double epoch = t.TotalMilliseconds / 1000;
                    metricValue.Epoch = Math.Round(epoch, 3);

                    metricValueList.Add(metricValue);
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("Failed to collect metric: {0}, {1}, {2}", metric.Category, metric.Instance, metric.CounterName), ex);
                }
            }
            return (metricValueList);
        }

        private void AddPerformanceCounter(string category, string names, string instance, uint scaleUpFactor,
            uint scaleDownFactor, string collectdPlugin, string collectdPluginInstance, string collectdType,
            string collectdTypeInstance)
        {
            string logstr =
                string.Format(new FixedLengthFormatter(), 
                    "Category={0}, Counter={1}, Instance={2}, CollectdPlugin={3}, CollectdPluginInstance={4}, CollectdType={5}, CollectdTypeInstance={6}, ScaleUpFactor={7}, ScaleDownFactor={8}",
                    category, names, instance, collectdPlugin, collectdPluginInstance,
                    collectdType, collectdTypeInstance, scaleUpFactor, scaleDownFactor);

            try
            {
                var metric = new Metric();
                string[] counterList = names.Split(',');
                metric.Counters = new List<PerformanceCounter>();
                foreach (string ctr in counterList)
                    metric.Counters.Add(new PerformanceCounter(category, ctr.Trim(), instance));
                metric.Category = category;
                metric.Instance = instance;
                metric.CounterName = names;
                metric.ScaleUpFactor = scaleUpFactor;
                metric.ScaleDownFactor = scaleDownFactor;
                metric.CollectdPlugin = collectdPlugin;
                metric.CollectdPluginInstance = collectdPluginInstance;
                metric.CollectdType = collectdType;
                metric.CollectdTypeInstance = collectdTypeInstance;

                _metrics.Add(metric);
                Logger.Info("Added Performance COUNTER : {0}", logstr);
            }
            catch (Exception exp)
            {
                Logger.Error("Got exception : {0}, while adding performance counter: {1}", exp, logstr);
            }
        }
    }

    public class FixedLengthFormatter : IFormatProvider, ICustomFormatter
    {
        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            string s = arg.ToString();
            s += "                              ";
            return s.Substring(0, 30);
        }

        public object GetFormat(Type formatType)
        {
            return (formatType == typeof(ICustomFormatter)) ? this : null;
        }
    }


}

// ----------------------------------------------------------------------------
// Copyright (C) 2015 Bloomberg Finance L.P.
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