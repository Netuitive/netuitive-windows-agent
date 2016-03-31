﻿using System;
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
        public int minLevel;
        public int maxLevel;
        public string filterExp;
        public string title;
        public int maxPerCycle;
        public int minEventId;
        public int maxEventId;
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
                EventQuery evt = new EventQuery
                {
                    log = eventConfig.Log,
                    source = eventConfig.Source,
                    filterExp = eventConfig.FilterExp,
                    minLevel = eventConfig.MinLevel,
                    maxLevel = eventConfig.MaxLevel,
                    maxPerCycle = eventConfig.MaxEventsPerCycle,
                    title = eventConfig.Title,
                    minEventId = eventConfig.MinEventId,
                    maxEventId = eventConfig.MaxEventId
                
                };
                _events.Add(evt);
                Logger.Info("Added event reader {0}: log:{1}, source:{2}, level:{3}-{4}, ID:{5}-{6}, maxAllowed:{7}", evt.title, evt.log, evt.source, evt.minLevel, evt.maxLevel, evt.minEventId, evt.maxEventId, evt.maxPerCycle);
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
            long totalEvents = 0;
            long collectionTime = (long)(Util.toEpoch(DateTime.UtcNow));
            List<long> recordIds = new List<long>();
            foreach (EventQuery eventQuery in _events)
            {
                List<EventRecord> records = GetEventRecords(eventQuery.minLevel, eventQuery.maxLevel, eventQuery.log, eventQuery.source);

                // Filter the events - event ID must be in target range, description must match regex and we mustn't have already read this event record ID in another query 
                Regex filterExp = new Regex(eventQuery.filterExp, RegexOptions.None);
                List<EventRecord> filteredRecords = records.FindAll(delegate(EventRecord record) {
                    return !recordIds.Contains((long)record.RecordId) && record.Id >= eventQuery.minEventId && record.Id <= eventQuery.maxEventId && filterExp.IsMatch(record.FormatDescription());
                });

                // Add these record IDs to dedupe list so we don't capture them again in a later query
                filteredRecords.ForEach(delegate(EventRecord record) { recordIds.Add((long)record.RecordId);});

                if (filteredRecords.Count <= eventQuery.maxPerCycle)
                {
                    foreach (EventRecord record in filteredRecords)
                    {
                        // Timestamp from record is machine time, not GMT
                        long timestamp = (long)(Util.toEpoch(record.TimeCreated.Value.ToUniversalTime()));
                        long id = (long)record.RecordId;
                        string message = record.FormatDescription();
                        EventValue newevent = new EventValue(_hostName, timestamp, record.Level.Value, eventQuery.title, message, id);
                        collectableValues.Add(newevent);
                        totalEvents++;
                    }
                }
                else 
                {
                    // Too many events - reduce to summary using attributes of the first
                    EventRecord record = filteredRecords[0];
                    string message = string.Format("Received {0} events", filteredRecords.Count);
                    string title = string.Format("{0} ({1} events)", eventQuery.title, filteredRecords.Count);
                    EventValue newevent = new EventValue(_hostName, collectionTime, record.Level.Value, title, message, 0);
                    collectableValues.Add(newevent);
                    totalEvents += filteredRecords.Count;
                }
            }   
            
            // Add event count metric
            MetricValue eventCountMetric = new MetricValue
            {
                HostName = _hostName,
                PluginName = "windows_events",
                PluginInstanceName = "",
                TypeName = "count",
                TypeInstanceName = "event_count",
                Values = new double[]{totalEvents},
                FriendlyNames = new string[]{"Windows Event Count"},
                Epoch = collectionTime
            };
            collectableValues.Add(eventCountMetric);
            return collectableValues;
        }

        // 1 = critical, 2=error, 3=warning, 4=information,5=verbose, -1=no filter
        private List<EventRecord> GetEventRecords(int minLevel, int maxLevel, string logName,string providerName) {
            List<EventRecord> eventRecords = new List<EventRecord>();
            EventRecord eventRecord;
            string queryString;

            try
            {
                if (providerName != null && providerName.Length > 0)
                {
                    queryString = String.Format("*[System[(Level >= {0}) and (Level <= {1}) and Provider/@Name = '{2}' and TimeCreated[timediff(@SystemTime) <= {3}]]]", minLevel, maxLevel, providerName, _interval * 1000);
                }
                else
                {
                    queryString = String.Format("*[System[(Level >= {0}) and (Level <= {1}) and TimeCreated[timediff(@SystemTime) <= {2}]]]", minLevel, maxLevel, _interval * 1000);
                }
                EventLogQuery query = new EventLogQuery(logName, PathType.LogName, queryString);
                EventLogReader reader = new EventLogReader(query);
                while ((eventRecord = reader.ReadEvent()) != null)
                {
                    // Filter out cases where level=0. This seems undocumented and the severity contradicts the displayed level
                    if (eventRecord.Level > 0)
                    {
                        eventRecords.Add(eventRecord);
                    }
                    else
                    {
                        Logger.Debug("Dropped event level 0: {0} - {1}", eventRecord.LevelDisplayName, eventRecord.FormatDescription());
                    }
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