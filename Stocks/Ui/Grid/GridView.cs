// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

[GObject.Subclass<Gtk.Box>(qualifiedName: nameof(GridView))]
[Gtk.Template<Gtk.AssemblyResource>("GridView.ui")]
public partial class GridView
{
    [Gtk.Connect] private Adw.Bin navigationContainer;
    [Gtk.Connect] private Gtk.Box pageStore;
    [Gtk.Connect] private Adw.NavigationPage gridContent;
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
    private TickerGrid tickerGrid = null!;
    private ScalingNavView gridView = null!;
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

        gridContent.Unparent();
        detailsContent.Unparent();
        pageStore.Unparent();

        gridView = ScalingNavView.NewWithProperties([]);
        gridView.Hexpand = true;
        gridView.Vexpand = true;
        gridView.Main = gridContent;
        gridView.Subpage = detailsContent;
        navigationContainer.SetChild(gridView);

        details = TickerDetails.NewWithModel(model);
        detailsContainer.SetChild(details);

        tickerGrid = TickerGrid.NewWithModel(model);
        gridView.GetThumbnail = () => tickerGrid.GetTransitionThumbnail(model.SelectedTicker);
        gridView.GetReturnThumbnail = () => tickerGrid.GetTransitionCard(model.SelectedTicker);
        gridView.GetSubpageBounds = () => details.GetChartBounds(gridView);
        gridView.OnOpening += details.SuspendChartHoverInteraction;
        gridView.OnClosing += details.SuspendChartHoverInteraction;
        gridView.OnTransitionStarted += SuspendChartDrawing;
        gridView.OnTransitionFinished += FinishTransition;
        tickerGrid.OnTickerActivated += ticker =>
        {
            model.SetActive(ticker);
            detailsContent.Title = ticker.DisplayName;
            gridView.ShowSubpage = true;
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

    private void SuspendChartDrawing()
    {
        tickerGrid.SuspendChartDrawing();
        details.SuspendChartDrawing();
    }

    private void FinishTransition()
    {
        tickerGrid.ResumeChartDrawing();
        details.ResumeChartDrawing();

        if (gridView.ShowSubpage)
        {
            details.ResumeChartHoverInteraction();
            return;
        }

        tickerGrid.RestoreActivatedCardFocus();
    }

    public Gtk.MenuButton MenuButton => menuButton;
    public Gtk.MenuButton DetailsMenuButton => detailsMenuButton;

    public void BrowseModeChangedTo(BrowseMode mode)
    {
        if (mode == BrowseMode.Grid)
            gridView.ShowMainImmediately();
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
