// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public class SymbolStorage
{
    private readonly Gio.Settings settings;

    public SymbolStorage(Gio.Settings settings)
    {
        this.settings = settings;
    }

    public List<Symbol> All =>
        settings.GetStrv("symbols")
            .Select(value => Symbol.TryCreate(value, out var symbol) ? symbol : null)
            .Where(symbol => symbol is not null)
            .Select(symbol => symbol!)
            .ToList();

    public void Add(Symbol symbol)
    {
        var x = All;
        x.Add(symbol);
        settings.SetStrv("symbols", x.Select(symbol => symbol.Value).ToArray());
    }

    public void Move(Symbol symbol, int index)
    {
        var x = All;
        x.Remove(symbol);
        x.Insert(index, symbol);
        settings.SetStrv("symbols", x.Select(symbol => symbol.Value).ToArray());
    }

    public void Remove(Symbol symbol)
    {
        var x = All;
        x.Remove(symbol);
        settings.SetStrv("symbols", x.Select(symbol => symbol.Value).ToArray());
    }
}
