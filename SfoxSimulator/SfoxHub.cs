using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Microsoft.AspNetCore.SignalR;
using Microsoft.CSharp.RuntimeBinder;

namespace SfoxSimulator;

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

    private async Task SendDataToClient(MarketDataMessage data)
    {
        try
        {
            _logger.LogInformation("[SendDataToClient] Preparing to send data - Sequence: {Sequence}, Recipient: {Recipient}", 
                data.sequence, data.recipient);
        
            // Validate recipient group exists
            if (string.IsNullOrEmpty(data.recipient))
            {
                _logger.LogWarning("Empty recipient in message {Sequence}", data.sequence);
                return;
            }

            _logger.LogDebug("Full message: {@Message}", data);
        
            await Clients.Group(data.recipient).SendAsync("ReceiveMarketData", data);
            _logger.LogInformation("Successfully sent message {Sequence} to group {Recipient}", 
                data.sequence, data.recipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message {Sequence} to {Recipient}", 
                data.sequence, data.recipient);
        }
    }

    private async Task SendDataToClient(object data)
    {
        if (data is MarketDataMessage msg)
        {
            // If the client subscribes to "ticker.sfox.btcusd", check:
            Console.WriteLine(data); // Should be "ticker.sfox.btcusd"

            await Clients.Group(msg.recipient).SendAsync("ReceiveMarketData", msg);
            return;
        }
    
        // Fallback to handle any other message types
        try
        {
            dynamic dynData = data;
            string feedKey = dynData.recipient;
            await Clients.Group(feedKey).SendAsync("ReceiveMarketData", data);
        }
        catch (RuntimeBinderException ex)
        {
            _logger.LogError(ex, "Failed to extract recipient from dynamic data");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send data to clients");
        }
    }

    private static string GetCurrentTimestamp() => DateTime.UtcNow.ToString("o");
}


public class FeedSubject : IDisposable
{
    private readonly Subject<MarketDataMessage> _subject = new();
    private readonly IDisposable _timer;
    private readonly ILogger _logger;
    private bool _disposed;
    public string FeedKey { get; }
    public MarketState MarketState { get; } = new();
    private readonly Random _random = new();

    public FeedSubject(string feedKey, ILogger logger)
    {
        FeedKey = feedKey;
        _logger = logger;

        _timer = new Timer(GenerateData, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(5000));
    }

    private void GenerateData(object state)
    {
        if (_disposed) return;

        try
        {
            var parts = FeedKey.Split('.');
            var feedType = parts[0];
            var currencyPair = parts[2];

            MarketState.UpdateState(feedType, _random);
            var data = CreateMarketData(feedType, currencyPair, MarketState);

            lock (_subject)
            {
                if (_subject.HasObservers)
                {
                    _subject.OnNext(data);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating data for {FeedKey}", FeedKey);
        }
    }

    private MarketDataMessage CreateMarketData(string feedType, string currencyPair, MarketState state)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long sequence = new Random().Next(1000, 100000); // use a more reliable sequence generator?
    
        object payload = feedType switch
        {
            "orderbook" => new { FeedKey, FeedType = feedType, CurrencyPair = currencyPair, state.Asks, state.Bids },
            "ticker" => new { FeedKey,  FeedType = feedType,  CurrencyPair = currencyPair,  Ticker = new  { Last = state.LastPrice, High = state.LastPrice * 1.05m, Low = state.LastPrice * 0.95m, Volume = state.Volume24h, Timestamp = DateTime.UtcNow }},
            "trades" => new { FeedKey, FeedType = feedType, CurrencyPair = currencyPair, Trade = new { Price = state.LastPrice, Quantity = state.RandomTradeQuantity(), Timestamp = DateTime.UtcNow } },
            _ => new { FeedKey, FeedType = feedType, CurrencyPair = currencyPair, Message = "Unsupported feed type" }
        };
    
        return new MarketDataMessage(sequence, FeedKey, timestamp, payload);
    }

    public IDisposable Subscribe(Action<MarketDataMessage> onNext)
    {
        return _subject.Subscribe(onNext);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _timer.Dispose();
            _subject.OnCompleted();
            _subject.Dispose();
        }
    }
}

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

public class MarketState
{
    public List<decimal[]> Asks { get; set; } = new();
    public List<decimal[]> Bids { get; set; } = new();
    public decimal LastPrice { get; set; } = 0;
    public object Volume24h { get; internal set; }

    public void UpdateState(string feedType, Random random)
    {
        if (feedType == "orderbook")
        {
            Asks = Enumerable.Range(1, 5).Select(i => new[] { 100m + i, random.Next(1, 10) }).ToList();
            Bids = Enumerable.Range(1, 5).Select(i => new[] { 100m - i, random.Next(1, 10) }).ToList();
        }
        else if (feedType == "trades" || feedType == "ticker")
        {
            LastPrice = 100 + (decimal)(random.NextDouble() * 10 - 5);
            Volume24h = Math.Round((decimal)(random.NextDouble() * 1000), 2);
        }
    }

    public decimal RandomTradeQuantity() => new Random().Next(1, 5);
}

public class MarketDataMessage
{
    public long sequence { get; set; }
    public string recipient { get; set; } 
    public long timestamp { get; set; } 
    public object payload { get; set; } 

    public MarketDataMessage(long sequence, string recipient, long timestamp, object payload)
    {
        this.sequence = sequence;
        this.recipient = recipient;
        this.timestamp = timestamp;
        this.payload = payload;
    }
}
