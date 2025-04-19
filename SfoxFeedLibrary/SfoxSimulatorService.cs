using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SfoxFeedLibrary.Models;

namespace SfoxFeedLibrary;

public interface ISfoxSimulatorService : IHostedService, IDisposable
{
    FeedSubject GetOrCreateFeedSubject(string feedKey);
    void RemoveFeedSubject(string feedKey);
}

public class SfoxSimulatorService : ISfoxSimulatorService
{
    private readonly IHubContext<SfoxSimulatorHub> _hubContext;
    private readonly ILogger<SfoxSimulatorService> _logger;
    private readonly ConcurrentDictionary<string, FeedSubject> _feedSubjects = new();

    public SfoxSimulatorService(IHubContext<SfoxSimulatorHub> hubContext, ILogger<SfoxSimulatorService> logger)
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