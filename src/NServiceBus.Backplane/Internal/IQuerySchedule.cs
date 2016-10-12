using System;
using System.Threading.Tasks;

namespace NServiceBus.Backplane.Internal
{
    internal interface IQuerySchedule
    {
        IDisposable Schedule(Func<Task> recurringAction);
    }
}