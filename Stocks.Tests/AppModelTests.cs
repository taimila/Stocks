using System.Text.Json;
using Stocks.Model;

namespace Stocks.Tests;

[NonParallelizable]
public sealed class AppModelTests
{
    private string tempDir = "";
    private string? originalXdgDataHome;

    [SetUp]
    public void SetUp()
    {
        originalXdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        tempDir = Path.Combine(Path.GetTempPath(), $"stocks-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", originalXdgDataHome);

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }

    [Test]
    public void SetActiveUpdatesSelectedTickerFromVisibleTickers()
    {
        var sut = CreateModel("AAPL", "MSFT");
        var target = sut.Tickers.Single(ticker => ticker.Symbol == "MSFT");

        sut.SetActive(target);

        Assert.That(sut.SelectedTicker, Is.SameAs(target));
    }

    [Test]
    public async Task RemoveTickerRemovesVisibleTickerAndPrunesCache()
    {
        var sut = CreateModel("AAPL", "MSFT");
        Ticker? removedTicker = null;
        sut.OnTickerRemoved += ticker => removedTicker = ticker;

        await sut.RemoveTicker("MSFT");

        Assert.That(sut.Tickers.Select(ticker => ticker.Symbol), Is.EqualTo(["AAPL"]));
        Assert.That(removedTicker?.Symbol, Is.EqualTo("MSFT"));
        Assert.That(sut.GetTicker("MSFT"), Is.Null);
    }

    [Test]
    public void MoveTickerReordersVisibleTickersAndKeepsCacheAligned()
    {
        var sut = CreateModel("AAPL", "MSFT", "GOOG");
        var movedTicker = sut.Tickers[0];
        Ticker? eventTicker = null;
        var eventIndex = -1;
        sut.OnTickerMoved += (ticker, index) =>
        {
            eventTicker = ticker;
            eventIndex = index;
        };

        sut.MoveTicker(movedTicker, 2);

        Assert.That(sut.Tickers.Select(ticker => ticker.Symbol), Is.EqualTo(["MSFT", "GOOG", "AAPL"]));
        Assert.That(sut.GetTicker("AAPL"), Is.SameAs(sut.Tickers[2]));
        Assert.That(eventTicker, Is.SameAs(movedTicker));
        Assert.That(eventIndex, Is.EqualTo(2));
    }

    [Test]
    public void SetTickerAliasPersistsAliasAndUpdatesCachedTicker()
    {
        var sut = CreateModel("AAPL");
        var ticker = sut.Tickers.Single();

        sut.SetTickerAlias(ticker, "Apple");

        var cachedTicker = sut.GetTicker("AAPL");
        var reloadedAliasStorage = new AliasStorage();

        Assert.That(cachedTicker, Is.SameAs(ticker));
        Assert.That(cachedTicker?.UserGivenAlias, Is.EqualTo("Apple"));
        Assert.That(reloadedAliasStorage.GetAlias("AAPL"), Is.EqualTo("Apple"));
    }

    private AppModel CreateModel(params string[] symbols)
    {
        SeedWatchlists(new WatchlistState
        {
            ActiveListId = "main",
            Lists =
            [
                new Watchlist
                {
                    Id = "main",
                    Name = "Main",
                    Symbols = [.. symbols]
                }
            ]
        });

        var fetcher = new TestTickerFetcher();
        var factory = new TickerFactory(fetcher);
        var settings = new TestAppSettings();
        var watchlists = new WatchlistModel(new WatchlistStorage(new WatchlistMigrator(null!)));
        var aliasStorage = new AliasStorage();

        return new AppModel(fetcher, factory, settings, watchlists, aliasStorage);
    }

    private void SeedWatchlists(WatchlistState state)
    {
        var appDataDir = Path.Combine(tempDir, Constants.APP_ID);
        Directory.CreateDirectory(appDataDir);
        File.WriteAllText(
            Path.Combine(appDataDir, "watchlists.json"),
            JsonSerializer.Serialize(state));
    }

    private sealed class TestAppSettings : AppSettings
    {
        public TestAppSettings() : base(null!)
        {
        }

        public override int UpdateIntervalInSeconds
        {
            get => 3600;
            set { }
        }

        public override string UserAgent
        {
            get => "Stocks.Tests";
            set { }
        }
    }

    private sealed class TestTickerFetcher : TickerFetcher
    {
        public TestTickerFetcher() : base(new TestAppSettings(), new HttpClient())
        {
        }

        public override Task<Result> Fetch(string symbol, TickerRange range)
        {
            return Task.FromResult(CreateResult(symbol));
        }

        private static Result CreateResult(string symbol)
        {
            var meta = new Meta(
                Currency: "USD",
                Symbol: symbol,
                ExchangeName: "NMS",
                FullExchangeName: "NASDAQ",
                InstrumentType: "EQUITY",
                FirstTradeDate: null,
                RegularMarketTime: null,
                Gmtoffset: 0,
                Timezone: "EST",
                ExchangeTimezoneName: "America/New_York",
                RegularMarketPrice: 100,
                PreviousClose: 95,
                ChartPreviousClose: 95,
                RegularMarketDayHigh: 101,
                RegularMarketDayLow: 99,
                RegularMarketVolume: 1000,
                LongName: $"{symbol} Inc",
                ShortName: symbol,
                PriceHint: 2,
                Scale: null,
                HasPrePostMarketData: null,
                CurrentTradingPeriod: null,
                TradingPeriods: null,
                DataGranularity: "1d",
                Range: "1d",
                ValidRanges: ["1d", "5d"]);

            var timestamps = new long[] { 1000, 2000 };
            var quote = new Quote(
                Open: [99, 100],
                High: [100, 101],
                Low: [98, 99],
                Close: [99.5, 100],
                Volume: [100, 200]);

            return new Result(meta, timestamps, new Indicators([quote], []));
        }
    }
}
