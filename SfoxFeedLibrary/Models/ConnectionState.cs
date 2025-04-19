using System.Collections.Concurrent;

namespace SfoxFeedLibrary.Models;

public class ConnectionState : IDisposable
{
    public ConcurrentDictionary<string, IDisposable> Subscriptions { get; } = new();

    public void Dispose()
    {
        foreach (var subscription in Subscriptions.Values)
        {
            subscription.Dispose();
        }

        Subscriptions.Clear();
    }
} 
