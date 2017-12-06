using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using NLog;
using System.Text.RegularExpressions;
using BloombergFLP.CollectdWin;
using System.ServiceProcess;
using System.Linq;

namespace Netuitive.CollectdWin
{

    internal class ReadSystemChecksPlugin : ICollectdReadPlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private IList<SystemCheckConfig> _checks;
        private string _hostName;
        private int _interval;
        private bool _sendAgentHeartbeat;
        private int _heartbeatInterval;
        public ReadSystemChecksPlugin()
        {
            _checks = new List<SystemCheckConfig>();
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
            _heartbeatInterval = (int)Math.Ceiling(_interval * Math.Max(1.0,config.HeartbeatTTLMultiplier));
            Logger.Info("Agent heartbeat enabled: {0}, interval: {1}secs", _sendAgentHeartbeat, _heartbeatInterval);


            foreach (SystemCheckConfig check in config.Checks)
            {
                _checks.Add(check);
                Logger.Info("Added {0} '{1}' as '{2}' with interval {3} secs", check.GetType().Name , check.Name, check.Alias, check.GetTTL(_interval));
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
                checkList.Add(createCheck("heartbeat", _heartbeatInterval));
            }

            checkList.AddRange(CheckServices());
            checkList.AddRange(CheckProcesses());

            return checkList;
        }

        private List<CollectableValue> CheckServices()
        {
            List<CollectableValue> checkList = new List<CollectableValue>();

            List<ServiceCheckConfig> serviceChecks = _checks.OfType<ServiceCheckConfig>().ToList();

            if (serviceChecks.Count > 0)
            {
                ServiceController[] serviceList = ServiceController.GetServices();

                foreach (SystemCheckConfig checkConfig in serviceChecks)
                {
                    Regex regex = new Regex(checkConfig.Name, RegexOptions.None);

                    List<ServiceController> matches = serviceList.Where(service => regex.IsMatch(service.ServiceName)).ToList();
                    foreach (ServiceController foundService in matches)
                    {
                        if (foundService.Status == ServiceControllerStatus.Running)
                        {
                            string checkName = String.IsNullOrWhiteSpace(checkConfig.Alias) ? foundService.ServiceName : checkConfig.Alias;
                            Logger.Debug("Creating check for service: {0}", checkName);
                            checkList.Add(createCheck(checkName, checkConfig.GetTTL(_interval)));
                        }
                        else
                        {
                            Logger.Warn("Service '{0}' not running. Check not created.", checkConfig.Name);
                        }
                    }

                    if (matches.Count == 0)
                    {
                        Logger.Warn("No services matching '{0}' were found. Check not created.", checkConfig.Name);
                    }
                }
            }
            return checkList;
        }

        private List<CollectableValue> CheckProcesses()
        {
            List<CollectableValue> checkList = new List<CollectableValue>();
            

            List<ProcessCheckConfig> processChecks = _checks.OfType<ProcessCheckConfig>().ToList();

            if (processChecks.Count > 0)
            {
                Process[] processList = Process.GetProcesses();

                foreach (SystemCheckConfig checkConfig in processChecks)
                {
                    Regex regex = new Regex(checkConfig.Name, RegexOptions.None);

                    List<Process> matches = processList.Where(process =>
                    {
                        return regex.IsMatch(process.ProcessName);
                    }).ToList();

                    foreach (Process foundProcess in matches)
                    {
                        string checkName = String.IsNullOrWhiteSpace(checkConfig.Alias) ? foundProcess.ProcessName : checkConfig.Alias;
                        Logger.Debug("Creating check for process: {0}", checkName);
                        checkList.Add(createCheck(checkName, checkConfig.GetTTL(_interval)));
                    }

                    if (matches.Count == 0)
                    {
                        Logger.Warn("No processes matching '{0}' were found. Check not created.", checkConfig.Name);
                    }

                }
            }
            return checkList;
        }

                {
                }
            }

            return checkList;
        }
        private CheckValue createCheck(string name, int interval)
        {
            string cleanName = name.Replace(",", "_");
            CheckValue check = new CheckValue
            {
                HostName = _hostName,
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