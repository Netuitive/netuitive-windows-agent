Netuitive Windows Agent
========================

The Netuitive Windows Agent leverages CollectdWin to collect, aggregate, and publish windows performance counters and attributes to Netuitive. It is designed to expose crucial metrics from your Windows machines and display them in a meaningful way in [Netuitive](https://http://www.netuitive.com/). The Netuitive Windows Agent is a fork of the CollectdWin project, which is similar in concept and design to [Collectd](https://collectd.org).

See the [Netuitive Windows agent docs](https://help.netuitive.com/Content/Misc/Datasources/new_jvm_datasource.htm) or the [wiki](../../wiki) for more information, or contact Netuitive support at [support@netuitive.com](mailto:support@netuitive.com).

Changes to CollectdWin
-----------------------

The core functionality of CollectdWin remains the same in our fork: exposing windows performance counters and attributes for collection and monitoring. The Netuitive Windows Agent diverges from CollectdWin when considering the different plugins available. Netuitive created plugins to read Windows events and attributes as well as plugins to write to [Netuitive](https://http://www.netuitive.com/) and [StatsD](https://github.com/etsy/statsd). Netuitive also changed much of the source code to allow for various other plugins, including the Read StatsD, Write HTTP, and Write AMQP.

