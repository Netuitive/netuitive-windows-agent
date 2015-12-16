using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using NLog;
using System.Text.RegularExpressions;
using BloombergFLP.CollectdWin;
using System.Management;
using System.Reflection;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;

namespace Netuitive.CollectdWin
{
    internal struct Attribute
    {
        public string name;
        public string variableName;
    }

    internal class ReadWindowsAttributesPlugin : ICollectdReadPlugin
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IList<Attribute> _attributes;
        private string _hostName;
        private bool _readEC2InstanceMetadata;

        public ReadWindowsAttributesPlugin()
        {
            _attributes = new List<Attribute>();
        }

        public void Configure()
        {
            var config = ConfigurationManager.GetSection("ReadWindowsAttributes") as ReadWindowsAttributesPluginConfig;
            if (config == null)
            {
                throw new Exception("Cannot get configuration section : ReadWindowsAttributes");
            }

            _hostName = Util.GetHostName();
            _readEC2InstanceMetadata = config.ReadEC2InstanceMetadata;
            _attributes.Clear();

            foreach (EnvironmentVariableConfig attr in config.EnvironmentVariables)
            {
                Attribute attribute = new Attribute
                {
                    name = attr.Name,
                    variableName = attr.Value
                };
                _attributes.Add(attribute);
                Logger.Info("Added attribute {0}: {1}", attr.Name, attr.Value);

            }
            Logger.Info("ReadWindowsAttributes plugin configured");
        }

        public void Start()
        {
            Logger.Info("ReadWindowsAttributes plugin started");
        }

        public void Stop()
        {
            Logger.Info("ReadWindowsAttributes plugin stopped");
        }

