namespace SfoxFeedLibrary.Models;

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
