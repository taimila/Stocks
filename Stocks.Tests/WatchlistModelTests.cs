using System.Text.Json;
using Stocks.Model;

namespace Stocks.Tests;

[NonParallelizable]
public sealed class WatchlistModelTests
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
    public void RenameWatchlistRenamesWatchlistByIdFromSummary()
    {
        var sut = CreateModel(
            activeListId: "main",
            new Watchlist { Id = "main", Name = "Main", Symbols = [] },
            new Watchlist { Id = "tech", Name = "Tech", Symbols = [] });
        var watchlist = sut.GetWatchlists().Single(item => item.Name == "Tech");

        sut.RenameWatchlist(watchlist.Id, "Growth");

        Assert.That(
            sut.GetWatchlists().Single(item => item.Id == watchlist.Id).Name,
            Is.EqualTo("Growth"));
    }

    [Test]
    public void SetActiveWatchlistSetsActiveByIdFromSummary()
    {
        var sut = CreateModel(
            activeListId: "main",
            new Watchlist { Id = "main", Name = "Main", Symbols = [] },
            new Watchlist { Id = "tech", Name = "Tech", Symbols = [] });
        var watchlist = sut.GetWatchlists().Single(item => item.Name == "Tech");

        sut.SetActiveWatchlist(watchlist.Id);

        Assert.That(sut.ActiveWatchlistId, Is.EqualTo(watchlist.Id));
        Assert.That(sut.ActiveWatchlistName, Is.EqualTo("Tech"));
    }

    [Test]
    public void DeleteWatchlistRemovesWatchlistByIdFromSummary()
    {
        var sut = CreateModel(
            activeListId: "main",
            new Watchlist { Id = "main", Name = "Main", Symbols = [] },
            new Watchlist { Id = "tech", Name = "Tech", Symbols = [] });
        var watchlist = sut.GetWatchlists().Single(item => item.Name == "Tech");
        var onChangedCalls = 0;
        sut.OnChanged += () => onChangedCalls++;

        sut.DeleteWatchlist(watchlist.Id);

        Assert.That(sut.GetWatchlists().Any(item => item.Id == watchlist.Id), Is.False);
        Assert.That(sut.GetWatchlists().Count, Is.EqualTo(1));
        Assert.That(onChangedCalls, Is.EqualTo(1));
    }

    [Test]
    public void RemoveSymbolFromWatchlistRemovesActiveSymbolAndRaisesEvent()
    {
        var sut = CreateModel(
            activeListId: "main",
            new Watchlist { Id = "main", Name = "Main", Symbols = ["AAPL", "MSFT"] });
        string? eventWatchlistId = null;
        string? eventSymbol = null;
        sut.OnSymbolRemoved += (watchlistId, symbol) =>
        {
            eventWatchlistId = watchlistId;
            eventSymbol = symbol;
        };

        var symbol = sut.GetActiveSymbols()[0];
        sut.RemoveSymbolFromWatchlist(symbol, sut.ActiveWatchlistId);

        Assert.That(sut.GetActiveSymbols(), Is.EqualTo(["MSFT"]));
        Assert.That(eventWatchlistId, Is.EqualTo(sut.ActiveWatchlistId));
        Assert.That(eventSymbol, Is.EqualTo("AAPL"));
    }

    [Test]
    public void MoveSymbolInActiveWatchlistReordersSymbolsAndRaisesEvent()
    {
        var sut = CreateModel(
            activeListId: "main",
            new Watchlist { Id = "main", Name = "Main", Symbols = ["AAPL", "MSFT", "GOOG"] });
        string? eventWatchlistId = null;
        string? eventSymbol = null;
        var eventIndex = -1;
        sut.OnSymbolMoved += (watchlistId, symbol, index) =>
        {
            eventWatchlistId = watchlistId;
            eventSymbol = symbol;
            eventIndex = index;
        };

        var symbol = sut.GetActiveSymbols()[0];
        sut.MoveSymbolInActiveWatchlist(symbol, 2);

        Assert.That(sut.GetActiveSymbols(), Is.EqualTo(["MSFT", "GOOG", "AAPL"]));
        Assert.That(eventWatchlistId, Is.EqualTo(sut.ActiveWatchlistId));
        Assert.That(eventSymbol, Is.EqualTo("AAPL"));
        Assert.That(eventIndex, Is.EqualTo(2));
    }

    private WatchlistModel CreateModel(string activeListId, params Watchlist[] watchlists)
    {
        SeedWatchlists(new WatchlistState
        {
            ActiveListId = activeListId,
            Lists = [.. watchlists]
        });

        return new WatchlistModel(new WatchlistStorage(new WatchlistMigrator(null!)));
    }

    private void SeedWatchlists(WatchlistState state)
    {
        var appDataDir = Path.Combine(tempDir, Constants.APP_ID);
        Directory.CreateDirectory(appDataDir);
        File.WriteAllText(
            Path.Combine(appDataDir, "watchlists.json"),
            JsonSerializer.Serialize(state));
    }
}
