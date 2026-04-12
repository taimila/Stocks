// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks;

class CompositionRoot
{
    public Application Application { get; private set; }

    public CompositionRoot()
    {
        var settings = Gio.Settings.New(APP_ID);
        var appSettings = new AppSettings(settings);
        var httpClient = new HttpClient();
        var fetcher = new TickerFetcher(appSettings, httpClient);
        var factory = new TickerFactory(fetcher);
        var watchlistMigrator = new WatchlistMigrator(settings);
        var watchlistStorage = new WatchlistStorage(watchlistMigrator);
        var watchlists = new WatchlistModel(watchlistStorage);
        var aliasStorage = new AliasStorage();
        var model = new AppModel(fetcher, factory, appSettings, watchlists, aliasStorage);
        var app = new Application(model, settings);

        Application = app;
    }
}
