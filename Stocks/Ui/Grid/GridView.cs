// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

[GObject.Subclass<Gtk.Box>(qualifiedName: nameof(GridView))]
[Gtk.Template<Gtk.AssemblyResource>("GridView.ui")]
public partial class GridView
{
    [Gtk.Connect] private Adw.NavigationView gridView;
    [Gtk.Connect] private Adw.NavigationPage detailsContent;
    [Gtk.Connect] private Gtk.ScrolledWindow scrollContainer;
    [Gtk.Connect] private Adw.Bin detailsContainer;
    [Gtk.Connect] private Adw.Banner errorBanner;
    [Gtk.Connect] private Adw.HeaderBar gridHeader;
    [Gtk.Connect] private Gtk.MenuButton menuButton;
    [Gtk.Connect] private Gtk.MenuButton detailsMenuButton;
    [Gtk.Connect] private Adw.HeaderBar detailsHeader;

    private AppModel model = null!;
    private TickerDetails details = null!;
    private readonly HashSet<Ticker> observedTickers = [];
    private bool isNarrow;

    public static GridView NewWithModel(AppModel model)
    {
        var view = NewWithProperties([]);
        view.SetModel(model);
        return view;
    }

    private void SetModel(AppModel model)
    {
        this.model = model;

        details = TickerDetails.NewWithModel(model);
        detailsContainer.SetChild(details);

        var tickerGrid = TickerGrid.NewWithModel(model);
        tickerGrid.OnTickerActivated += ticker =>
        {
            model.SetActive(ticker);
            detailsContent.Title = ticker.DisplayName;
            gridView.PushByTag("details");
        };
        scrollContainer.SetChild(tickerGrid);

        gridHeader.PackStart(AddButton.NewWithModel(model));
        gridHeader.ShowTitle = true;
        gridHeader.SetTitleWidget(WatchlistButton.NewWithModel(model.Watchlists));

        SyncVisibleTickerSubscriptions();

        model.OnTickerAdded += OnTickerAdded;
        model.OnTickerRemoved += OnTickerRemoved;
        model.OnVisibleTickersReloaded += SyncVisibleTickerSubscriptions;
        model.OnActiveTickerChanged += (Ticker? _, Ticker? ticker) =>
        {
            if (ticker is null)
                return;

            detailsContent.Title = ticker.DisplayName;
        };
    }

    public Gtk.MenuButton MenuButton => menuButton;
    public Gtk.MenuButton DetailsMenuButton => detailsMenuButton;

    public void BrowseModeChangedTo(BrowseMode mode)
    {
        if (mode == BrowseMode.Grid && gridView.GetVisiblePageTag() != "grid")
            gridView.PopToTag("grid");
    }

    public void SetIsNarrow(bool enable)
    {
        if (enable == isNarrow)
            return;

        isNarrow = enable;
        details.SetIsNarrow(enable);
        UpdateDetailsHeaderTitle();
    }

    private void OnTickerAdded(Ticker ticker)
    {
        ObserveTicker(ticker);
        UpdateErrorBannerState();
    }

    private void OnTickerRemoved(Ticker ticker)
    {
        UnobserveTicker(ticker);
        UpdateErrorBannerState();
    }

    private void SyncVisibleTickerSubscriptions()
    {
        var visible = model.Tickers.ToHashSet();

        foreach (var ticker in observedTickers.Except(visible).ToList())
            UnobserveTicker(ticker);

        foreach (var ticker in visible.Except(observedTickers).ToList())
            ObserveTicker(ticker);

        UpdateErrorBannerState();
    }

    private void ObserveTicker(Ticker ticker)
    {
        if (!observedTickers.Add(ticker))
            return;

        ticker.OnUpdated += OnTickerUpdated;
    }

    private void UnobserveTicker(Ticker ticker)
    {
        if (!observedTickers.Remove(ticker))
            return;

        ticker.OnUpdated -= OnTickerUpdated;
    }

    private void OnTickerUpdated(Ticker _)
    {
        GLib.Functions.IdleAdd(100, () =>
        {
            UpdateErrorBannerState();
            return false;
        });
    }

    private void UpdateDetailsHeaderTitle()
    {
        detailsHeader.ShowTitle = isNarrow;
    }

    private void UpdateErrorBannerState()
    {
        errorBanner.Revealed = model.Tickers.Any(x => x.DataFetchFailed);
    }
}
