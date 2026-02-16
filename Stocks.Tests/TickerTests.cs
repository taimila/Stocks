using System.Runtime.CompilerServices;
using NSubstitute;
using NUnit.Framework;
using Stocks.Model;

namespace Stocks.Tests;

public class TickerTests
{
    private const string Symbol = "AAPL";
    
    private static Market CreateMarket(MarketStatus status = MarketStatus.Open)
    {
        var market = Substitute.For<Market>();
        market.Status.Returns(status);
        return market;
    }

    private static TestTickerFetcher CreateFetcher(Result? result = null, Exception? exception = null)
    {
        if (exception is not null)
        {
            return TestTickerFetcher.Create((_, _) => Task.FromException<Result>(exception));
        }

        return TestTickerFetcher.Create((_, _) => Task.FromResult(result ?? CreateResult()));
    }

    [Test]
    public void SymbolReturnsConstructorValue()
    {
        var fetcher = CreateFetcher();
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        var symbol = sut.Symbol;

        Assert.That(symbol, Is.EqualTo(Symbol));
    }

    [Test]
    public void NameDefaultsToEmpty()
    {
        var fetcher = CreateFetcher();
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        var name = sut.Name;

        Assert.That(name, Is.Empty);
    }

    [Test]
    public void ExchangeNameDefaultsToEmpty()
    {
        var fetcher = CreateFetcher();
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        var exchangeName = sut.ExchangeName;

        Assert.That(exchangeName, Is.Empty);
    }

    [Test]
    public void DataFetchFailedDefaultsToFalse()
    {
        var fetcher = CreateFetcher();
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        var failed = sut.DataFetchFailed;

        Assert.That(failed, Is.False);
    }

    [Test]
    public void AvailableRangesDefaultsToEmpty()
    {
        var fetcher = CreateFetcher();
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        var ranges = sut.AvailableRanges;

        Assert.That(ranges, Is.Empty);
    }

    [Test]
    public async Task RefreshStoresDayDataRange()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        await sut.Refresh(TickerRange.Day);

        var hasData = sut.TryGetData(TickerRange.Day, out TickerData data);
        var range = data.Range; 

