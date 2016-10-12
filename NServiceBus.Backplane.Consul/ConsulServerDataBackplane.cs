using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Consul;

namespace NServiceBus.Backplane.Consul
{
    internal class ConsulDataBackplane : IDataBackplane
    {
        private readonly string owner;
        private readonly string connectionString;
        private readonly string ServiceName = "NServiceBus.Dataplane";

        public ConsulDataBackplane(string owner, string connectionString)
        {
            this.owner = owner;
            this.connectionString = connectionString;
        }

        public Task Publish(string type, string data)
        {
            var client = GetConsulClient();

            var registration = new AgentServiceRegistration
                               {
                                   ID = $"{owner}:{type}",
                                   Name = ServiceName,
                                   Tags = new[] {data}
                               };

            client.Agent.ServiceRegister(registration);
            return Task.FromResult(0);
        }

        public Task Revoke(string type)
        {
            var client = GetConsulClient();
            client.Agent.ServiceDeregister($"{owner}:{type}");
            return Task.FromResult(0);
        }

        private Client GetConsulClient()
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                var uri = new Uri(connectionString);
                return new Client(new ConsulClientConfiguration {Address = uri.Authority, Scheme = uri.Scheme});
            }
            return new Client();
        }

        public Task<IReadOnlyCollection<Entry>> Query()
        {
            var client = GetConsulClient();

            var services = client.Catalog.Service(ServiceName).Response;

            var entries = from service in services
                          let entryOwner = service.ServiceID.Split(':')[0]
                          let type = service.ServiceID.Split(':')[1]
                          let data = service.ServiceTags[0]
                          where entryOwner != owner
                          select new Entry(entryOwner, type, data);

            return Task.FromResult((IReadOnlyCollection<Entry>) entries.ToList());
        }
    }
}