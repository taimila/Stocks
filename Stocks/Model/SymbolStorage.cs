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

    public List<string> All =>
        settings.GetStrv("symbols").ToList();

    public void Add(string symbol)
    {
        var x = All;
        x.Add(symbol);
        settings.SetStrv("symbols", x.ToArray());
    }

    public void Move(string symbol, int index)
    {
        var x = All;
        x.Remove(symbol);
        x.Insert(index, symbol);
        settings.SetStrv("symbols", x.ToArray());
    }

    public void Remove(string symbol)
    {
        var x = All;
        x.Remove(symbol);
        settings.SetStrv("symbols", x.ToArray());
    }
}
