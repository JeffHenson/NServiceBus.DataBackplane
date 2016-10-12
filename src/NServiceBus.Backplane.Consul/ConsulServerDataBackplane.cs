using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Consul;

namespace NServiceBus.Backplane.Consul
{
    internal class ConsulDataBackplane : IDataBackplane
    {
        private const string ServiceName = "NServiceBus.Dataplane";

        private readonly string owner;
        private readonly string connectionString;

        public ConsulDataBackplane(string owner, string connectionString)
        {
            this.owner = owner;
            this.connectionString = connectionString;
        }

        public async Task Publish(string type, string data)
        {
            var client = GetConsulClient();

            var id = $"{owner}:{type}";
            try
            {
                await CheckIn(client, id).ConfigureAwait(false);
            }
            catch
            {
                await RegisterService(data, id, client).ConfigureAwait(false);
            }
        }

        private static async Task CheckIn(ConsulClient client, string id)
        {
            await client.Agent.PassTTL($"service:{id}", null).ConfigureAwait(false);
        }

        private static async Task RegisterService(string data, string id, ConsulClient client)
        {
            var registration = new AgentServiceRegistration
                               {
                                   ID = id,
                                   Name = ServiceName,
                                   Tags = new[] {data},
                                   Check = new AgentServiceCheck
                                           {
                                               TTL = TimeSpan.FromSeconds(10),
                                               DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(60),
                                               Status = CheckStatus.Passing
                                           }
                               };

            await client.Agent.ServiceRegister(registration).ConfigureAwait(false);
        }

        public async Task Revoke(string type)
        {
            var client = GetConsulClient();
            await client.Agent.ServiceDeregister($"{owner}:{type}").ConfigureAwait(false);
        }

        private ConsulClient GetConsulClient()
        {
            if (string.IsNullOrEmpty(connectionString))
            {
                return new ConsulClient();
            }
            var uri = new Uri(connectionString);
            return new ConsulClient(c => c.Address = uri);
        }

        public async Task<IReadOnlyCollection<Entry>> Query()
        {
            var client = GetConsulClient();

            var services = await client.Health.Service(ServiceName).ConfigureAwait(false);
            var entries = from service in services.Response
                          where service.Checks.All(c => c.Status == "passing")
                          let serviceId = service.Service.ID.Split(':')
                          let entryOwner = serviceId[0]
                          let type = serviceId[1]
                          let data = service.Service.Tags[0]
                          where entryOwner != owner
                          select new Entry(entryOwner, type, data);

            return entries.ToList();
        }
    }
}