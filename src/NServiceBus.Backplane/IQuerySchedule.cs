using System;
using System.Threading.Tasks;

namespace NServiceBus.Backplane
{
    internal interface IQuerySchedule
    {
        IDisposable Schedule(Func<Task> recurringAction);
    }
}