﻿using System;
using NLog;
using System.Configuration;
using System.Net;
using System.Collections.Generic;
using System.Net.Sockets;
using BloombergFLP.CollectdWin;

namespace Netuitive.CollectdWin
{
    internal class WriteStatsdPlugin : ICollectdWritePlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string _host;
        private int _port;
        private string _prefix;

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("WriteStatsd") as WriteStatsdPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : WriteStatsd");
            }

            _host = config.Host;
            _prefix = config.Prefix;
            _port = config.Port;
            Logger.Info("Posting to: {0}:{1}", _host, _port);
            Logger.Info("Namespace prefix: {0}", _prefix, _port);

        }

        public void Start()
        {
            Logger.Info("WriteStatsdPlugin plugin started");
        }

        public void Stop()
        {
            Logger.Info("WriteStatsdPlugin plugin stopped");
        }

        public void Write(CollectableValue value)
        {
            // This Write Plugin only knows about metrics
            if (!(value is MetricValue))
                return;

            MetricValue metric = (MetricValue)value;
            //Rough first pass
            //TODO - handle multiple metrics
            //TODO - check type 
            string bucket = "";
            if (_prefix.Trim().Length > 0)
                bucket += _prefix.Trim() + ".";
            
            bucket += metric.HostName + "." + metric.PluginName;
            if (metric.PluginInstanceName.Length > 0)
                bucket += "." + metric.PluginInstanceName;

            bucket += "." + metric.TypeInstanceName;

            // Remove : and | from bucket names
            bucket = bucket.Replace(":", "_").Replace("|", "_").Replace(" ", "_");
            string payload = bucket + ":" + metric.Values[0] + "|g";

            if (metric.Values.Length > 1)
            {
                Logger.Warn("Multiple counters not handled yet - only posting first");
            }
            Logger.Debug("WriteStatsdPlugin: {0}", payload);

            using (var client = new UdpClient())
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse(_host), _port);
                client.Connect(ep);

                byte[] data = System.Text.Encoding.UTF8.GetBytes (payload);
                // Fire and forget
                client.Send(data, data.Length);
            }
        }

        public void Write(Queue<CollectableValue> values)
        {
            try
            {
                foreach (CollectableValue value in values)
                {
                    Write(value);
                }
            }
            catch (Exception ex)
            {
                LogEventInfo logEvent = new LogEventInfo(LogLevel.Error, Logger.Name, "WriteStatsD failed");
                logEvent.Exception = ex;
                logEvent.Properties.Add("EventID", ErrorCodes.ERROR_UNHANDLED_EXCEPTION);
                Logger.Log(logEvent);
            }
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