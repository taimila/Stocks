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

    public SplitView(AppModel model): this(Builder.FromFile("SplitView.ui"))
    {
        this.model = model;

        details = new TickerDetails(model);
        detailsContainer.SetChild(details);

        sidebar = new Sidebar(model);
        sidebarContainer.SetChild(sidebar);

        sidebarHeader.PackStart(new AddButton(model));

        splitView.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() != "collapsed")
                return;

            sidebar.IsCollapsed = splitView.Collapsed;
            UpdateDetailsHeaderTitle();
        };

        foreach (var ticker in model.Tickers)
            ticker.OnUpdated += OnTickerUpdated;

        model.OnTickerAdded += OnTickerAdded;
        model.OnTickerRemoved += OnTickerRemoved;
        model.OnActiveTickerChanged += OnActiveTickerChanged;

        // Default to details page; in collapsed mode this keeps content visible
        // instead of forcing the sidebar.
        splitView.ShowContent = true;
        sidebar.IsCollapsed = splitView.Collapsed;
        UpdateDetailsHeaderTitle();
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

    public void SetCollapsed(bool collapsed)
    {
        var wasCollapsed = splitView.Collapsed;

        splitView.Collapsed = collapsed;
        sidebar.IsCollapsed = collapsed;

        // When entering collapsed mode from wide layout, keep details visible.
        // If already collapsed, don't override user navigation back to sidebar.
        if (!collapsed || !wasCollapsed)
            splitView.ShowContent = true;

        UpdateDetailsHeaderTitle();
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
        ticker.OnUpdated += OnTickerUpdated;

        GLib.Functions.IdleAdd(100, () =>
        {
            sidebarContainer.ScrollToBottom();
            return false;
        });
    }

    private void OnTickerRemoved(Ticker ticker)
    {
        ticker.OnUpdated -= OnTickerUpdated;
        ShowToast(string.Format(_("{0} removed"), ticker.Symbol));
        UpdateErrorBannerState();
    }

    private void OnActiveTickerChanged(Ticker? _, Ticker ticker)
    {
        detailsContent.Title = ticker.DisplayName;
        splitView.ShowContent = true;
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

    private void UpdateErrorBannerState()
    {
        errorBanner.Revealed = model.Tickers.Any(x => x.DataFetchFailed);
    }

    private void ShowToast(string text)
    {
        toastOverlay.AddToast(Adw.Toast.New(text));
    }
}
