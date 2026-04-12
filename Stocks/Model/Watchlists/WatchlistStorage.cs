// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace Stocks.Model;

public class WatchlistStorage
{
    private readonly string filePath;
    private readonly WatchlistMigrator watchlistMigrator;

    public WatchlistStorage(WatchlistMigrator watchlistMigrator)
    {
        this.filePath = GetWatchlistFilePath();
        this.watchlistMigrator = watchlistMigrator;
    }

    internal WatchlistState Load()
    {
        if (TryLoadFromDisk(out var state))
        {
            return EnsureValid(state);
        }

        var migratedState = EnsureValid(watchlistMigrator.Migrate());

        if (TrySaveToDisk(migratedState))
            watchlistMigrator.ClearLegacySymbols();

        return migratedState;
    }

    internal void Save(WatchlistState state)
    {
        TrySaveToDisk(EnsureValid(Clone(state)));
    }

    private bool TryLoadFromDisk(out WatchlistState state)
    {
        state = new WatchlistState();

        if (!File.Exists(filePath))
            return false;

        try
        {
            var json = File.ReadAllText(filePath);
            var loaded = JsonSerializer.Deserialize<WatchlistState>(json);
            if (loaded is null)
                return false;

            state = loaded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TrySaveToDisk(WatchlistState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(state);
            File.WriteAllText(filePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static WatchlistState EnsureValid(WatchlistState state)
    {
        var result = new WatchlistState
        {
            ActiveListId = state.ActiveListId?.Trim() ?? "",
            Lists = []
        };

        foreach (var list in state.Lists ?? [])
        {
            var id = string.IsNullOrWhiteSpace(list.Id)
                ? CreateId()
                : list.Id.Trim();

            var name = string.IsNullOrWhiteSpace(list.Name)
                ? "Watchlist"
                : list.Name.Trim();

            var symbols = (list.Symbols ?? [])
                .Select(NormalizeSymbol)
                .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.Lists.Add(new Watchlist
            {
                Id = id,
                Name = name,
                Symbols = symbols
            });
        }

        if (result.Lists.Count == 0)
        {
            var defaultList = new Watchlist
            {
                Id = CreateId(),
                Name = C_("Name of the default watchlist", "Watchlist"),
                Symbols = []
            };

            result.Lists.Add(defaultList);
            result.ActiveListId = defaultList.Id;
        }

        if (!result.Lists.Any(list => list.Id == result.ActiveListId))
            result.ActiveListId = result.Lists[0].Id;

        return result;
    }

    private static WatchlistState Clone(WatchlistState state)
    {
        return new WatchlistState
        {
            ActiveListId = state.ActiveListId,
            Lists = state.Lists
                .Select(list => new Watchlist
                {
                    Id = list.Id,
                    Name = list.Name,
                    Symbols = [.. list.Symbols]
                })
                .ToList()
        };
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return "";

        return symbol.Trim().ToUpperInvariant();
    }

    private static string CreateId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string GetWatchlistFilePath()
    {
        var baseDir = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(baseDir, APP_ID, "watchlists.json");
    }
}
