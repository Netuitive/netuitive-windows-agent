using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using NLog;
using System.Text.RegularExpressions;
using System.Threading;

namespace BloombergFLP.CollectdWin
{
    internal struct Metric
    {
        public string Category;
        public string CollectdPlugin, CollectdPluginInstance, CollectdType, CollectdTypeInstance;
        public string CounterName;
        public IList<PerformanceCounter> Counters;
        public string Instance;
        public double Multiplier;
        public int DecimalPlaces;
        public string[] FriendlyNames;
    }

    internal class ReadWindowsPerfCountersPlugin : ICollectdReadPlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IList<Metric> _metrics;
        private string _hostName;
        private int _reloadInterval;
        private double _lastUpdated;

        public ReadWindowsPerfCountersPlugin()
        {
            _metrics = new List<Metric>();
        }

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("ReadWindowsPerfCounters") as ReadWindowsPerfCountersPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : ReadWindowsPerfCounters");
            }

            _hostName = Util.GetHostName();

            // Set reload time
            _reloadInterval = config.ReloadInterval;
            Logger.Info("Loading metric configuration. Reload interval: {0} sec", _reloadInterval);

            _lastUpdated = Util.GetNow();

            // Load the metrics - this checks for existence
            _metrics.Clear();
            int metricCounter = 0;


            foreach (CounterConfig counter in config.Counters)
            {

                if (counter.Instance == "")
                {
                    // Instance not specified 
                    if (AddPerformanceCounter(counter.Category, counter.Name,
                        counter.Instance, counter.Multiplier,
                        counter.DecimalPlaces, counter.CollectdPlugin,
                        counter.CollectdPluginInstance, counter.CollectdType,
                        counter.CollectdTypeInstance))
                    {
                        metricCounter++;
                    }
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
                        LogEventInfo logEvent = new LogEventInfo(LogLevel.Error, Logger.Name, "Failed to initialise performance counter");
                        logEvent.Properties.Add("EventID", ErrorCodes.ERROR_CONFIGURATION_EXCEPTION);
                        Logger.Log(logEvent);
                        Logger.Warn(string.Format("Failed to parse instance regular expression: category={0}, instance={1}, counter={2}", counter.Category, counter.Instance, counter.Name), ex);
                    }
                    catch (InvalidOperationException ex)
                    {
                        if (ex.Message.ToLower().Contains("category does not exist")) {
                            Logger.Warn(string.Format("Performance Counter not added: Category does not exist: {0}", counter.Category));
                        }
                        else
                        {
                            LogEventInfo logEvent = new LogEventInfo(LogLevel.Error, Logger.Name, "Failed to initialise performance counter");
                            logEvent.Properties.Add("EventID", ErrorCodes.ERROR_CONFIGURATION_EXCEPTION);
                            Logger.Log(logEvent);
                            Logger.Warn(string.Format("Could not initialise performance counter category: {0}, instance: {1}, counter: {2}", counter.Category, counter.Instance, counter.Name), ex);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEventInfo logEvent = new LogEventInfo(LogLevel.Error, Logger.Name, "Failed to initialise performance counter");
                        logEvent.Properties.Add("EventID", ErrorCodes.ERROR_CONFIGURATION_EXCEPTION);
                        Logger.Log(logEvent);
                        Logger.Warn(string.Format("Could not initialise performance counter category: {0}", counter.Category), ex);
                    }
                    if (instances.Length == 0)
                    {
                        Logger.Warn("No instances matching category: {0}, instance: {1}", counter.Category, counter.Instance);
                    }

                    foreach (string instance in instances)
                    {
                        string instanceAlias = instance;

                        if (instances.Length == 1)
                        {
                            // There is just one instance
                            // If this is because the regex was hardcoded then replace the instance name with the CollectdInstanceName - i.e., alias the instance
                            // But if the regex contains wildcards then it is a fluke that there was a single match
                            if (counter.Instance.IndexOf("?") < 0 && counter.Instance.IndexOf("*") < 0)
                            {
                                // No wildcards => this was hardcoded value.
                                instanceAlias = counter.CollectdPluginInstance;
                            }
                        }

                        // Replace collectd_plugin_instance with the Instance got from counter
                        if (AddPerformanceCounter(counter.Category, counter.Name,
                            instance, counter.Multiplier,
                            counter.DecimalPlaces, counter.CollectdPlugin,
                            instanceAlias, counter.CollectdType,
                            counter.CollectdTypeInstance))
                        {
                            metricCounter++;
                        }
                    }
                }
            }
            // Wait 1 second for the two-valued counters to be ready for next incremental read - see https://msdn.microsoft.com/en-us/library/system.diagnostics.performancecounter.nextvalue(v=vs.110).aspx
            Thread.Sleep(1000);

            Logger.Info("ReadWindowsPerfeCounters plugin configured {0} metrics", metricCounter);
        }

        public void Start()
        {
            Logger.Info("ReadWindowsPerfCounters plugin started");
        }

        public void Stop()
        {
            Logger.Info("ReadWindowsPerfCounters plugin stopped");
        }

        public IList<CollectableValue> Read()
        {
            // Check if it is time to reload the metric config
            // We need to periodially reload the config in case new instances or new categories are available (e.g. due to cluster move)
            if (Util.GetNow() - _lastUpdated >= _reloadInterval)
            {
                Configure();
            }

            var metricValueList = new List<CollectableValue>();
            foreach (Metric metric in _metrics)
            {
                try
                {
                    var vals = new List<double>();
                    foreach (PerformanceCounter ctr in metric.Counters)
                    {
                        double val = ctr.NextValue();
                        val = val * metric.Multiplier;

                        if (metric.DecimalPlaces >= 0)
                            val = Math.Round(val, metric.DecimalPlaces);
                        vals.Add(val);
                    }

                    var metricValue = new MetricValue
                    {
                        HostName = _hostName,
                        PluginName = metric.CollectdPlugin,
                        PluginInstanceName = metric.CollectdPluginInstance,
                        TypeName = metric.CollectdType,
                        TypeInstanceName = metric.CollectdTypeInstance,
                        Values = vals.ToArray(),
                        FriendlyNames = metric.FriendlyNames

                    };

                    metricValue.Epoch = Util.toEpoch(DateTime.UtcNow);

                    metricValueList.Add(metricValue);
                }
                catch (Exception ex)
                {
                    Logger.Warn(string.Format("Failed to collect metric: {0}, {1}, {2}", metric.Category, metric.Instance, metric.CounterName), ex);
                }
            }
            return (metricValueList);
        }

        private Boolean AddPerformanceCounter(string category, string names, string instance, double multiplier,
            int decimalPlaces, string collectdPlugin, string collectdPluginInstance, string collectdType,
            string collectdTypeInstance)
        {
            string logstr =
                string.Format(new FixedLengthFormatter(), 
                    "Category={0} Counter={1} Instance={2} CollectdPlugin={3} CollectdPluginInstance={4} CollectdType={5} CollectdTypeInstance={6} Multiplier={7} DecimalPlaces={8}",
                    category, names, instance, collectdPlugin, collectdPluginInstance,
                    collectdType, collectdTypeInstance, multiplier, decimalPlaces);

            try
            {
                var metric = new Metric();
                string[] counterList = names.Split(',');
                metric.Counters = new List<PerformanceCounter>();
                metric.FriendlyNames = new string[counterList.Length];
                int ix = 0;
                foreach (string ctr in counterList)
                {
                    PerformanceCounter perfCounter = new PerformanceCounter(category, ctr.Trim(), instance);
                    // Collect a value - this is needed to initialise counters that need two values
                    perfCounter.NextValue();
                    metric.Counters.Add(perfCounter);
                    string friendlyName = ctr.Trim();
                    if (instance.Length > 0)
                        friendlyName += " (" + instance + ")";
                    metric.FriendlyNames[ix++] = friendlyName;
                }

                metric.Category = category;
                metric.Instance = instance;
                metric.CounterName = names;
                metric.Multiplier = multiplier;
                metric.DecimalPlaces = decimalPlaces;
                metric.CollectdPlugin = collectdPlugin;
                metric.CollectdPluginInstance = collectdPluginInstance;
                metric.CollectdType = collectdType;
                metric.CollectdTypeInstance = collectdTypeInstance;
                _metrics.Add(metric);
                Logger.Debug("Added Performance counter : {0}", logstr);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.ToLower().Contains("category does not exist")) {
                    Logger.Warn(string.Format("Performance Counter not added: Category does not exist: {0}", category));
                }
                else
                {
                    LogEventInfo logEvent = new LogEventInfo(LogLevel.Error, Logger.Name, "Could not initialise performance counter");
                    logEvent.Properties.Add("EventID", ErrorCodes.ERROR_CONFIGURATION_EXCEPTION);
                    Logger.Log(logEvent);
                    Logger.Warn(string.Format("Could not initialise performance counter category: {0}, instance: {1}, counter: {2}", category, instance, names), ex);
                    return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogEventInfo logEvent = new LogEventInfo(LogLevel.Error, Logger.Name, "Could not initialise performance counter");
                logEvent.Properties.Add("EventID", ErrorCodes.ERROR_CONFIGURATION_EXCEPTION);
                Logger.Log(logEvent);
                Logger.Warn(string.Format("Could not initialise performance counter category: {0}, instance: {1}, counter: {2}", category, instance, names), ex);
                return false;
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