// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public class AppModel
{
    private readonly TickerFetcher fetcher;
    private readonly TickerFactory factory;
    private readonly SymbolStorage storage;
    private readonly AliasStorage aliasStorage;
    private readonly PeriodicTimer timer;
    private readonly CancellationTokenSource cts = new();

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

    public List<Ticker> Tickers = [];
    public bool HasTickers => Tickers.Count > 0;
    public Ticker? SelectedTicker { get; private set; } = null;

    public event Action<Ticker>? OnTickerAdded;
    public event Action<Ticker>? OnTickerRemoved;
    public event Action<Ticker?, Ticker>? OnActiveTickerChanged;
    public event Action<TickerRange>? OnActiveTickerRangeChanged;

    public AppModel(TickerFetcher fetcher, TickerFactory factory, AppSettings appSettings, SymbolStorage storage, AliasStorage aliasStorage)
    {
        this.timer = new(TimeSpan.FromSeconds(appSettings.UpdateIntervalInSeconds));
        this.fetcher = fetcher;
        this.factory = factory;
        this.storage = storage;
        this.aliasStorage = aliasStorage;

        Tickers = this.storage
            .All
            .Select(factory.Create)
            .ToList();

        foreach (var ticker in Tickers)
        {
            var alias = aliasStorage.GetAlias(ticker.Symbol);
            if (!string.IsNullOrWhiteSpace(alias))
                ticker.SetAlias(alias);
        }

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
        Tickers.Remove(ticker);
        Tickers.Insert(index, ticker);
        storage.Move(ticker.Symbol, index);
    }

    public async Task AddTicker(string symbol)
    {
        // Do not allow duplicate tickers.
        if (!Tickers.Any(t => t.Symbol == symbol))
        {
            var ticker = factory.Create(symbol);
            Task.Run(async () => await ticker.Refresh(TickerRange.Day));
            storage.Add(symbol);
            Tickers.Add(ticker);
            OnTickerAdded?.Invoke(ticker);
        }
    }

    public async Task RemoveTicker(string symbol)
    {
        if (Tickers.Any(t => t.Symbol == symbol))
        {
            var ticker = Tickers.First(t => t.Symbol == symbol);
            storage.Remove(symbol);
            aliasStorage.RemoveAlias(symbol);
            Tickers.RemoveAll(t => t.Symbol == symbol);
            OnTickerRemoved?.Invoke(ticker);
        }
    }

    public void SetTickerAlias(Ticker ticker, string alias)
    {
        if (!Tickers.Contains(ticker))
            return;

        aliasStorage.SetAlias(ticker.Symbol, alias);
        ticker.SetAlias(alias);
    }

    public void SetActive(Ticker ticker)
    {
        if (Tickers.Contains(ticker))
        {
            var previous = SelectedTicker;
            SelectedTicker = ticker;
            OnActiveTickerChanged?.Invoke(previous, ticker);
        }
    }

    public Ticker? GetTicker(string symbol)
    {
        return Tickers.FirstOrDefault(t => t.Symbol == symbol);
    }

    public async Task UpdateAll(bool forceNetworkFetch)
    {
        var tasks = Tickers
            .Select(x => x.Refresh(TickerRange.Day, forceNetworkFetch))
            .ToList();

        if (ActiveRange != TickerRange.Day && SelectedTicker is Ticker selectedTicker)
        {
            tasks.Add(selectedTicker.Refresh(ActiveRange, forceNetworkFetch));
        }

        await Task.WhenAll(tasks);
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
