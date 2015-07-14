using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using NLog;
using System.Text.RegularExpressions;
using BloombergFLP.CollectdWin;
using System.Management;
using System.Reflection;

namespace Netuitive.CollectdWin
{
    internal struct Attribute
    {
        public string name;
        public string variableName;
//        public string CollectdPlugin, CollectdPluginInstance, CollectdType, CollectdTypeInstance;
//        public string CounterName;
//        public IList<PerformanceCounter> Counters;
//        public string Instance;
    }

    internal class WindowsAttributesPlugin : ICollectdReadPlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IList<Attribute> _attributes;
        private string _hostName;

        public WindowsAttributesPlugin()
        {
            _attributes = new List<Attribute>();
        }

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : CollectdWinConfig");
            }

            _hostName = Util.GetHostName();

            _attributes.Clear();

            foreach (CollectdWinConfig.EnvironmentVariableConfig attr in config.EnvironmentVariableList)
            {
                Attribute attribute = new Attribute
                {
                    name = attr.Name,
                    variableName = attr.Value
                };
                _attributes.Add(attribute);

            }
            Logger.Info("WindowsAttributes plugin configured");
        }

        public void Start()
        {
            Logger.Info("WindowsAttributes plugin started");
        }

        public void Stop()
        {
            Logger.Info("WindowsAttributes plugin stopped");
        }

        public IList<CollectableValue> Read()
        {
            var metricValueList = new List<CollectableValue>();

            metricValueList.AddRange(getCommonAttributes());
            foreach (Attribute attribute in _attributes)
            {
                try
                {
                    string value = Environment.GetEnvironmentVariable(attribute.variableName);
                    AttributeValue attr = new AttributeValue(_hostName, attribute.name, value);
                    attr.HostName = _hostName;
                    metricValueList.Add(attr);

                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("Failed to collect attribute: {0}", attribute.variableName), ex);
                }
            }
            return (metricValueList);
        }

        private IList<CollectableValue> getCommonAttributes()
        {
            // Return standard attributes
            IList<CollectableValue> attributes = new List<CollectableValue>();

            Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            AttributeValue numProcessors = new AttributeValue(_hostName, "cpus", Environment.ProcessorCount.ToString());
            attributes.Add(numProcessors);

            AttributeValue osVersion = new AttributeValue(_hostName, "osversion", Environment.OSVersion.ToString());
            attributes.Add(osVersion);

            AttributeValue agent = new AttributeValue(_hostName, "agent", "collectdwin-" + fvi.FileVersion);
            attributes.Add(agent);

            long totalRAM = 0;
            try
            {
                ConnectionOptions connection = new ConnectionOptions();
                connection.Impersonation = ImpersonationLevel.Impersonate;
                ManagementScope scope = new ManagementScope("\\\\.\\root\\CIMV2", connection);
                scope.Connect();
                ObjectQuery query = new ObjectQuery("SELECT * FROM Win32_PhysicalMemory");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    Logger.Debug("Capacity: {0}", queryObj["Capacity"]);
                    totalRAM += Convert.ToInt64(queryObj["Capacity"]);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get system memory", ex);
            }
            AttributeValue ram = new AttributeValue(_hostName, "ram", totalRAM.ToString());
            attributes.Add(ram);
            return attributes;

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