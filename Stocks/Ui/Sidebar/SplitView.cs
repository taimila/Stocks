// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class SplitView : Gtk.Box
{
    [Gtk.Connect] private readonly Adw.NavigationSplitView splitView;
    [Gtk.Connect] private readonly Gtk.ScrolledWindow sidebarContainer;
    [Gtk.Connect] private readonly Adw.Bin detailsContainer;
    [Gtk.Connect] private readonly Adw.Banner errorBanner;
    [Gtk.Connect] private readonly Adw.HeaderBar sidebarHeader;
    [Gtk.Connect] private readonly Adw.ToastOverlay toastOverlay;
    [Gtk.Connect] private readonly Gtk.MenuButton menuButton;
    [Gtk.Connect] private readonly Adw.NavigationPage detailsContent;
    [Gtk.Connect] private readonly Adw.HeaderBar detailsHeader;

    private readonly AppModel model;
    private readonly Sidebar sidebar;
    private readonly TickerDetails details;
    private readonly HashSet<Ticker> observedTickers = [];
    private bool isNarrow;

    private SplitView(Gtk.Builder builder): base()
    {
        builder.Connect(this);
        Hexpand = true;
        Vexpand = true;
        splitView!.Hexpand = true;
        splitView.Vexpand = true;
        Append(splitView);
    }

    public SplitView(AppModel model, Adw.ApplicationWindow window): this(Builder.FromFile("SplitView.ui"))
    {
        this.model = model;

        details = new TickerDetails(model);
        detailsContainer.SetChild(details);

        sidebar = new Sidebar(model);
        sidebarContainer.SetChild(sidebar);
        sidebar.OnTickerActivated += OnSidebarTickerActivated;

        sidebarHeader.PackStart(new AddButton(model));
        sidebarHeader.ShowTitle = true;
        sidebarHeader.SetTitleWidget(new WatchlistButton(model.Watchlists));
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
        var collapseBreakpoint = new Adw.Breakpoint
        {
            Condition = Adw.BreakpointCondition.Parse("max-width: 600px")
        };

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
