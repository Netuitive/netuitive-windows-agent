using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using NLog;

namespace BloombergFLP.CollectdWin
{
    internal class MetricsCollector
    {
        private const int MaxQueueSize = 30000;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly Aggregator _aggregator;
        private readonly int _interval;
        private readonly Queue<CollectableValue> _collectedValueQueue;
        private readonly IList<ICollectdPlugin> _plugins;
        private readonly Object _queueLock;
        private readonly int _timeout;
        private Thread _aggregatorThread;
        private Thread _readThread;
        private bool _runAggregatorThread;
        private bool _runReadThread, _runWriteThread;
        private Thread _writeThread;

        public MetricsCollector()
        {
            var config = ConfigurationManager.GetSection("CollectdWinConfig") as CollectdWinConfig;
            if (config == null)
            {
                Logger.Error("Cannot get configuration section");
                return;
            }

            _runReadThread = false;
            _runWriteThread = false;

            var registry = new PluginRegistry();
            _plugins = registry.CreatePlugins();

            _interval = config.GeneralSettings.Interval;
            if (_interval <= 10)
                _interval = 10;

            _timeout = config.GeneralSettings.Timeout;
            if (_timeout <= _interval)
                _timeout = _interval*3;

            bool storeRates = config.GeneralSettings.StoreRates;

            _aggregator = new Aggregator(_timeout, storeRates);

            _collectedValueQueue = new Queue<CollectableValue>();
            _queueLock = new Object();
        }

        public void ConfigureAll()
        {
            Logger.Trace("ConfigureAll() begin");
            Logger.Info("Configuring plugins");
            foreach (ICollectdPlugin plugin in _plugins)
                plugin.Configure();
            Logger.Trace("ConfigureAll() return");
        }

        public void StartAll()
        {
            Logger.Trace("StartAll() begin");
            Logger.Info("Starting plugins");
            foreach (ICollectdPlugin plugin in _plugins)
                plugin.Start();

            _runWriteThread = true;
            _writeThread = new Thread(WriteThreadProc);
            _writeThread.Start();

            _runReadThread = true;
            _readThread = new Thread(ReadThreadProc);
            _readThread.Start();

            _runAggregatorThread = true;
            _aggregatorThread = new Thread(AggregatorThreadProc);
            _aggregatorThread.Start();
            Logger.Trace("StartAll() return");
        }

        public void StopAll()
        {
            Logger.Trace("StopAll() begin");
            _runReadThread = false;
            _runWriteThread = false;
            _runAggregatorThread = false;

            _readThread.Interrupt();
            _writeThread.Interrupt();
            _aggregatorThread.Interrupt();

            foreach (ICollectdPlugin plugin in _plugins)
                plugin.Stop();
            Logger.Trace("StopAll() end");
        }

        private void ReadThreadProc()
        {
            Logger.Trace("ReadThreadProc() begin");
            int numMetricsDropped = 0;
            while (_runReadThread)
            {
                try
                {
                    double collectionStart = Util.GetNow();
                    foreach (ICollectdPlugin plugin in _plugins)
                    {
                        var readPlugin = plugin as ICollectdReadPlugin;
                        if (readPlugin == null)
                        {
                            // skip if plugin is not a readplugin, it might be a writeplugin
                            continue;
                        }

                        double start = Util.GetNow();
                        IList<CollectableValue> collectedValues = readPlugin.Read();

                        double end = Util.GetNow();
                        Logger.Info("{0} read {1} items in {2:0.00}s", readPlugin.GetType().Name, collectedValues.Count, end - start);

                        if (collectedValues == null || !collectedValues.Any())
                            continue;


                        lock (_queueLock)
                        {
                            foreach (CollectableValue metric in collectedValues)
                            {
                                _collectedValueQueue.Enqueue(metric);
                                while (_collectedValueQueue.Count >= MaxQueueSize)
                                {
                                    // When queue size grows above the Max limit, 
                                    // old entries are removed
                                    _collectedValueQueue.Dequeue();
                                    if ((++numMetricsDropped%1000) == 0)
                                    {
                                        Logger.Error("Number of metrics dropped : {0}", numMetricsDropped);
                                    }
                                }
                            }
                        }
                    }
                    double collectionEnd = Util.GetNow();
                    double elapsed = collectionEnd - collectionStart;
                    double revisedInterval = (_interval - elapsed) * 1000;
                    if (revisedInterval / _interval < 0.1)
                    {
                        Logger.Error("Read thread took {0} seconds out of {1} second cycle", elapsed, _interval);
                    }
                    if (revisedInterval > 0)
                        Thread.Sleep((int)revisedInterval);
                }
                catch (ThreadInterruptedException)
                {
                    Logger.Info("Read thread interrupted");
                }
                catch (Exception exp)
                {
                    Logger.Error("ReadThreadProc() got exception : ", exp);
                    Thread.Sleep(_interval * 1000);
                }
            }
            Logger.Trace("ReadThreadProc() return");
        }

