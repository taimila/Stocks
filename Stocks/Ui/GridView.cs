// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class GridView : Gtk.Box
{
    [Gtk.Connect] private readonly Adw.NavigationView gridView;
    [Gtk.Connect] private readonly Adw.NavigationPage detailsContent;
    [Gtk.Connect] private readonly Gtk.ScrolledWindow scrollContainer;
    [Gtk.Connect] private readonly Adw.Bin detailsContainer;
    [Gtk.Connect] private readonly Adw.Banner errorBanner;
    [Gtk.Connect] private readonly Adw.HeaderBar gridHeader;
    [Gtk.Connect] private readonly Gtk.MenuButton menuButton;
    [Gtk.Connect] private readonly Gtk.MenuButton detailsMenuButton;
    [Gtk.Connect] private readonly Adw.HeaderBar detailsHeader;

    private readonly AppModel model;
    private readonly TickerDetails details;
    private bool isNarrow;

    private GridView(Gtk.Builder builder) : base()
    {
        builder.Connect(this);
        Hexpand = true;
        Vexpand = true;
        Append(gridView!);
    }

    public GridView(AppModel model): this(Builder.FromFile("GridView.ui"))
    {
        this.model = model;

        details = new TickerDetails(model);
        detailsContainer.SetChild(details);

        var tickerGrid = new TickerGrid(model);
        scrollContainer.SetChild(tickerGrid);

        gridHeader.PackStart(new AddButton(model));

        foreach (var ticker in model.Tickers)
            ticker.OnUpdated += OnTickerUpdated;

        model.OnTickerAdded += OnTickerAdded;
        model.OnTickerRemoved += OnTickerRemoved;
        model.OnActiveTickerChanged += (Ticker? _, Ticker ticker) =>
        {
            detailsContent.Title = ticker.DisplayName;
            gridView.PushByTag("details");
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
        ticker.OnUpdated += OnTickerUpdated;
        UpdateErrorBannerState();
    }

    private void OnTickerRemoved(Ticker ticker)
    {
        ticker.OnUpdated -= OnTickerUpdated;
        UpdateErrorBannerState();
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
