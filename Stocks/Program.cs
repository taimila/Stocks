// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

global using static Stocks.Constants;
global using static Stocks.Translations;

namespace Stocks;

public class Program
{
    private static readonly CompositionRoot compositionRoot = new();

    public static void Main(string[] args)
    {
        Translations.Initialize();        
        Gio.Module.Initialize();
        var app = compositionRoot.Application;
        app.Run(args);
    }

}