        private void WriteThreadProc()
        {
            Logger.Trace("WriteThreadProc() begin");
            // Wait a few seconds to give read thread a chance to get metrics.
            // Otherwise write thread is always pushing metrics _interval seconds after they were read
            Thread.Sleep(15000); 
            while (_runWriteThread)
            {
                try
                {
                    double writeStart = Util.GetNow();
                    int numValues = _collectedValueQueue.Count;
                    if (numValues > 0)
                    {
                        // Transfer current queue contents to working list
                        // Individual write plugins can choose how to handle the list of collectable values.
                        Queue<CollectableValue> collectedValues = new Queue<CollectableValue>();
                        lock (_queueLock)
                        {
                            while (_collectedValueQueue.Count > 0)
                            {
                                collectedValues.Enqueue(_collectedValueQueue.Dequeue());
                            }
                        }
                        if (collectedValues.Count > 0) {
                            foreach (CollectableValue collectedValue in collectedValues)
                            {
                                collectedValue.Interval = _interval;
                                if (collectedValue is MetricValue)
                                {
                                    MetricValue metricValue = (MetricValue)collectedValue;
                                    _aggregator.Aggregate(ref metricValue);
                                }
                            }

                            foreach (ICollectdPlugin plugin in _plugins)
                            {
                                var writePlugin = plugin as ICollectdWritePlugin;
                                if (writePlugin == null)
                                {
                                    // skip if plugin is not a writeplugin
                                    continue;
                                }
                                writePlugin.Write(collectedValues);
                            }
                        }
                    }
                    double writeEnd = Util.GetNow();
                    Logger.Info("Written {0} values in {1:0.00}s", numValues, (writeEnd - writeStart));
                    
                    double elapsed = writeEnd - writeStart;
                    double revisedInterval = (_interval - elapsed) * 1000;
                    if (revisedInterval / _interval < 0.1)
                    {
                        Logger.Error("Write thread took {0} seconds out of {1} second cycle", elapsed, _interval);
                    }
                    if (revisedInterval > 0)
                        Thread.Sleep((int)revisedInterval);

                }
                catch (ThreadInterruptedException)
                {
                    Logger.Info("Write thread interrupted");
                }
                catch (Exception exp)
                {
                    Logger.Error("WriteThreadProc() got exception : ", exp);
                    Thread.Sleep(_interval * 1000);
                }
            }
            Logger.Trace("WriteThreadProc() return");
        }

        private void AggregatorThreadProc()
        {
            Logger.Trace("AggregatorThreadProc() begin");
            while (_runAggregatorThread)
            {
                try
                {
                    _aggregator.RemoveExpiredEntries();
                    Thread.Sleep(_timeout*1000);
                }
                catch (ThreadInterruptedException)
                {
                    Logger.Info("Aggregator thread interrupted");
                }
                catch (Exception exp)
                {
                    Logger.Error("AggregatorThreadProc() got exception : ", exp);
                }
            }
            Logger.Trace("AggregatorThreadProc() return");
        }
    }
}

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