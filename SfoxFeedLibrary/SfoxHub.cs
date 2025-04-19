using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SfoxFeedLibrary.Models;

namespace SfoxFeedLibrary;

public class SfoxHub : Hub
{
    private static readonly ConcurrentDictionary<string, ConnectionState> _connectionStates = new();
    private static int _connectionCount = 0;

    private readonly ILogger<SfoxHub> _logger;
    private readonly ISfoxFeedService _feedService;

    public SfoxHub(ILogger<SfoxHub> logger, ISfoxFeedService feedService)
    {
        _logger = logger;
        _feedService = feedService;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        if (_connectionStates.TryRemove(connectionId, out var connectionState))
        {
            foreach (var sub in connectionState.Subscriptions)
            {
                // Just dispose of the subscription, the feed service will manage feed subjects
                sub.Value.Dispose();
                
                // Check if anyone is still using this feed
                var stillInUse = _connectionStates.Values.Any(c => c.Subscriptions.ContainsKey(sub.Key));
                if (!stillInUse)
                {
                    _feedService.RemoveFeedSubject(sub.Key);
                    _logger.LogInformation("Removed feed subject {FeedKey} after disconnect", sub.Key);
                }
            }

            connectionState.Dispose();
            Interlocked.Decrement(ref _connectionCount);
        }

        _logger.LogInformation("Connection {ConnectionId} disconnected. Active connections: {Count}",
            connectionId, _connectionCount);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task Subscribe(string feedKey)
    {
        try
        {
            var connectionId = Context.ConnectionId;
            await Groups.AddToGroupAsync(connectionId, feedKey);

            var connectionState = _connectionStates.GetOrAdd(connectionId, _ => new ConnectionState());
            var feedSubject = _feedService.GetOrCreateFeedSubject(feedKey);

            var subscription = feedSubject.Subscribe(data => {
                // No need to do anything here - the feed service handles sending the data
            });

            connectionState.Subscriptions[feedKey] = subscription;
            _logger.LogInformation("Connection {ConnectionId} subscribed to feed {FeedKey}", connectionId, feedKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Subscribe failed for connection {ConnectionId} and feed {FeedKey}", Context.ConnectionId, feedKey);
            await Clients.Caller.SendAsync("ReceiveError", new
            {
                Code = "SUBSCRIBE_FAILED",
                Message = ex.Message,
                Severity = "error",
                Timestamp = GetCurrentTimestamp()
            });
        }
    }

    public async Task Unsubscribe(string feedKey)
    {
        var connectionId = Context.ConnectionId;
        await Groups.RemoveFromGroupAsync(connectionId, feedKey);

        if (_connectionStates.TryGetValue(connectionId, out var connectionState))
        {
            if (connectionState.Subscriptions.TryRemove(feedKey, out var subscription))
            {
                subscription.Dispose();
            
                // Check if any other connections are still subscribed to this feed
                var stillInUse = _connectionStates.Values.Any(c => c.Subscriptions.ContainsKey(feedKey));
                if (!stillInUse)
                {
                    // Use the feed service to remove and dispose the feed subject
                    _feedService.RemoveFeedSubject(feedKey);
                    _logger.LogInformation("Removed feed subject {FeedKey} after unsubscribe", feedKey);
                }
            }
        }
    
        _logger.LogInformation("Connection {ConnectionId} unsubscribed from feed {FeedKey}", connectionId, feedKey);
    }

    private static string GetCurrentTimestamp() => DateTime.UtcNow.ToString("o");
}
