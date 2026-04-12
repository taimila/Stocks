// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

internal class WatchlistState
{
    public string ActiveListId { get; set; } = "";
    public List<Watchlist> Lists { get; set; } = [];
}

internal class Watchlist
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Symbols { get; set; } = [];
}
