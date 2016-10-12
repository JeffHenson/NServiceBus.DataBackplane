using NServiceBus;

namespace Messages
{
    public class SomeEvent : IEvent
    {
        public int Number { get; set; }
    }
}