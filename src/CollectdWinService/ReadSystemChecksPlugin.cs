using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using NLog;
using System.Text.RegularExpressions;
using BloombergFLP.CollectdWin;
using System.Management;
using System.Reflection;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Linq;

namespace Netuitive.CollectdWin
{
    internal struct CheckConfig
    {
        public string Name;
        public string Alias;
        public int Interval;
        public CheckType Type;
    }

    internal class ReadSystemChecksPlugin : ICollectdReadPlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IList<CheckConfig> _checks;
        private string _hostName;
        private int _interval;
        private bool _sendAgentHeartbeat;
        private int _heartbeatInterval;
        public ReadSystemChecksPlugin()
        {
            _checks = new List<CheckConfig>();
        }

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("ReadSystemChecks") as ReadSystemChecksPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : ReadWindowsAttributes");
            }

            var baseConfig = CollectdWinConfig.GetConfig();

            _interval = baseConfig.GeneralSettings.Interval;

            _sendAgentHeartbeat = config.EnableAgentHeartbeat;
            _heartbeatInterval = _interval * config.HeartbeatIntervalMultiplier;
            Logger.Info("Agent heartbeat enabled: {0}, interval: {1}secs", _sendAgentHeartbeat, _heartbeatInterval);

            foreach (SystemCheckConfig checkConfig in config.Checks)
            {
                CheckConfig check = new CheckConfig
                {
                    Name = checkConfig.Name,
                    Alias = String.IsNullOrWhiteSpace(checkConfig.Alias) ? checkConfig.Name : checkConfig.Alias,
                    Type = checkConfig.Type,
                    Interval = checkConfig.IntervalMultiplier * _interval
                };
                _checks.Add(check);
                Logger.Info("Added check for {0} '{1}' as '{2}' with interval {3} secs", check.Type, check.Name, check.Alias, check.Interval);
            }

            _hostName = Util.GetHostName();

            Logger.Info("ReadSystemChecks plugin configured");
        }

        public void Start()
        {
            Logger.Info("ReadSystemChecks plugin started");
        }

        public void Stop()
        {
            Logger.Info("ReadSystemChecks plugin stopped");
        }

        public IList<CollectableValue> Read()
        {
            var checkList = new List<CollectableValue>();

            if (_sendAgentHeartbeat)
            {
                checkList.Add(createCheck("heartbeat"));
            }

            checkList.AddRange(CheckServices());
            checkList.AddRange(CheckProcesses());

            return checkList;
        }

        private List<CollectableValue> CheckServices()
        {
            List<CollectableValue> checkList = new List<CollectableValue>();
            ServiceController[] serviceList = ServiceController.GetServices();
            foreach (CheckConfig checkConfig in _checks.Where(o => o.Type == CheckType.Service).ToList())
            {
                ServiceController foundService = serviceList.FirstOrDefault(service => service.ServiceName.Equals(checkConfig.Name));
                if (foundService != null)
                {
                    if (foundService.Status == ServiceControllerStatus.Running)
                    {
                        Logger.Debug("Creating check for service: {0}", checkConfig.Name);
                        checkList.Add(createCheck(checkConfig.Alias, checkConfig.Interval)); 
                    }
                    else
                    {
                        Logger.Warn("Service '{0}' not running. Check not created.", checkConfig.Name);
                    }
                }
                else
                {
                    Logger.Warn("Service '{0}' not found. Check not created.", checkConfig.Name);
                }
            }

            return checkList;
        }

        private List<CollectableValue> CheckProcesses()
        {
            List<CollectableValue> checkList = new List<CollectableValue>();
            
            Process[] processList = Process.GetProcesses();

            foreach (CheckConfig checkConfig in _checks.Where(o => o.Type == CheckType.Process).ToList())
            {
                Process foundProcess = processList.FirstOrDefault(process => process.MainModule.ModuleName.Equals(checkConfig.Name));
                if (foundProcess != null)
                {
                    Logger.Debug("Creating check for process: {0}", checkConfig.Name);
                    checkList.Add(createCheck(checkConfig.Alias, checkConfig.Interval));
                }
                else
                {
                    Logger.Warn("Process '{0}' not found. Check not created.", checkConfig.Name);
                }
                
            }

            return checkList;
        }

        private CheckValue createCheck(string name, int interval)
        {
            CheckValue check = new CheckValue
            {
                HostName = _hostName,
                Name = name,
                CheckInterval = _interval
                Name = cleanName,
                CheckInterval = interval
            };

            return check;

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