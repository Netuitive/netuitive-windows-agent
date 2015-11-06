using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using NLog;
using BloombergFLP.CollectdWin;
using System.Text.RegularExpressions;

namespace Netuitive.CollectdWin
{
    internal struct EventQuery
    {
        public string log;
        public string source;
        public int maxLevel;
        public string filterExp;
    }

    internal class ReadWindowsEventsPlugin : ICollectdReadPlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IList<EventQuery> _events;
        private string _hostName;
        private int _interval;

        public ReadWindowsEventsPlugin()
        {
            _events = new List<EventQuery>();
        }

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("ReadWindowsEvents") as ReadWindowsEventPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : WindowsEvents");
            }
            var baseConfig = CollectdWinConfig.GetConfig();

            _interval = baseConfig.GeneralSettings.Interval;
            _hostName = Util.GetHostName();
            _events.Clear();
            
            
            foreach (WindowsEventConfig eventConfig in config.Events)
            {
                int level = EventValue.levelToInt(eventConfig.MaxLevel);

                EventQuery evt = new EventQuery
                {
                    log = eventConfig.Log,
                    source = eventConfig.Source,
                    filterExp = eventConfig.FilterExp,
                    maxLevel = level
                };
                _events.Add(evt);
                Logger.Info("Added event reader: {0}, {1}, {2}", evt.log, evt.source, evt.maxLevel);
            }
            Logger.Info("ReadWindowsEvents plugin configured");
            
        }

        public void Start()
        {
            Logger.Info("ReadWindowsEvents plugin started");
        }

        public void Stop()
        {
            Logger.Info("ReadWindowsEvents plugin stopped");
        }

        public IList<CollectableValue> Read()
        {
            IList<CollectableValue> collectableValues = new List<CollectableValue>();
            foreach (EventQuery eventQuery in _events)
            {
                List<EventRecord> records = GetEventRecords(eventQuery.maxLevel, eventQuery.log, eventQuery.source);
                foreach (EventRecord record in records)
                {
                    // Timestamp from record is machine time, not GMT
                    long timestamp = (long)(Util.toEpoch(record.TimeCreated.Value.ToUniversalTime()));
                    long id = (long)record.RecordId;
                    string message = record.FormatDescription();
                    EventValue newevent = new EventValue(_hostName, timestamp, record.Level.Value, message, id);

                    // Dedupe
                    bool add = true;
                    foreach(EventValue ev in collectableValues) {
                        if (ev.Equals(newevent))
                        {
                            add = false;
                            break;
                        }
                    }

                    // Filter
                    if (add)
                    {
                        Regex regex = new Regex(eventQuery.filterExp, RegexOptions.None);
                        add &= regex.IsMatch(message);
                    }

                    if (add)
                        collectableValues.Add(newevent);
                }
            }   
             
            return collectableValues;
        }

        // 1 = critical, 2=error, 3=warning, 4=information,5=verbose, -1=no filter
        private List<EventRecord> GetEventRecords(int level, string logName,string providerName) {
            List<EventRecord> eventRecords = new List<EventRecord>();
            EventRecord eventRecord;
            string queryString;
            try
            {
                if (providerName != null && providerName.Length > 0)
                {
                    queryString = "*";
                    if (level >= 0)
                        queryString += String.Format("[System/Level <= {0}]", level);

                    queryString += String.Format("[System/Provider/@Name = '{0}'][System/TimeCreated[timediff(@SystemTime) <= {1}]]", providerName, _interval * 1000);
                }
                else
                {
                    queryString = "*[System[";
                    if (level >= 0)
                        queryString += String.Format("(Level <= {0}) and ", level);
                    queryString += String.Format(
                        "TimeCreated[timediff(@SystemTime) <= {0}]]]", _interval * 1000);
                }
                EventLogQuery query = new EventLogQuery(logName, PathType.LogName, queryString);
                EventLogReader reader = new EventLogReader(query);
                while ((eventRecord = reader.ReadEvent()) != null)
                {
                    eventRecords.Add(eventRecord);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            return eventRecords;
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