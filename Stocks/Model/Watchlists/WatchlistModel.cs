// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public class WatchlistModel
{
    private readonly WatchlistStorage watchlistStorage;
    private readonly WatchlistState watchlistState;

    public string ActiveWatchlistId => watchlistState.ActiveListId;
    public string ActiveWatchlistName => GetActiveWatchlist().Name;

    public event Action? OnChanged;
    public event Action? OnActiveChanged;
    public event Action<string, Symbol>? OnSymbolAdded;
    public event Action<string, Symbol>? OnSymbolRemoved;
    public event Action<string, Symbol, int>? OnSymbolMoved;

    public WatchlistModel(WatchlistStorage watchlistStorage)
    {
        this.watchlistStorage = watchlistStorage;
        watchlistState = watchlistStorage.Load();
    }

    public IReadOnlyList<WatchlistSummary> GetWatchlists()
    {
        return watchlistState.Lists
            .Select(group => new WatchlistSummary(group.Id, group.Name, group.Symbols.Count))
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<Symbol> GetActiveSymbols()
    {
        return GetActiveWatchlist().Symbols
            .Select(symbol => Symbol.TryCreate(symbol, out var parsed) ? parsed : null)
            .Where(symbol => symbol is not null)
            .Select(symbol => symbol!)
            .ToList();
    }

    public IReadOnlyList<Symbol> GetAllSymbols()
    {
        return watchlistState.Lists
            .SelectMany(group => group.Symbols)
            .Select(symbol => Symbol.TryCreate(symbol, out var parsed) ? parsed : null)
            .Where(symbol => symbol is not null)
            .Select(symbol => symbol!)
            .Distinct()
            .ToList();
    }

    public bool IsSymbolInWatchlist(Symbol symbol, string watchlistId)
    {
        return watchlistState.Lists.Any(group =>
            group.Id == watchlistId &&
            group.Symbols.Contains(symbol.Value));
    }

    public bool IsSymbolInAnyWatchlist(Symbol symbol)
    {
        return watchlistState.Lists.Any(group =>
            group.Symbols.Contains(symbol.Value));
    }

    public bool IsWatchlistNameAvailable(string name, string? excludedWatchlistId = null)
    {
        var normalizedName = NormalizeWatchlistName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
            return false;

        return !watchlistState.Lists.Any(group =>
            group.Id != excludedWatchlistId &&
            string.Equals(group.Name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));
    }

    public void CreateWatchlist(string name)
    {
        var normalizedName = NormalizeWatchlistName(name);
        if (!IsWatchlistNameAvailable(normalizedName))
            return;

        watchlistState.Lists.Add(new Watchlist
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = normalizedName,
            Symbols = []
        });

        SaveState();
        OnChanged?.Invoke();
    }

    public void RenameWatchlist(string id, string name)
    {
        var watchlist = watchlistState.Lists.First(group => group.Id == id);
        var normalizedName = NormalizeWatchlistName(name);

        if (!IsWatchlistNameAvailable(normalizedName, id))
            return;

        watchlist.Name = normalizedName;
        SaveState();

        OnChanged?.Invoke();

        if (watchlist.Id == ActiveWatchlistId)
            OnActiveChanged?.Invoke();
    }

    public void DeleteWatchlist(string id)
    {
        // Never allow to remove all watchlists
        if (watchlistState.Lists.Count <= 1)
            return;

        var index = watchlistState.Lists.FindIndex(group => group.Id == id);
        if (index < 0)
            return;

        var activeChanged = watchlistState.ActiveListId == id;
        var replacementId = watchlistState.ActiveListId;

        if (activeChanged)
        {
            var replacementIndex = index < watchlistState.Lists.Count - 1
                ? index + 1
                : index - 1;

            replacementId = watchlistState.Lists[replacementIndex].Id;
        }

        watchlistState.Lists.RemoveAt(index);
        watchlistState.ActiveListId = replacementId;
        SaveState();

        OnChanged?.Invoke();

        if (activeChanged)
            OnActiveChanged?.Invoke();
    }

    public void SetActiveWatchlist(string id)
    {
        if (watchlistState.ActiveListId == id)
            return;

        watchlistState.ActiveListId = id;
        SaveState();
        OnActiveChanged?.Invoke();
    }

    public void AddSymbolToWatchlist(Symbol symbol, string watchlistId)
    {
        var watchlist = watchlistState.Lists.First(group => group.Id == watchlistId);

        if (watchlist.Symbols.Contains(symbol.Value))
            return;

        watchlist.Symbols.Add(symbol.Value);
        SaveState();

        OnChanged?.Invoke();
        OnSymbolAdded?.Invoke(watchlistId, symbol);
    }

    public void RemoveSymbolFromWatchlist(Symbol symbol, string watchlistId)
    {
        var watchlist = watchlistState.Lists.First(group => group.Id == watchlistId);

        if (!watchlist.Symbols.Remove(symbol.Value))
            return;

        SaveState();

        OnSymbolRemoved?.Invoke(watchlistId, symbol);
        OnChanged?.Invoke();
    }

    public void MoveSymbolInActiveWatchlist(Symbol symbol, int index)
    {
        var watchlist = GetActiveWatchlist();
        var oldIndex = watchlist.Symbols.FindIndex(existing => existing == symbol.Value);

        var newIndex = Math.Clamp(index, 0, watchlist.Symbols.Count - 1);
        if (newIndex == oldIndex)
            return;

        watchlist.Symbols.RemoveAt(oldIndex);
        watchlist.Symbols.Insert(newIndex, symbol.Value);
        SaveState();
        OnSymbolMoved?.Invoke(watchlist.Id, symbol, newIndex);
    }

    private Watchlist GetActiveWatchlist()
    {
        return watchlistState.Lists.First(group => group.Id == watchlistState.ActiveListId);
    }

    private void SaveState()
    {
        watchlistStorage.Save(watchlistState);
    }

    private static string NormalizeWatchlistName(string name)
    {
        return name.Trim();
    }
}
