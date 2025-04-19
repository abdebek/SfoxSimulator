namespace SfoxFeedLibrary.Models;

public class MarketDataMessage(long sequence, string recipient, long timestamp, object payload)
{
    public long sequence { get; set; } = sequence;
    public string recipient { get; set; } = recipient;
    public long timestamp { get; set; } = timestamp;
    public object payload { get; set; } = payload;
}