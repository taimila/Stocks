// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class MainWindow : Adw.ApplicationWindow
{
    [Gtk.Connect] private readonly Adw.NavigationSplitView splitView;
    [Gtk.Connect] private readonly Gtk.Stack mainContentStack;
    [Gtk.Connect] private readonly Adw.ToolbarView emptyView;
    [Gtk.Connect] private readonly Gtk.ScrolledWindow sidebarContainer;
    [Gtk.Connect] private readonly Adw.Bin detailsContainer;
    [Gtk.Connect] private readonly Adw.Banner errorBanner;
    [Gtk.Connect] private readonly Gtk.Button addButton;
    [Gtk.Connect] private readonly Gtk.Button emptyHeaderAddButton;
    [Gtk.Connect] private readonly Gtk.Button addSymbolButton;
    [Gtk.Connect] private readonly Adw.ToastOverlay toastOverlay;
    [Gtk.Connect] private readonly Gtk.MenuButton menuButton;
    [Gtk.Connect] private readonly Gtk.MenuButton emptyMenuButton;
    [Gtk.Connect] private readonly Adw.NavigationPage detailsContent;
    [Gtk.Connect] private readonly Adw.HeaderBar detailsHeader;

    private readonly AppModel model;
    private readonly Gio.Settings settings;
    private Sidebar sidebar;
    private bool isNarrow = false;

    private MainWindow(Gtk.Builder builder, string name) 
        : base(new Adw.Internal.ApplicationWindowHandle(builder.GetPointer(name), false))
    {
        builder.Connect(this);
        addButton!.OnClicked += (_, _) => { OnAdd(); };
        addSymbolButton!.OnClicked += (_, _) => { ShowAddDialog(); };
        emptyHeaderAddButton!.OnClicked += (_, _) => { OnAdd(); };
        splitView!.OnNotify += (_, args) =>
        {
            if (args.Pspec.GetName() == "collapsed")
                sidebar!.IsCollapsed = splitView.Collapsed;
        };
    }

    public MainWindow(Adw.Application application, AppModel model, Gio.Settings settings) : this(Builder.FromFile("MainWindow.ui"), "mainWindow")
    {
        this.model = model;
        this.settings = settings;
        Application = application;
        
        #if DEBUG
        AddCssClass("devel");
        #endif

        SetupThemeSwitcher();
        SetupUIUpdateWhenNarrow();
        SetupDetailsAndSidebar();
        SetupModelObserving();

        UpdateEmptyState();
    }

    private void SetupModelObserving()
    {
        foreach (var t in model.Tickers)
            t.OnUpdated += OnTickerUpdate;
     
        model.OnTickerAdded += (ticker) => {
            ticker.OnUpdated += OnTickerUpdate;
            UpdateEmptyState();
            GLib.Functions.IdleAdd(100, () =>
            {
                sidebarContainer.ScrollToBottom();
                return false;
            });
        };

        model.OnTickerRemoved += (ticker) => {
            ticker.OnUpdated -= OnTickerUpdate;
            ShowToast(string.Format(_("{0} removed"), ticker.Symbol));
            UpdateEmptyState();
        };

        model.OnActiveTickerChanged += (_, ticker) =>
        {
            detailsContent.Title = ticker.DisplayName;
            splitView.ShowContent = true;
        };
    }

    private void OnTickerUpdate(Ticker ticker)
    {
        GLib.Functions.IdleAdd(100, () =>
        {
            detailsContent.Title = model.SelectedTicker?.DisplayName ?? "";
            UpdateErrorBannerState();
            return false;
        });
    }

    private void SetupDetailsAndSidebar()
    {
        var details = new TickerDetails(model);
        detailsContainer.SetChild(details);
       
        sidebar = new Sidebar(model);
        sidebarContainer.SetChild(sidebar);
    }

    private void SetupThemeSwitcher()
    {
        void InjectThemeSwitcher(Gtk.MenuButton? target)
        {
            var themeSwitcher = new ThemeSwitcher(settings);

            // Inject theme switcher into primary menu as a first item.
            ((((target.Popover
                ?.GetChild() as Gtk.ScrolledWindow)
                ?.GetChild() as Gtk.Viewport)
                ?.GetChild() as Gtk.Stack)
                ?.VisibleChild as Gtk.Box)
                ?.Prepend(themeSwitcher);
        }

        InjectThemeSwitcher(menuButton);
        InjectThemeSwitcher(emptyMenuButton);
    }

    private void SetupUIUpdateWhenNarrow()
    {
        Application!.ActiveWindow!.OnNotify += (_,x) =>
        {
            if (x.Pspec.GetName() == "default-width")
                SetNarrow(Application.ActiveWindow.GetWidth() <= 1050);
        };
    }

    private void UpdateEmptyState()
    {
        mainContentStack.VisibleChild = model.HasTickers ? splitView : emptyView;
    }

    private void SetNarrow(bool enable)
    {
        if (enable == isNarrow)
            return;

        isNarrow = enable;
        (detailsContainer.Child as TickerDetails)?.SetIsNarrow(enable);

        detailsHeader.ShowTitle = enable;
    }

    private void ShowToast(string text)
    {
        toastOverlay.AddToast(Adw.Toast.New(text));
    }

    private void UpdateErrorBannerState()
    {
        errorBanner.Revealed = model.Tickers.Any(x => x.DataFetchFailed);
    }

    private void OnAdd()
    {
        if (splitView.Collapsed)
            ShowAddDialog();
        else
            ShowAddPopover();
    }

    private void ShowAddPopover()
    {
        var popover = new AddTickerPopover(model);
        popover.SetParent(model.HasTickers ? addButton : emptyHeaderAddButton);
        popover.Show();
    }

    private void ShowAddDialog()
    {
        var dialog = new AddTickerDialog(model);
        dialog.Present(this);
    }
}
