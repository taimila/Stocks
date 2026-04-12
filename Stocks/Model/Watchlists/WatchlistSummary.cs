// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public record WatchlistSummary(
    string Id, 
    string Name, 
    int TickerCount
);