        public IList<CollectableValue> Read()
        {
            var collectedValueList = new List<CollectableValue>();

            collectedValueList.AddRange(GetCommonAttributes());

            if (_readEC2InstanceMetadata)
            {
                // This is only done once on the assumption that the EC2 would have to reboot for these values to change
                collectedValueList.AddRange(GetEC2Metadata());
                _readEC2InstanceMetadata = false;
            }
            foreach (Attribute attribute in _attributes)
            {
                try
                {
                    string value = Environment.GetEnvironmentVariable(attribute.variableName);
                    AttributeValue attr = new AttributeValue(_hostName, attribute.name, value);
                    attr.HostName = _hostName;
                    collectedValueList.Add(attr);

                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("Failed to collect attribute: {0}", attribute.variableName), ex);
                }
            }
            return collectedValueList;
        }


        private IList<CollectableValue> GetEC2Metadata()
        {
            IList<CollectableValue> values = new List<CollectableValue>();

            //This URL is not configurable - see http://docs.aws.amazon.com/AWSEC2/latest/UserGuide/ec2-instance-metadata.html
            string url = "http://169.254.169.254/latest/dynamic/instance-identity/document";

            try
            {
                // Using HttpWebRequest instead of WebClient so we can set the timeout to something less than default 100 seconds
                var http = (HttpWebRequest)WebRequest.Create(url);
                http.Timeout = 5000; // This URL is locally routed so should respond v. fast. If it doesn't chances are this isn't an EC2.
                var response = http.GetResponse();
                EC2InstanceIdentity ec2;

                using (var stream = response.GetResponseStream())
                {
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(EC2InstanceIdentity));
                    ec2 = (EC2InstanceIdentity)ser.ReadObject(stream);
                }

                if (ec2 != null)
                {
                    // Deserialize json response into attribute pairs
                    values.Add(new AttributeValue(_hostName, "accountId", ec2.accountId));
                    values.Add(new AttributeValue(_hostName, "architecture", ec2.architecture));
                    values.Add(new AttributeValue(_hostName, "availabilityZone", ec2.availabilityZone));
                    values.Add(new AttributeValue(_hostName, "billingProducts", String.Join(",", ec2.billingProducts)));
                    values.Add(new AttributeValue(_hostName, "devpayProductCodes", ec2.devpayProductCodes));
                    values.Add(new AttributeValue(_hostName, "imageId", ec2.imageId));
                    values.Add(new AttributeValue(_hostName, "instanceId", ec2.instanceId));
                    values.Add(new AttributeValue(_hostName, "instanceType", ec2.instanceType));
                    values.Add(new AttributeValue(_hostName, "kernelId", ec2.kernelId));
                    values.Add(new AttributeValue(_hostName, "pendingTime", ec2.pendingTime));
                    values.Add(new AttributeValue(_hostName, "privateIp", ec2.privateIp));
                    values.Add(new AttributeValue(_hostName, "ramdiskId", ec2.ramdiskId));
                    values.Add(new AttributeValue(_hostName, "region", ec2.region));
                    values.Add(new AttributeValue(_hostName, "version", ec2.version));

                    // Create relationship pair
                    values.Add(new RelationValue(_hostName, String.Format("{0}:{1}", ec2.region, ec2.instanceId)));
                }
                else
                {
                    Logger.Warn("Failed to get EC2 instance metadata. If this server is not an EC2 update the ReadWindowsAttributes.config file to disable collection.");
                }            
            }
            catch (System.Net.WebException ex)
            {
                Logger.Warn("Failed to get EC2 instance metadata. If this server is not an EC2 update the ReadWindowsAttributes.config file to disable collection.", ex);
            }

            return values;
        }

        private IList<CollectableValue> GetCommonAttributes()
        {
            // Return standard attributes
            IList<CollectableValue> attributes = new List<CollectableValue>();

            Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            AttributeValue numProcessors = new AttributeValue(_hostName, "cpus", Environment.ProcessorCount.ToString());
            attributes.Add(numProcessors);

            AttributeValue osVersion = new AttributeValue(_hostName, "osversion", Environment.OSVersion.ToString());
            attributes.Add(osVersion);

            AttributeValue agent = new AttributeValue(_hostName, "agent", "collectdwin-" + fvi.FileVersion);
            attributes.Add(agent);

            long totalRAM = 0;
            try
            {
                ConnectionOptions connection = new ConnectionOptions();
                connection.Impersonation = ImpersonationLevel.Impersonate;
                ManagementScope scope = new ManagementScope("\\\\.\\root\\CIMV2", connection);
                scope.Connect();
                ObjectQuery query = new ObjectQuery("SELECT * FROM Win32_PhysicalMemory");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);
                foreach (ManagementObject queryObj in searcher.Get())
                {                    
                    totalRAM += Convert.ToInt64(queryObj["Capacity"]);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to get system memory", ex);
            }
            AttributeValue ram = new AttributeValue(_hostName, "ram bytes", totalRAM.ToString());
            attributes.Add(ram);
            return attributes;

        }
    }
}

// ******************** DataContract objects for JSON serialisation ******************** 
/*{
  "instanceId" : "i-32b83cdb",
  "billingProducts" : [ "bp-6ba54002" ],
  "accountId" : "973100236690",
  "imageId" : "ami-478d782c",
  "instanceType" : "t2.micro",
  "kernelId" : null,
  "ramdiskId" : null,
  "pendingTime" : "2015-07-17T14:22:35Z",
  "architecture" : "x86_64",
  "region" : "us-east-1",
  "version" : "2010-08-31",
  "availabilityZone" : "us-east-1e",
  "privateIp" : "172.31.6.181",
  "devpayProductCodes" : null
}
*/
[DataContract]
class EC2InstanceIdentity
{
    [DataMember]
    public string instanceId { get; set; }
    [DataMember]
    public string[] billingProducts { get; set; }
    [DataMember]
    public string accountId { get; set; }
    [DataMember]
    public string imageId { get; set; }
    [DataMember]
    public string instanceType { get; set; }
    [DataMember]
    public string kernelId { get; set; }
    [DataMember]
    public string ramdiskId { get; set; }
    [DataMember]
    public string pendingTime { get; set; }
    [DataMember]
    public string architecture { get; set; }
    [DataMember]
    public string region { get; set; }
    [DataMember]
    public string version { get; set; }
    [DataMember]
    public string availabilityZone { get; set; }
    [DataMember]
    public string privateIp { get; set; }
    [DataMember]
    public string devpayProductCodes { get; set; }

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