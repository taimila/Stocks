// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;
using static Stocks.Translations;

namespace Stocks.UI;

[GObject.Subclass<Gtk.Box>(qualifiedName: nameof(SplitView))]
[Gtk.Template<Gtk.AssemblyResource>("SplitView.ui")]
public partial class SplitView
{
    [Gtk.Connect] private Adw.NavigationSplitView splitView;
    [Gtk.Connect] private Gtk.ScrolledWindow sidebarContainer;
    [Gtk.Connect] private Adw.Bin detailsContainer;
    [Gtk.Connect] private Adw.Banner errorBanner;
    [Gtk.Connect] private Adw.HeaderBar sidebarHeader;
    [Gtk.Connect] private Adw.ToastOverlay toastOverlay;
    [Gtk.Connect] private Gtk.MenuButton menuButton;
    [Gtk.Connect] private Adw.NavigationPage detailsContent;
    [Gtk.Connect] private Adw.HeaderBar detailsHeader;

    private AppModel model = null!;
    private Sidebar sidebar = null!;
    private TickerDetails details = null!;
    private readonly HashSet<Ticker> observedTickers = [];
    private bool isNarrow;

    public static SplitView NewWithModel(AppModel model, Adw.ApplicationWindow window)
    {
        var view = NewWithProperties([]);
        view.SetModel(model, window);

        return view;
    }

    private void SetModel(AppModel model, Adw.ApplicationWindow window)
    {
        this.model = model;

        details = TickerDetails.NewWithModel(model);
        detailsContainer.SetChild(details);

        sidebar = Sidebar.NewWithModel(model);
        sidebarContainer.SetChild(sidebar);
        sidebar.OnTickerActivated += OnSidebarTickerActivated;

        sidebarHeader.PackStart(AddButton.NewWithModel(model));
        sidebarHeader.ShowTitle = true;
        sidebarHeader.SetTitleWidget(WatchlistButton.NewWithModel(model.Watchlists));
        SetupResponsiveCollapse(window);

        splitView.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() != "collapsed")
                return;

            HandleCollapsedChanged();
        };

        SyncVisibleTickerSubscriptions();

        model.OnTickerAdded += OnTickerAdded;
        model.OnTickerRemoved += OnTickerRemoved;
        model.OnVisibleTickersReloaded += SyncVisibleTickerSubscriptions;
        model.OnVisibleTickersReloaded += HandleVisibleTickersReloaded;
        model.OnActiveTickerChanged += OnActiveTickerChanged;

        HandleCollapsedChanged();
        UpdateErrorBannerState();
    }

    public Gtk.MenuButton MenuButton => menuButton;
    public bool Collapsed => splitView.Collapsed;

    public void BrowseModeChangedTo(BrowseMode mode)
    {
        if (mode == BrowseMode.Grid)
        {
            splitView.ShowContent = true;
        }
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

        GLib.Functions.IdleAdd(100, () =>
        {
            sidebarContainer.ScrollToBottom();
            return false;
        });
    }

    private void OnTickerRemoved(Ticker ticker)
    {
        UnobserveTicker(ticker);
        ShowToast(string.Format(_("{0} removed"), ticker.Symbol));
        UpdateErrorBannerState();
    }

    private void OnActiveTickerChanged(Ticker? _, Ticker? ticker)
    {
        if (ticker is null)
            return;

        detailsContent.Title = ticker.DisplayName;

        if (!splitView.Collapsed || splitView.ShowContent)
            splitView.ShowContent = true;
    }

    private void OnSidebarTickerActivated(Ticker ticker)
    {
        detailsContent.Title = ticker.DisplayName;
        splitView.ShowContent = true;
    }

    private void HandleVisibleTickersReloaded()
    {
        if (!splitView.Collapsed || !GetMapped())
            return;

        // When the active watchlist changes in collapsed list mode, stay on the list.
        splitView.ShowContent = false;
    }

    private void OnTickerUpdated(Ticker _)
    {
        GLib.Functions.IdleAdd(100, () =>
        {
            detailsContent.Title = model.SelectedTicker?.DisplayName ?? "";
            UpdateErrorBannerState();
            return false;
        });
    }

    private void UpdateDetailsHeaderTitle()
    {
        detailsHeader.ShowTitle = isNarrow || splitView.Collapsed;
    }

    private void SetupResponsiveCollapse(Adw.ApplicationWindow window)
    {
        var collapseBreakpoint = Adw.Breakpoint.New(Adw.BreakpointCondition.Parse("max-width: 600px"));

        collapseBreakpoint.AddSetter(
            splitView,
            "collapsed",
            new GObject.Value(true));

        window.AddBreakpoint(collapseBreakpoint);
    }

    private void HandleCollapsedChanged()
    {
        // Keep details visible when the split view enters or leaves collapsed mode.
        splitView.ShowContent = true;
        sidebar.SetLayoutState(splitView.Collapsed);
        UpdateDetailsHeaderTitle();
    }

    private void UpdateErrorBannerState()
    {
        errorBanner.Revealed = model.Tickers.Any(x => x.DataFetchFailed);
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

    private void ShowToast(string text)
    {
        toastOverlay.AddToast(Adw.Toast.New(text));
    }
}
