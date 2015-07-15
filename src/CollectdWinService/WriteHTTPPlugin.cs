using System;
using NLog;
using System.Configuration;
using System.Net;
using System.Collections.Generic;

namespace BloombergFLP.CollectdWin
{
    internal class WriteHTTPPlugin : ICollectdWritePlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string _url;

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : CollectdWinConfig");
            }

            string url = config.WriteHTTP.Url;
            Logger.Info("Posting to: {0}", url);

            _url = url;

        }

        public void Start()
        {
            Logger.Info("WriteHTTP plugin started");
        }

        public void Stop()
        {
            Logger.Info("WriteHTTP plugin stopped");
        }

        public void Write(CollectableValue value)
        {
            // This Write Plugin only knows about metrics
            if (!(value is MetricValue))
                return;

            string payload = "[" + value.getJSON() + "]";
            Logger.Debug("WriteHTTPPlugin: {0}", payload);

            string result = "";
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                result = client.UploadString(_url, "POST", payload);
            }
            Logger.Debug("response: {0}", result);
        }

        public void Write(Queue<CollectableValue> values)
        {
            foreach (CollectableValue value in values)
            {
                Write(value);
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