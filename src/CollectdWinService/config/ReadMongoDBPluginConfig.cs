using System;
using System.Configuration;
using BloombergFLP.CollectdWin;

namespace Netuitive.CollectdWin
{
    internal class ReadMongoDBPluginConfig : CollectdPluginConfig
    {
        [ConfigurationProperty("ConnectionString", IsRequired = true)]
        public string ConnectionString
        {
            get { return (string)base["ConnectionString"]; }
            set { base["ConnectionString"] = value; }
        }

        [ConfigurationProperty("Databases", IsRequired = false, DefaultValue = "$^")]
        public string Databases
        {
            get { return (string)base["Databases"]; }
            set { base["Databases"] = value; }
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