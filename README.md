# NServiceBus.DataBackplane

This is an implementation of a data backplane for NServiceBus endpoints. It allows the endpoints to exhange out-of-band information between each other. An example of such
information might be list of messages that can be handled by a given endpoint. Based on that information each endpoint can build a complete routing table for all messages
without requiring any configuration.

## Usage

Data backplane API has three basic operations.

```
Task<IDataBackplaneSubscription> GetAllAndSubscribeToChanges(string type, Func<Entry, Task> onChanged, Func<Entry, Task> onRemoved);

Task Publish(string type, string data);

Task Revoke(string type);
```

which allow for querying (and continuously monitor changes) for data, publishing new data and removal of publications.

## Implementations

### File system

```
busConfig.EnableDataBackplane<FileSystemBackplane>();
```

The file system implementation is designed for development environments only. It uses a local directory (defaults to a temp folder) as a container for backplane publications. 

### Consul

```
busConfig.EnableDataBackplane<ConsulBackplane>("http://127.0.0.1:8500");
```

The Consul based implementation is suitable for enviroments that are already running a highly available Consul cluster to do service discovery and management. It registers
	endpoint instanes and all the messages types they handle in the Consul service registry so other endpoints can route messages based on this information.

## Sample

In order to see the backplane-based routing in action, please run both `Sender` and `Publisher` projects.

## Other usage scenarios

The data backplane can be used for auto-configuring other features, e.g. SQL Server multi-database feature.
