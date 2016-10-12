using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NServiceBus.Backplane.Internal
{
    internal class DataBackplaneClient : IDataBackplaneClient
    {
        private readonly Dictionary<CacheKey, Entry> _cache = new Dictionary<CacheKey, Entry>();
        private readonly IDataBackplane _dataBackplane;
        private readonly IQuerySchedule _schedule;
        private IDisposable _timer;
        private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new ConcurrentDictionary<Guid, Subscriber>();

        public DataBackplaneClient(IDataBackplane dataBackplane, IQuerySchedule schedule)
        {
            _dataBackplane = dataBackplane;
            _schedule = schedule;
        }

        public Task Start()
        {
            _timer = _schedule.Schedule(async () =>
                                        {
                                            var addedOrUpdated = new List<Entry>();
                                            var results = await _dataBackplane.Query().ConfigureAwait(false);
                                            var removed = _cache.Values.Where(x => !results.Any(r => (r.Type == x.Type) && (r.Owner == x.Owner))).ToArray();
                                            foreach (var entry in results)
                                            {
                                                Entry oldEntry;
                                                var key = new CacheKey(entry.Owner, entry.Type);
                                                if (!_cache.TryGetValue(key, out oldEntry) || (oldEntry.Data != entry.Data))
                                                {
                                                    _cache[key] = entry;
                                                    addedOrUpdated.Add(entry);
                                                }
                                            }
                                            foreach (var change in addedOrUpdated)
                                            {
                                                await NotifyChanged(change).ConfigureAwait(false);
                                            }
                                            foreach (var entry in removed)
                                            {
                                                _cache.Remove(new CacheKey(entry.Owner, entry.Type));
                                                await NotifyRemoved(entry).ConfigureAwait(false);
                                            }
                                        });
            return Task.FromResult(0);
        }

        private async Task NotifyChanged(Entry change)
        {
            foreach (var subscriber in _subscribers)
            {
                await subscriber.Value.NotifyChanged(change).ConfigureAwait(false);
            }
        }

        private async Task NotifyRemoved(Entry change)
        {
            foreach (var subscriber in _subscribers)
            {
                await subscriber.Value.NotifyRemoved(change).ConfigureAwait(false);
            }
        }

        public Task Stop()
        {
            _timer.Dispose();
            return Task.FromResult(0);
        }

        public Task Publish(string type, string data)
        {
            return _dataBackplane.Publish(type, data);
        }

        public Task Revoke(string type)
        {
            return _dataBackplane.Revoke(type);
        }

        public async Task<IDataBackplaneSubscription> GetAllAndSubscribeToChanges(string type, Func<Entry, Task> onChanged, Func<Entry, Task> onRemoved)
        {
            var subscriberId = Guid.NewGuid();
            var subscriber = new Subscriber(type, onChanged, onRemoved, () =>
                                                                        {
                                                                            Subscriber _;
                                                                            _subscribers.TryRemove(subscriberId, out _);
                                                                        });
            _subscribers.TryAdd(subscriberId, subscriber);
            var cacheCopy = _cache;
            foreach (var entry in cacheCopy)
            {
                await subscriber.NotifyChanged(entry.Value).ConfigureAwait(false);
            }
            return subscriber;
        }

        private class Subscriber : IDataBackplaneSubscription
        {
            private readonly Func<Entry, Task> _onAddedOrUpdated;
            private readonly Func<Entry, Task> _onRemoved;
            private readonly Action _unsubscribe;
            private readonly string _type;
            private bool _unsubscribed;

            public Subscriber(string type, Func<Entry, Task> onAddedOrUpdated, Func<Entry, Task> onRemoved, Action unsubscribe)
            {
                _onAddedOrUpdated = onAddedOrUpdated;
                _onRemoved = onRemoved;
                _unsubscribe = unsubscribe;
                _type = type;
            }

            public Task NotifyChanged(Entry changedEntry)
            {
                if (_type == changedEntry.Type)
                {
                    return _onAddedOrUpdated(changedEntry);
                }
                return Task.FromResult(0);
            }

            public Task NotifyRemoved(Entry removedEntry)
            {
                if (_type == removedEntry.Type)
                {
                    return _onRemoved(removedEntry);
                }
                return Task.FromResult(0);
            }

            public void Unsubscribe()
            {
                if (_unsubscribed)
                {
                    return;
                }
                _unsubscribe();
                _unsubscribed = true;
            }
        }

        private class CacheKey
        {
            private readonly string _owner;
            private readonly string _type;

            public CacheKey(string owner, string type)
            {
                _owner = owner;
                _type = type;
            }

            protected bool Equals(CacheKey other)
            {
                return string.Equals(_owner, other._owner) && string.Equals(_type, other._type);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((CacheKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_owner.GetHashCode() * 397) ^ _type.GetHashCode();
                }
            }

            public static bool operator ==(CacheKey left, CacheKey right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(CacheKey left, CacheKey right)
            {
                return !Equals(left, right);
            }
        }
    }
}