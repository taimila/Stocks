// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks;

class CompositionRoot
{
    public Application Application { get; private set; }

    /// <summary>
    /// Composition root of the app. Creates the object graph of the app
    /// by incjecting all the dependencies in place.
    /// 
    /// https://blog.ploeh.dk/2011/07/28/CompositionRoot/
    /// </summary>
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
