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
    private readonly Dictionary<Symbol, Ticker> tickerCache = [];
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

        InitializeTickers();
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

    public Ticker GetEmpheralTicker(Symbol symbol)
    {
        var ticker = factory.Create(symbol);
        Task.Run(async () => await ticker.Refresh(TickerRange.Day));
        return ticker;
    }

    public void MoveTicker(Ticker ticker, int index)
    {
        Watchlists.MoveSymbolInActiveWatchlist(ticker.Symbol, index);
    }

    public Task AddTicker(Symbol symbol)
    {
        Watchlists.AddSymbolToWatchlist(symbol, Watchlists.ActiveWatchlistId);
        return Task.CompletedTask;
    }

    public Task RemoveTicker(Symbol symbol)
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

    public Ticker? GetTicker(Symbol symbol)
    {
        tickerCache.TryGetValue(symbol, out var ticker);
        return ticker;
    }

    public async Task UpdateAll(bool forceNetworkFetch)
    {
        var watchlistTickers = Watchlists.GetAllSymbols()
            .Select(GetOrCreateTicker)
            .ToList();
        var tasks = watchlistTickers
            .Select(x => x.Refresh(TickerRange.Day, forceNetworkFetch))
            .ToList();

        if (ActiveRange != TickerRange.Day && SelectedTicker is Ticker selectedTicker && watchlistTickers.Contains(selectedTicker))
        {
            tasks.Add(selectedTicker.Refresh(ActiveRange, forceNetworkFetch));
        }

        await Task.WhenAll(tasks);
    }

    private void SwitchVisibleTickers()
    {
        visibleWatchlistId = Watchlists.ActiveWatchlistId;
        LoadVisibleTickers(Watchlists.GetActiveSymbols());
        SetActiveTicker(Tickers.FirstOrDefault());
        OnVisibleTickersReloaded?.Invoke();
    }

    private void InitializeTickers()
    {
        var visibleSymbols = Watchlists.GetActiveSymbols();
        LoadVisibleTickers(visibleSymbols);
        CacheHiddenTickers(visibleSymbols);
    }

    private void LoadVisibleTickers(IReadOnlyList<Symbol> symbols)
    {
        Tickers.Clear();

        foreach (var symbol in symbols)
            Tickers.Add(GetOrCreateTicker(symbol));
    }

    private void CacheHiddenTickers(IReadOnlyList<Symbol> visibleSymbols)
    {
        var visibleSymbolSet = visibleSymbols.ToHashSet();

        foreach (var symbol in Watchlists.GetAllSymbols().Where(symbol => !visibleSymbolSet.Contains(symbol)))
            GetOrCreateTicker(symbol);
    }

    private void HandleWatchlistGroupsChanged()
    {
        PruneUnusedTickers();
    }

    private void HandleActiveWatchlistChanged()
    {
        if (visibleWatchlistId == Watchlists.ActiveWatchlistId)
            return;

        SwitchVisibleTickers();
    }

    private void HandleWatchlistSymbolAdded(string watchlistId, Symbol symbol)
    {
        var ticker = GetOrCreateTicker(symbol);

        if (watchlistId == visibleWatchlistId)
        {
            Tickers.Add(ticker);
            OnTickerAdded?.Invoke(ticker);
        }

        Task.Run(async () => await ticker.Refresh(TickerRange.Day));
    }

    private void HandleWatchlistSymbolRemoved(string watchlistId, Symbol symbol)
    {
        if (watchlistId != visibleWatchlistId)
        {
            PruneUnusedTicker(symbol);
            return;
        }

        var ticker = tickerCache[symbol];
        var wasSelected = SelectedTicker == ticker;
        Tickers.Remove(ticker);
        OnTickerRemoved?.Invoke(ticker);

        if (wasSelected)
            SetActiveTicker(Tickers.FirstOrDefault());

        PruneUnusedTicker(symbol);
    }

    private void HandleWatchlistSymbolMoved(string watchlistId, Symbol symbol, int index)
    {
        if (watchlistId != visibleWatchlistId)
            return;

        var ticker = tickerCache[symbol];
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

    private Ticker GetOrCreateTicker(Symbol symbol)
    {
        if (tickerCache.TryGetValue(symbol, out var ticker))
            return ticker;

        ticker = factory.Create(symbol);

        var alias = aliasStorage.GetAlias(symbol);
        if (!string.IsNullOrWhiteSpace(alias))
            ticker.SetAlias(alias);

        tickerCache[symbol] = ticker;
        return ticker;
    }

    private void PruneUnusedTickers()
    {
        foreach (var symbol in tickerCache.Keys.ToList())
            PruneUnusedTicker(symbol);
    }

    private void PruneUnusedTicker(Symbol symbol)
    {
        if (Watchlists.IsSymbolInAnyWatchlist(symbol))
            return;

        tickerCache.Remove(symbol);
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
