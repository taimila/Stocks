// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public class AppModel
{
    private readonly TickerFetcher fetcher;
    private readonly TickerFactory factory;
    private readonly AliasStorage aliasStorage;
    private readonly PeriodicTimer timer;
    private readonly CancellationTokenSource cts = new();
    private readonly Dictionary<string, Ticker> tickerCache = [];
    private string visibleWatchlistId;

    private TickerRange activeRange = TickerRange.Day;
    public TickerRange ActiveRange { 
        get
        {
            return activeRange;
        } 
        set
        {
            activeRange = value;
            OnActiveTickerRangeChanged?.Invoke(value);
        } 
    }

    public List<Ticker> Tickers { get; } = [];
    public bool HasTickers => Tickers.Count > 0;
    public Ticker? SelectedTicker { get; private set; } = null;
    public WatchlistModel Watchlists { get; }

    // Events that allow UI to update itself as application state changes
    public event Action<Ticker>? OnTickerAdded;
    public event Action<Ticker>? OnTickerRemoved;
    public event Action<Ticker, int>? OnTickerMoved;
    public event Action<Ticker?, Ticker?>? OnActiveTickerChanged;
    public event Action<TickerRange>? OnActiveTickerRangeChanged;
    public event Action? OnVisibleTickersReloaded;

    public AppModel(TickerFetcher fetcher, TickerFactory factory, AppSettings appSettings, WatchlistModel watchlists, AliasStorage aliasStorage)
    {
        this.timer = new(TimeSpan.FromSeconds(appSettings.UpdateIntervalInSeconds));
        this.fetcher = fetcher;
        this.factory = factory;
        Watchlists = watchlists;
        this.aliasStorage = aliasStorage;
        visibleWatchlistId = Watchlists.ActiveWatchlistId;

        foreach (var symbol in Watchlists.GetActiveSymbols())
            Tickers.Add(GetOrCreateTicker(symbol));

        SelectedTicker = Tickers.FirstOrDefault();

        Watchlists.OnChanged += HandleWatchlistGroupsChanged;
        Watchlists.OnActiveChanged += HandleActiveWatchlistChanged;
        Watchlists.OnSymbolAdded += HandleWatchlistSymbolAdded;
        Watchlists.OnSymbolRemoved += HandleWatchlistSymbolRemoved;
        Watchlists.OnSymbolMoved += HandleWatchlistSymbolMoved;

        Task.Run(async () => await StartAutoUpdate());
    }

    public async Task<List<SearchResult>> SearchTickers(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Trim().Length < 2)
            return [];
        else
            return await fetcher.SearchTickers(searchTerm.Trim());
    }

    public Ticker GetEmpheralTicker(string symbol)
    {
        var ticker = factory.Create(symbol);
        Task.Run(async () => await ticker.Refresh(TickerRange.Day));
        return ticker;
    }

    public void MoveTicker(Ticker ticker, int index)
    {
        Watchlists.MoveSymbolInActiveWatchlist(ticker.Symbol, index);
    }

    public Task AddTicker(string symbol)
    {
        Watchlists.AddSymbolToWatchlist(symbol, Watchlists.ActiveWatchlistId);
        return Task.CompletedTask;
    }

    public Task RemoveTicker(string symbol)
    {
        Watchlists.RemoveSymbolFromWatchlist(symbol, Watchlists.ActiveWatchlistId);
        return Task.CompletedTask;
    }

    public void SetTickerAlias(Ticker ticker, string alias)
    {
        aliasStorage.SetAlias(ticker.Symbol, alias);
        ticker.SetAlias(alias);
    }

    public void SetActive(Ticker ticker)
    {
        var previous = SelectedTicker;
        SelectedTicker = ticker;
        OnActiveTickerChanged?.Invoke(previous, ticker);
    }

    public Ticker? GetTicker(string symbol)
    {
        tickerCache.TryGetValue(NormalizeSymbol(symbol), out var ticker);
        return ticker;
    }

    public async Task UpdateAll(bool forceNetworkFetch)
    {
        var visibleTickers = Tickers.ToList();
        var tasks = visibleTickers
            .Select(x => x.Refresh(TickerRange.Day, forceNetworkFetch))
            .ToList();

        if (ActiveRange != TickerRange.Day && SelectedTicker is Ticker selectedTicker && visibleTickers.Contains(selectedTicker))
        {
            tasks.Add(selectedTicker.Refresh(ActiveRange, forceNetworkFetch));
        }

        await Task.WhenAll(tasks);
    }

    private void SwitchVisibleTickers(bool forceRefresh)
    {
        visibleWatchlistId = Watchlists.ActiveWatchlistId;
        var nextSymbols = Watchlists.GetActiveSymbols();

        Tickers.Clear();

        foreach (var symbol in nextSymbols)
            Tickers.Add(GetOrCreateTicker(symbol));

        SetActiveTicker(Tickers.FirstOrDefault());
        OnVisibleTickersReloaded?.Invoke();

        if (forceRefresh)
            Task.Run(async () => await UpdateAll(false));
    }

    private void HandleWatchlistGroupsChanged()
    {
        PruneUnusedTickers();
    }

    private void HandleActiveWatchlistChanged()
    {
        if (visibleWatchlistId == Watchlists.ActiveWatchlistId)
            return;

        SwitchVisibleTickers(forceRefresh: true);
    }

    private void HandleWatchlistSymbolAdded(string watchlistId, string symbol)
    {
        if (watchlistId != visibleWatchlistId)
            return;

        var ticker = GetOrCreateTicker(symbol);
        Tickers.Add(ticker);
        OnTickerAdded?.Invoke(ticker);
        Task.Run(async () => await ticker.Refresh(TickerRange.Day));
    }

    private void HandleWatchlistSymbolRemoved(string watchlistId, string symbol)
    {
        if (watchlistId != visibleWatchlistId)
            return;

        var ticker = tickerCache[NormalizeSymbol(symbol)];
        var wasSelected = SelectedTicker == ticker;
        Tickers.Remove(ticker);
        OnTickerRemoved?.Invoke(ticker);

        if (wasSelected)
            SetActiveTicker(Tickers.FirstOrDefault());
    }

    private void HandleWatchlistSymbolMoved(string watchlistId, string symbol, int index)
    {
        if (watchlistId != visibleWatchlistId)
            return;

        var ticker = tickerCache[NormalizeSymbol(symbol)];
        var oldIndex = Tickers.IndexOf(ticker);
        Tickers.RemoveAt(oldIndex);
        var newIndex = Math.Clamp(index, 0, Tickers.Count);
        Tickers.Insert(newIndex, ticker);
        OnTickerMoved?.Invoke(ticker, newIndex);
    }

    private void SetActiveTicker(Ticker? ticker)
    {
        var previous = SelectedTicker;
        SelectedTicker = ticker;

        if (!ReferenceEquals(previous, ticker))
            OnActiveTickerChanged?.Invoke(previous, ticker);
    }

    private Ticker GetOrCreateTicker(string symbol)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);

        if (tickerCache.TryGetValue(normalizedSymbol, out var ticker))
            return ticker;

        ticker = factory.Create(normalizedSymbol);

        var alias = aliasStorage.GetAlias(normalizedSymbol);
        if (!string.IsNullOrWhiteSpace(alias))
            ticker.SetAlias(alias);

        tickerCache[normalizedSymbol] = ticker;
        return ticker;
    }

    private void PruneUnusedTickers()
    {
        foreach (var symbol in tickerCache.Keys.ToList())
            PruneUnusedTicker(symbol);
    }

    private void PruneUnusedTicker(string symbol)
    {
        if (Watchlists.IsSymbolInAnyWatchlist(symbol))
            return;

        tickerCache.Remove(symbol);
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return "";

        return symbol.Trim().ToUpperInvariant();
    }

    private async Task StartAutoUpdate()
    {
        do
        {
            try { await UpdateAll(false); }
            catch { }
        }
        while (await timer.WaitForNextTickAsync(cts.Token));
    }
}
