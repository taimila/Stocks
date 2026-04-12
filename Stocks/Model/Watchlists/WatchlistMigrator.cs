// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

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
            .Select(NormalizeSymbol)
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct()
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
}
