// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using static Stocks.Translations;

namespace Stocks.Model;

/// <summary>
/// Simple tool to migrate watch list from GSettings to JSON based persistence
/// that was introduced with Watchlist support. GSettings was not flexible enough
/// to manage multiple ordered lists.
/// </summary>
public class WatchlistMigrator
{
    private readonly Gio.Settings settings;

    public WatchlistMigrator(Gio.Settings settings)
    {
        this.settings = settings;
    }

    internal WatchlistState Migrate()
    {
        var migratedSymbols = settings.GetStrv("symbols")
            .Select(symbol => Symbol.TryCreate(symbol, out var parsed) ? parsed : null)
            .Where(symbol => symbol is not null)
            .Select(symbol => symbol!)
            .Distinct()
            .Select(symbol => symbol.Value)
            .ToList();

        var list = new Watchlist
        {
            Id = CreateId(),
            Name = C_("Name of the default watchlist", "Watchlist"),
            Symbols = migratedSymbols
        };

        return new WatchlistState
        {
            ActiveListId = list.Id,
            Lists = [list]
        };
    }

    internal void ClearLegacySymbols()
    {
        settings.SetStrv("symbols", []);
    }

    private static string CreateId()
    {
        return Guid.NewGuid().ToString("N");
    }
}
