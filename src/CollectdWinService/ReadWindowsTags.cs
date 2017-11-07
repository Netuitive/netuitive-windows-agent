using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using NLog;
using BloombergFLP.CollectdWin;
using System.Management;
using System.Reflection;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Net.Sockets;

namespace Netuitive.CollectdWin
{
    internal struct Tag
    {
        public string name;
        public string value;
    }

    internal class ReadWindowsTagsPlugin : ICollectdReadPlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IList<Tag> _tags;
        private string _hostName;

        public ReadWindowsTagsPlugin()
        {
            _tags = new List<Tag>();
        }

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("ReadWindowsTags") as ReadWindowsTagsPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : ReadWindowsTags");
            }

            _hostName = Util.GetHostName();
            _tags.Clear();

            foreach (TagConfig tagConfig in config.Tags)
            {
                Tag tag = new Tag
                {
                    name = tagConfig.Name,
                    value = tagConfig.Value
                };
                _tags.Add(tag);
                Logger.Info("Added tag {0}: {1}", tagConfig.Name, tagConfig.Value);

            }
            Logger.Info("ReadWindowsTags plugin configured");
        }

        public void Start()
        {
            Logger.Info("ReadWindowsTags plugin started");
        }

        public void Stop()
        {
            Logger.Info("ReadWindowsTags plugin stopped");
        }

        public IList<CollectableValue> Read()
        {
            var collectedValueList = new List<CollectableValue>();

            return collectedValueList;
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