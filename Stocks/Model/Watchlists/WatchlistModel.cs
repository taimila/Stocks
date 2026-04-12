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
    public event Action<string, string>? OnSymbolAdded;
    public event Action<string, string>? OnSymbolRemoved;
    public event Action<string, string, int>? OnSymbolMoved;

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

    public IReadOnlyList<string> GetActiveSymbols()
    {
        return GetActiveWatchlist().Symbols.ToList();
    }

    public bool IsSymbolInWatchlist(string symbol, string watchlistId)
    {
        return watchlistState.Lists.Any(group =>
            group.Id == watchlistId &&
            group.Symbols.Contains(NormalizeSymbol(symbol), StringComparer.OrdinalIgnoreCase));
    }

    public bool IsSymbolInAnyWatchlist(string symbol)
    {
        return watchlistState.Lists.Any(group =>
            group.Symbols.Contains(NormalizeSymbol(symbol), StringComparer.OrdinalIgnoreCase));
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

    public void AddSymbolToWatchlist(string symbol, string watchlistId)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var watchlist = watchlistState.Lists.First(group => group.Id == watchlistId);

        if (string.IsNullOrWhiteSpace(normalizedSymbol))
            return;

        if (watchlist.Symbols.Contains(normalizedSymbol, StringComparer.OrdinalIgnoreCase))
            return;

        watchlist.Symbols.Add(normalizedSymbol);
        SaveState();

        OnChanged?.Invoke();
        OnSymbolAdded?.Invoke(watchlistId, normalizedSymbol);
    }

    public void RemoveSymbolFromWatchlist(string symbol, string watchlistId)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var watchlist = watchlistState.Lists.First(group => group.Id == watchlistId);

        if (!watchlist.Symbols.Remove(normalizedSymbol))
            return;

        SaveState();

        OnSymbolRemoved?.Invoke(watchlistId, normalizedSymbol);
        OnChanged?.Invoke();
    }

    public void MoveSymbolInActiveWatchlist(string symbol, int index)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var watchlist = GetActiveWatchlist();
        var oldIndex = watchlist.Symbols.FindIndex(existing =>
            string.Equals(existing, normalizedSymbol, StringComparison.OrdinalIgnoreCase));

        var newIndex = Math.Clamp(index, 0, watchlist.Symbols.Count - 1);
        if (newIndex == oldIndex)
            return;

        watchlist.Symbols.RemoveAt(oldIndex);
        watchlist.Symbols.Insert(newIndex, normalizedSymbol);
        SaveState();
        OnSymbolMoved?.Invoke(watchlist.Id, normalizedSymbol, newIndex);
    }

    private Watchlist GetActiveWatchlist()
    {
        return watchlistState.Lists.First(group => group.Id == watchlistState.ActiveListId);
    }

    private void SaveState()
    {
        watchlistStorage.Save(watchlistState);
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return "";

        return symbol.Trim().ToUpperInvariant();
    }

    private static string NormalizeWatchlistName(string name)
    {
        return name.Trim();
    }
}
