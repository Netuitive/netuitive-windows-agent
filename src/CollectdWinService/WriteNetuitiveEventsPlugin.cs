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
    internal class WriteNetuitiveEventsPlugin : ICollectdWritePlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string _eventIngestUrl;
        private string _location;
        private string _elementType;
        private int _payloadSize;
        private int _maxLength;
        private const string EVENT_JSON_FORMAT = @"{{""type"": ""{0}"", ""source"":""{1}"", ""data"":{{""elementId"":""{2}""}}, ""title"":""{3}"", ""timestamp"": {4} }}";

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("WriteNetuitiveEvents") as WriteNetuitiveEventsPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : WriteNetuitive");
            }

            _eventIngestUrl = config.Url;
            Logger.Info("Posting events to: {0}", _eventIngestUrl);

            _payloadSize = config.PayloadSize;
            if (_payloadSize < 0)
                _payloadSize = 99999;
            else if (_payloadSize == 0)
                _payloadSize = 25;

            Logger.Info("Maximum payload size: {0}", _payloadSize);

            _maxLength = config.MaxLength;
        }

        public void Start()
        {
            Logger.Info("WriteNetuitiveEvents plugin started");
        }

        public void Stop()
        {
            Logger.Info("WriteNetuitiveEvents plugin stopped");
        }

        public void Write(Queue<CollectableValue> values)
        {
            // Split the complete list into configured batch size

            List<string> jsonList = new List<string>();
            int ix = 0;
            foreach (CollectableValue value in values) {
                ix++;
                    if (value is EventValue)
                    {
                        EventValue ev = (EventValue)value;
                        string message = ev.Level + " - " + ev.Message;
                        if (message.Length > _maxLength)
                            message = message.Substring(0, _maxLength);

                        message = message.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "");
                        //TODO - once other types are supported make dynamic
                        string json = String.Format(EVENT_JSON_FORMAT, "INFO", "", ev.HostName, message, ev.Timestamp * 1000);

                        jsonList.Add(json);
                    }
                else
                {
                    // Collectable value type not handled by this adapter
                }

                // Send payload if reached end or max size
                if (jsonList.Count == _payloadSize  || (ix == values.Count && jsonList.Count > 0)) {
                    string payload = "[" + string.Join(",", jsonList.ToArray()) + "]";
                    string res = Util.PostJson(_eventIngestUrl, payload);
                    if (res.Length > 0)
                    {
                        Logger.Error("Error posting events: {0}", res);
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

        public string GetEventJsonStr(EventValue value)
        {
            return value.getJSON();
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