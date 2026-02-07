// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public class Ticker(string symbol, TickerFetcher fetcher, Market market)
{
    private readonly Dictionary<TickerRange ,TickerData> datas = [];
    private int numberOfDecimals;

    public string Symbol { get; } = symbol;
    public string UserGivenAlias { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string ExchangeName { get; private set; } = "";
    public MarketStatus MarketStatus => market.Status;

    /// Text that is used as a symbol on UI. If user has set display name
    /// for the ticker we use it, otherwise we show original symbol.
    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(UserGivenAlias))
                return Symbol;
            else
                return UserGivenAlias;
        }
    }

    /// This is set to true if latest data fetch failed. This means current data might be stale.
    public bool DataFetchFailed { get; private set; } = false;

    // Available ranges for this ticker.
    public TickerRange[] AvailableRanges { get; private set; } = [];

    // Last update of the data.
    public DateTime LastUpdated { get; private set; } = DateTime.MinValue;

    // This event is fired after data refresh so that UI knows to update it self.
    public event Action<Ticker>? OnUpdated;

    public bool TryGetData(TickerRange range, out TickerData data) => datas.TryGetValue(range, out data);

    // User can replace override symbol name in UI with an alias.
    public void SetAlias(string value)
    {
        UserGivenAlias = value.Trim();
        OnUpdated?.Invoke(this);
    }

    public async Task Refresh(TickerRange range, bool forceNetworkFetch = false)
    {
        try
        {
            if(forceNetworkFetch || ShouldFetch(range)) 
            {
                var result = await fetcher.Fetch(Symbol, range);
                UpdateModelStateFrom(result, range);
                DataFetchFailed = false;
            }

            OnUpdated?.Invoke(this);
        }
        catch (TickerFetchFailedException)
        {
            DataFetchFailed = true;
            OnUpdated?.Invoke(this);
        }
    }

    // Fetch data only if market is open or requested data is not available yet in the app.
    private bool ShouldFetch(TickerRange range)
    {
        if (market.Status == MarketStatus.Open)
            return true;

        if (!datas.ContainsKey(range))
            return true;

        return false;
    }

    // This is called by TickerDetails view when ever user has interaction with a chart.
    public (Amount?, IPercentageChange) GetAmountAndChangeFor(TickerRange range, DataPoint? dp1, DataPoint? dp2)
    {
        // If user mouse cursor is on top of graph before requested range is loaded, this function will crash
        // because we need the data to provide corrent values. Quick fix is now to return dummy values, which
        // seems to work just fine since the API tends to be very fast. This function could be however changed
        // to return optional result.
        if (!datas.Keys.Contains(range))
            return (null, new ChangeFromPreviousClose(1,1));

        var data = datas[range];

        if (dp1 is DataPoint a && dp2 is DataPoint b) // Range
        {
            if (dp1.Timestamp <= dp2.Timestamp)
            {
                return (null, new ChangeBetweenTwoPrices(dp1.Close, dp2.Close));
            }
            else
            {
                return (null, new ChangeBetweenTwoPrices(dp2.Close, dp1.Close));
            }
        } 
        else if (dp1 is DataPoint dp) // Up-to data point
        {
            IPercentageChange percentage = data.Range == TickerRange.Day || data.Range == TickerRange.FiveDays ? 
                new ChangeFromPreviousClose(dp.Close, data.PreviousClose) :
                new ChangeBetweenTwoPrices(data.DataPoints.First().Close, dp.Close);

            var value = new Amount(dp.Close, data.MarketPrice.Currency.Code, numberOfDecimals);

            return (value, percentage);
        }
        else // No range or hover (show data for full range)
        {
            return (data.MarketPrice, data.PercentageChange);
        }
    }

    private void UpdateModelStateFrom(Result result, TickerRange range)
    {
        this.numberOfDecimals = result.Meta.PriceHint ?? 2;

        Name = result.Meta.LongName?.Trim() ?? result.Meta.ShortName?.Trim() ?? "";
        ExchangeName = result.Meta.FullExchangeName;
        LastUpdated = DateTime.Now;
        AvailableRanges = result.Meta.ValidRanges?.Select(ParseRange).ToArray() ?? [];

        market.Update(result);

        datas[range] = new TickerDataParser().Parse(result, range);
    }

    private TickerRange ParseRange(string s) => s switch
    {
        "1d" => TickerRange.Day,
        "5d" => TickerRange.FiveDays,
        "1mo" => TickerRange.Month,
        "3mo" => TickerRange.ThreeMonths,
        "6mo" => TickerRange.SixMonths,
        "1y" => TickerRange.Year,
        "2y" => TickerRange.TwoYears,
        "5y" => TickerRange.FiveYears,
        "10y" => TickerRange.TenYears,
        "ytd" => TickerRange.Ytd,
        "max" => TickerRange.All,
        _ => TickerRange.Day
    };
}
