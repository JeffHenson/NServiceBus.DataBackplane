using NServiceBus;

namespace Messages
{
    public class SomeCommand : ICommand
    {
        public int Number { get; set; }
    }
}