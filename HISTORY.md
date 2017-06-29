## v0.8.2
- Change default config to collect total processor metrics.

## v0.8.1
- Added MongoDB plugin (requires .NET CLR 4.0)
- Agent now requires .NET CLR 4.0 (see https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/versions-and-dependencies) 

## v0.7.4
- Fixed FQN in EC2 relationship (latest version to support .NET CLR 2.0)

## v0.7.3
- Fixed ReadStatsD plugin.
- Changed default configuration to enable ReadStatsD plugin and disable ReadWindowsEvents plugin

## v0.7.2
- Added support for warning events.

## v0.7.1
- Added friendly version of RAM attribute.
- Remove invalid characters from metric FQNs.
- Minor bug fixes.
- Changed to semantic versioning

## v0.6.5976
- Made event collection cycle configurable. Minor bugfixes

## v0.6.5966
- Increased detail in grouped event messages

## v0.6.5942
- Added IIS/.Net metrics to default configuration.

## v0.6.5938
- Fixed bug where some metrics returned 0 on reload.

## v0.6.5934
- Added event grouping and improved event filtering.

## v0.6.5921
- Added IP address to common attributes. Suppress empty event payloads.

## v0.6.5890
- Added API Key parameter for silent install.

## v0.6.5877
-Filter out undocumented windows level 0 events.

## v0.6.5828
- Reduced default event sensitivity. Added further memory metrics.

## v0.6.5819
- Added relationship support and EC2 instance metadata.

## v0.6.5786
- Various bug fixes.
## v0.6.5745
- Initial release