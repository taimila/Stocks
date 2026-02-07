// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public class TickerFactory(TickerFetcher fetcher)
{
    public Ticker Create(string symbol)
    {
        return new Ticker(symbol, fetcher, new Market());
    }
}
