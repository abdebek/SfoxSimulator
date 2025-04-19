using Microsoft.Extensions.Logging;
using System.Reactive.Subjects;

namespace SfoxFeedLibrary.Models;

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
