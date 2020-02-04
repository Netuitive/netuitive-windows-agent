using System;
using System.Configuration;
using System.Net;
using System.Threading;
using NLog;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Text;

namespace BloombergFLP.CollectdWin
{
    public static class Util
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static double toEpoch(DateTime time)
        {
            TimeSpan t = time - new DateTime(1970, 1, 1);
            double epoch = t.TotalMilliseconds / 1000;
            double rounded = Math.Round(epoch, 3);
            return rounded;

        }

        public static double GetNow()
        {
            return toEpoch(DateTime.Now);
        }

        public static string GetHostName()
        {
            var config = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : CollectdWinConfig");
            }
            if (config.GeneralSettings.Hostname.Length > 0)
                return config.GeneralSettings.Hostname;
            else
                return (Environment.MachineName.ToLower());
        }

        public static KeyValuePair<int, string> PostJson(string url, string userAgent, string payload, int maxRetries = 1)
        {
            string message = "";
            int statusCode = 200;

            int count = -1;
            int waitms = 500;

            while (++count < maxRetries)
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        client.Headers[HttpRequestHeader.ContentType] = "application/json";
                        client.Headers[HttpRequestHeader.UserAgent] = userAgent;
                        message = client.UploadString(url, "POST", payload);
                        return new KeyValuePair<int, string>(statusCode, message);
                    }
                }
                catch (System.Net.WebException ex)
                {
                    Exception baseex = ex.GetBaseException();
                    if (baseex as ThreadInterruptedException != null)
                    {
                        throw baseex;
                    }
                    else
                    {
                        message = ex.Message;
                        if (ex.Response as HttpWebResponse != null)
                        {
                            // Get the actual code
                            statusCode = ((HttpWebResponse)ex.Response).StatusCode.GetHashCode();
                        }
                        else
                        {
                            // use a generic client error
                            statusCode = 400;
                        }

                    }

                    if (statusCode < 500 || statusCode >= 600) {
                        // Do not retry
                        // HTTP return code is used as event log id
                        LogEventInfo logEvent = LogEventInfo.Create(LogLevel.Error, Logger.Name, String.Format("Error posting payload to {0}", url), ex);
                        logEvent.Properties.Add("EventID", statusCode);
                        Logger.Log(logEvent);
                        break;
                    }

                    Logger.Info("Attempt {0} failed with status {1}. Retrying in {2}ms", count, statusCode, waitms);
                    Thread.Sleep(waitms);
                    waitms *= 2;
                }
            }

            return new KeyValuePair<int, string>(statusCode, message);
        }

        public static string SerialiseJsonObject(Object obj, Type type)
        {
            MemoryStream stream = new MemoryStream();
            DataContractJsonSerializer ser = new DataContractJsonSerializer(type);
            ser.WriteObject(stream, obj);
            string json = Encoding.Default.GetString(stream.ToArray());
            return json;

        }
    }
}
// ----------------------------------------------------------------------------
// Copyright (C) 2017 Netuitive Inc.
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
// ---------------

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