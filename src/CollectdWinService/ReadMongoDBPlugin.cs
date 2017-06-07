using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using NLog;
using System.Text.RegularExpressions;
using System.Threading;
using BloombergFLP.CollectdWin;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Netuitive.CollectdWin
{

    internal class ReadMongoDBPlugin : ICollectdReadPlugin
    {
        private MongoClient client;

        private String basePrefix;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private String _connectionString;
        private String _databaseExpr;

        private string _hostName;

        public ReadMongoDBPlugin()
        {
        }

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("ReadMongoDB") as ReadMongoDBPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : ReadMongoDB");
            }

            _hostName = Util.GetHostName();

            _connectionString = config.ConnectionString;
            _databaseExpr = config.Databases;

            Logger.Info("ReadMongoDBPlugin plugin configured");
        }

        public void Start()
        {
            Logger.Info("ReadMongoDBPlugin plugin started");

            client = new MongoClient(_connectionString);
        }

        public void Stop()
        {
            Logger.Info("ReadMongoDBPlugin plugin stopped");
        }

        public IList<CollectableValue> Read()
        {

            var metricValueList = new List<CollectableValue>();

            var cursor = client.ListDatabases();

            var databaseList = cursor.ToList<BsonDocument>();

            if (databaseList.Count == 0)
            {
                return metricValueList;
            }

            //TODO should this be provided in config?
            var firstdb = databaseList[0];
            
            var serverStatus = new BsonDocument {
                {"serverStatus", true }
            };

            var doc = client.GetDatabase(firstdb.GetValue("name").AsString).RunCommand<BsonDocument>(serverStatus);

            metricValueList.AddRange(bsonToMetrics(doc, "opcounters", "", "counter"));
            metricValueList.AddRange(bsonToMetrics(doc, "opcountersRepl", "", "counter"));
            metricValueList.AddRange(bsonToMetrics(doc, "network", "", "counter"));
            metricValueList.AddRange(bsonToMetrics(doc, "extra_info", "", new List<String>() { "page_faults" }, "counter"));

            // Simple
            metricValueList.AddRange(bsonToMetrics(doc, "connections", "", "count"));
            metricValueList.AddRange(bsonToMetrics(doc, "globalLock", "", "count"));
            metricValueList.AddRange(bsonToMetrics(doc, "indexCounters", "", "count"));

            // For parity with linux mongo agent:
            //  Add support for replicated sets
            //  Add support for SSL connectivity
            //  Add collection of lock metrics (disabled by default)

            var dbStatsCommand = new BsonDocument {
                {"dbStats", true }
            };

            Regex dbMatcher = new Regex(_databaseExpr, RegexOptions.None);

            foreach (var database in databaseList)
            {
                
                var dbName = database.GetValue("name").AsString;

                if (dbMatcher.IsMatch(dbName))
                {
                    var dbConn = client.GetDatabase(dbName);
                    var dbStats = dbConn.RunCommand<BsonDocument>(dbStatsCommand);
                    doc.Add(dbName, dbStats);

                    /* Not currently requesting collection metrics
                    var collectionCursor = dbConn.ListCollections();
                    var collectionList = collectionCursor.ToList<BsonDocument>();

                    foreach (var collection in collectionList)
                    {
                        var collectionName =  collection.GetValue("name").AsString;
                        var collStatsCommand = new BsonDocument {
                            {"collStats", collectionName }
                        };

                        var collectionStats = dbConn.RunCommand<BsonDocument>(collStatsCommand);

                        doc.GetValue(dbName).AsBsonDocument.Add(collectionName, collectionStats);
                    }
                    */
                    metricValueList.AddRange(bsonToMetrics(doc, dbName, "", "count"));
                }
            }
            return metricValueList;
        }

        private List<MetricValue> bsonToMetrics(BsonDocument doc, String key, String prefix, String typeName)
        {
            return bsonToMetrics(doc, key, prefix, new List<String>(), typeName);
        }

        private List<MetricValue> bsonToMetrics(BsonDocument doc, String key, String prefix, List<String> filter, String typeName)
        {

            var newprefix = prefix == "" ? key : String.Join(".", new String[] { prefix, key });
            var metricList = new List<MetricValue>();

            BsonValue value;
            bool res = doc.TryGetValue(key, out value);

            if (!res)
            {
                return metricList;
            }

            if (value.BsonType == BsonType.Document)
            {
                foreach (BsonElement child in value.AsBsonDocument)
                {
                    switch (child.Value.BsonType)
                    {
                        case BsonType.Document:
                            metricList.AddRange(bsonToMetrics(value.AsBsonDocument, child.Name, newprefix, filter, typeName));
                            break;
                        case BsonType.Double:
                        case BsonType.Int32:
                        case BsonType.Int64:
                            if (filter.Count == 0 || filter.Contains(child.Name))
                            {
                                metricList.Add(bsonValueToMetric(child.Value, "mongo", newprefix, child.Name, typeName));
                            }
                            break;
                        default:
                            break;
                    }
                }

            }

            return metricList;
        }

        private MetricValue bsonValueToMetric(BsonValue value, String pluginName, String instanceName, String name, String typeName)
        {
            var metric = new MetricValue
            {
                HostName = _hostName,
                PluginName = pluginName,
                PluginInstanceName = instanceName,
                TypeName = typeName,
                TypeInstanceName = name,
                Values = new double[1] { value.ToDouble() },
                FriendlyNames = new string[1] { "" },
                Epoch = Util.toEpoch(DateTime.UtcNow)
            };

            return metric;
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
// ----------------------------- END-OF-FILE ----------------------------------