        Assert.That(hasData, Is.True);
        Assert.That(range, Is.EqualTo(TickerRange.Day));
    }

    [Test]
    public async Task RefreshStoresRequestedDataRange()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        await sut.Refresh(TickerRange.Month);

        sut.TryGetData(TickerRange.Month, out TickerData data);
        var range = data.Range; 

        Assert.That(range, Is.EqualTo(TickerRange.Month));
    }

    [Test]
    public void LastUpdatedDefaultsToMinValue()
    {
        var fetcher = CreateFetcher();
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        var lastUpdated = sut.LastUpdated;

        Assert.That(lastUpdated, Is.EqualTo(DateTime.MinValue));
    }

    [Test]
    public async Task RefreshCallsFetcherWithDayRange()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        await sut.Refresh(TickerRange.Day);

        Assert.That(fetcher.FetchCalls.Count, Is.EqualTo(1));
        Assert.That(fetcher.FetchCalls[0], Is.EqualTo((Symbol, TickerRange.Day)));
    }

    [Test]
    public async Task RefreshSetsNameFromMeta()
    {
        var fetcher = CreateFetcher(CreateResult(longName: "Acme Corp"));
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        await sut.Refresh(TickerRange.Day);

        Assert.That(sut.Name, Is.EqualTo("Acme Corp"));
    }

    [Test]
    public async Task RefreshSetsExchangeNameFromMeta()
    {
        var fetcher = CreateFetcher(CreateResult(fullExchangeName: "NASDAQ"));
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        await sut.Refresh(TickerRange.Day);

        Assert.That(sut.ExchangeName, Is.EqualTo("NASDAQ"));
    }

    [Test]
    public async Task RefreshSetsAvailableRangesFromMeta()
    {
        var fetcher = CreateFetcher(CreateResult(validRanges: new[] { "1d", "5d", "1mo" }));
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        await sut.Refresh(TickerRange.Day);

        Assert.That(sut.AvailableRanges.Length, Is.EqualTo(3));
    }

    [Test]
    public async Task RefreshSetsDayDataRangeToDay()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        await sut.Refresh(TickerRange.Day);

        var hasData = sut.TryGetData(TickerRange.Day, out TickerData data);
        var range = data.Range; 

        Assert.That(hasData, Is.True);
        Assert.That(range, Is.EqualTo(TickerRange.Day));
    }

    [Test]
    public async Task RefreshUpdatesLastUpdated()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        await sut.Refresh(TickerRange.Day);

        Assert.That(sut.LastUpdated, Is.GreaterThan(DateTime.MinValue));
    }

    [Test]
    public async Task RefreshRaisesOnUpdatedEventOnSuccess()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());
        var wasRaised = false;
        sut.OnUpdated += _ => wasRaised = true;

        await sut.Refresh(TickerRange.Day);

        Assert.That(wasRaised, Is.True);
    }

    [Test]
    public async Task RefreshSetsDataFetchFailedOnException()
    {
        var fetcher = CreateFetcher(exception: new TickerFetchFailedException("nope", new Exception("boom")));
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        await sut.Refresh(TickerRange.Day);

        Assert.That(sut.DataFetchFailed, Is.True);
    }

    [Test]
    public async Task RefreshRaisesOnUpdatedEventOnFailure()
    {
        var fetcher = CreateFetcher(exception: new TickerFetchFailedException("nope", new Exception("boom")));
        var sut = new Ticker(Symbol, fetcher, CreateMarket());
        var wasRaised = false;
        sut.OnUpdated += _ => wasRaised = true;

        await sut.Refresh(TickerRange.Day);

        Assert.That(wasRaised, Is.True);
    }

    [Test]
    public async Task RefreshCallsFetcherWithRequestedRange()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        await sut.Refresh(TickerRange.Month);

        Assert.That(fetcher.FetchCalls.Count, Is.EqualTo(1));
        Assert.That(fetcher.FetchCalls[0], Is.EqualTo((Symbol, TickerRange.Month)));
    }

    [Test]
    public async Task RefreshSetsDataRangeToRequestedRange()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());

        await sut.Refresh(TickerRange.Month);

        sut.TryGetData(TickerRange.Month, out TickerData data);
        var range = data.Range; 

        Assert.That(range, Is.EqualTo(TickerRange.Month));
    }

    [Test]
    public async Task GetAmountAndChangeForWithRangeReturnsNullAmount()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());
        await sut.Refresh(TickerRange.Day);
        var dp1 = new DataPoint(new DateTime(2024, 1, 2), 1, 1, 1, 10, 1);
        var dp2 = new DataPoint(new DateTime(2024, 1, 3), 1, 1, 1, 15, 1);

        var (amount, _) = sut.GetAmountAndChangeFor(TickerRange.Day, dp1, dp2);

        Assert.That(amount, Is.Null);
    }

    [Test]
    public async Task GetAmountAndChangeForWithReverseRangeUsesLaterCloseAsEnd()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());
        await sut.Refresh(TickerRange.Day);
        var dp1 = new DataPoint(new DateTime(2024, 1, 3), 1, 1, 1, 20, 1);
        var dp2 = new DataPoint(new DateTime(2024, 1, 2), 1, 1, 1, 10, 1);

        var (_, change) = sut.GetAmountAndChangeFor(TickerRange.Day, dp1, dp2);

        Assert.That(change.Percentage, Is.EqualTo(100d));
    }

    [Test]
    public async Task GetAmountAndChangeForWithSingleDayPointReturnsAmountForClose()
    {
        var fetcher = CreateFetcher(CreateResult(priceHint: 3));
        var sut = new Ticker(Symbol, fetcher, CreateMarket());
        await sut.Refresh(TickerRange.Day);
        var dp = new DataPoint(new DateTime(2024, 1, 2), 1, 1, 1, 12.345, 1);

        var (amount, _) = sut.GetAmountAndChangeFor(TickerRange.Day, dp, null);

        Assert.That(amount?.Price, Is.EqualTo(12.345));
    }

    [Test]
    public async Task GetAmountAndChangeForWithSingleDayPointReturnsShortTermChange()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());
        await sut.Refresh(TickerRange.Day);
        var dp = new DataPoint(new DateTime(2024, 1, 2), 1, 1, 1, 12, 1);

        var (_, change) = sut.GetAmountAndChangeFor(TickerRange.Day, dp, null);

        Assert.That(change, Is.TypeOf<ChangeFromPreviousClose>());
    }

    [Test]
    public async Task GetAmountAndChangeForWithSingleLongRangePointReturnsLongTermChange()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());
        await sut.Refresh(TickerRange.Month);
        var dp = new DataPoint(new DateTime(2024, 1, 2), 1, 1, 1, 12, 1);

        var (_, change) = sut.GetAmountAndChangeFor(TickerRange.Month, dp, null);

        Assert.That(change, Is.TypeOf<ChangeBetweenTwoPrices>());
    }

    [Test]
    public async Task GetAmountAndChangeForWithoutPointsReturnsDataPercentageChange()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());
        await sut.Refresh(TickerRange.Month);

        sut.TryGetData(TickerRange.Month, out TickerData data);
        var (_, change) = sut.GetAmountAndChangeFor(TickerRange.Month, null, null);

        Assert.That(ReferenceEquals(change, data.PercentageChange), Is.True);
    }

    [Test]
    public async Task GetAmountAndChangeForWithoutPointsReturnsDataMarketPrice()
    {
        var fetcher = CreateFetcher(CreateResult());
        var sut = new Ticker(Symbol, fetcher, CreateMarket());
        await sut.Refresh(TickerRange.Month);

        sut.TryGetData(TickerRange.Month, out TickerData data);
        var (amount, _) = sut.GetAmountAndChangeFor(TickerRange.Month, null, null);

        Assert.That(amount?.Price, Is.EqualTo(data.MarketPrice.Price));
    }

    private static Result CreateResult(
        string? longName = "Acme Corp",
        string? shortName = null,
        string fullExchangeName = "NYSE",
        string currency = "USD",
        double regularMarketPrice = 100,
        double chartPreviousClose = 90,
        double regularMarketDayHigh = 110,
        double regularMarketDayLow = 80,
        int? priceHint = 2,
        string[]? validRanges = null)
    {
        var meta = new Meta(
            Currency: currency,
            Symbol: Symbol,
            ExchangeName: "XNYS",
            FullExchangeName: fullExchangeName,
            InstrumentType: "EQUITY",
            FirstTradeDate: null,
            RegularMarketTime: null,
            Gmtoffset: 0,
            Timezone: "UTC",
            ExchangeTimezoneName: "UTC",
            RegularMarketPrice: regularMarketPrice,
            PreviousClose: null,
            ChartPreviousClose: chartPreviousClose,
            RegularMarketDayHigh: regularMarketDayHigh,
            RegularMarketDayLow: regularMarketDayLow,
            RegularMarketVolume: null,
            LongName: longName,
            ShortName: shortName,
            PriceHint: priceHint,
            Scale: null,
            HasPrePostMarketData: null,
            CurrentTradingPeriod: null,
            TradingPeriods: null,
            DataGranularity: "1d",
            Range: "1d",
            ValidRanges: validRanges ?? ["1d", "5d"]);

        var timestamps = new long[] { 1000, 2000 };
        var quote = new Quote(
            Open: [9, 11],
            High: [12, 13],
            Low: [8, 10],
            Close: [10, 12],
            Volume: [100, 200]);
        var indicators = new Indicators([quote], []);

        return new Result(meta, timestamps, indicators);
    }

    private sealed class TestTickerFetcher : TickerFetcher
    {
        private Func<string, TickerRange, Task<Result>> onFetch = default!;
        public List<(string Symbol, TickerRange Range)> FetchCalls { get; private set; } = default!;

        private TestTickerFetcher() : base(null!, new HttpClient())
        {
        }

        public static TestTickerFetcher Create(Func<string, TickerRange, Task<Result>> onFetch)
        {
            var fetcher = (TestTickerFetcher)RuntimeHelpers.GetUninitializedObject(typeof(TestTickerFetcher));
            fetcher.onFetch = onFetch;
            fetcher.FetchCalls = [];
            return fetcher;
        }

        public override Task<Result> Fetch(string symbol, TickerRange range)
        {
            FetchCalls.Add((symbol, range));
            return onFetch(symbol, range);
        }
    }
}
