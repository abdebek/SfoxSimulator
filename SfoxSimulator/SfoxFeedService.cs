using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace SfoxSimulator;

public interface ISfoxFeedService : IHostedService, IDisposable
{
    FeedSubject GetOrCreateFeedSubject(string feedKey);
    void RemoveFeedSubject(string feedKey);
}

public class SfoxFeedService : ISfoxFeedService
{
    private readonly IHubContext<SfoxHub> _hubContext;
    private readonly ILogger<SfoxFeedService> _logger;
    private readonly ConcurrentDictionary<string, FeedSubject> _feedSubjects = new();

    public SfoxFeedService(IHubContext<SfoxHub> hubContext, ILogger<SfoxFeedService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public FeedSubject GetOrCreateFeedSubject(string feedKey)
    {
        return _feedSubjects.GetOrAdd(feedKey, key =>
        {
            var subject = new FeedSubject(key, _logger);
            subject.Subscribe(data =>
            {
                _ = SendDataToClients(data);
            });
            return subject;
        });
    }

    public void RemoveFeedSubject(string feedKey)
    {
        if (_feedSubjects.TryRemove(feedKey, out var subject))
        {
            subject.Dispose();
        }
    }

    private async Task SendDataToClients(MarketDataMessage data)
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

            await _hubContext.Clients.Group(data.recipient).SendAsync("ReceiveMarketData", data);
            _logger.LogInformation("Successfully sent message {Sequence} to group {Recipient}",
                data.sequence, data.recipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message {Sequence} to {Recipient}",
                data.sequence, data.recipient);
        }
    }

    // IHostedService implementation
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Dispose of all feed subjects when the service stops
        foreach (var subject in _feedSubjects.Values)
        {
            subject.Dispose();
        }
        _feedSubjects.Clear();
    }

    public void Dispose()
    {
        foreach (var subject in _feedSubjects.Values)
        {
            subject.Dispose();
        }
    }
}