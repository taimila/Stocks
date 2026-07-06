// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

[GObject.Subclass<Adw.ApplicationWindow>(qualifiedName: nameof(MainWindow))]
[Gtk.Template<Gtk.AssemblyResource>("MainWindow.ui")]
public partial class MainWindow
{
    [Gtk.Connect] private Gtk.Stack stack;

    private AppModel model = null!;
    private AppTheme theme = null!;
    private AppMode mode = null!;
    private SplitView splitView = null!;
    private GridView gridView = null!;
    private EmptyView emptyView = null!;

    private bool isNarrow = false;

    public static MainWindow NewWithModel(Adw.Application application, AppModel model, Gio.Settings settings)
    {
        var window = NewWithProperties([]);
        window.SetModel(application, model, settings);
        return window;
    }

    private void SetModel(Adw.Application application, AppModel model, Gio.Settings settings)
    {
        WidthRequest = 355;

        this.model = model;
        Application = application;

        theme = new AppTheme(settings);
        mode = new AppMode(settings);
        splitView = SplitView.NewWithModel(model, this);
        gridView = GridView.NewWithModel(model);
        emptyView = EmptyView.NewWithModel(model);

        stack.AddNamed(splitView, "split");
        stack.AddNamed(gridView, "grid");
        stack.AddNamed(emptyView, "empty");

        mode.OnChanged += mode =>
        {
            // MenuButton is different in modes, so we need to reopen the menu
            // after mode changes. This causes flashy UI and should be fixed.
            var previouslyOpenMenu = GetOpenPrimaryMenuButton();
            SetBrowseMode(mode);
            ReopenPrimaryMenu(previouslyOpenMenu);
        };

        model.OnTickerAdded += _ => UpdateUI();
        model.OnTickerRemoved += _ => UpdateUI();
        model.OnVisibleTickersReloaded += UpdateUI;

        SetupPrimaryMenu();
        SetupUpdatesOnWindowResize();
        UpdateUI();
    }

    private void SetBrowseMode(BrowseMode mode)
    {
        splitView.BrowseModeChangedTo(mode);
        gridView.BrowseModeChangedTo(mode);

        this.mode.SetBrowseMode(mode);
        UpdateUI();
        UpdateResponsiveState();
    }

    // If there are no tickers, there is no visible change in UI, but mode still changes.
    // After user adds the first ticker, then current mode becomes visible.
    public void ToggleBrowseMode()
    {
        var nextMode = mode.Current == BrowseMode.Grid
            ? BrowseMode.List
            : BrowseMode.Grid;

        SetBrowseMode(nextMode);
    }

    private void SetupPrimaryMenu()
    {
        void InjectPrimaryMenuTopControls(Gtk.MenuButton target)
        {
            var controls = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
            controls.Hexpand = true;
            controls.Halign = Gtk.Align.Fill;

            var themeSwitcher = ThemeSwitcher.NewWithModel(theme);
            var modeSwitcher = BrowseModeSwitcher.NewWithModel(mode);
            var separator = Gtk.Separator.New(Gtk.Orientation.Horizontal);

            themeSwitcher.Hexpand = true;
            themeSwitcher.Halign = Gtk.Align.Fill;
            modeSwitcher.Hexpand = true;
            modeSwitcher.Halign = Gtk.Align.Fill;

            controls.Append(themeSwitcher);
            controls.Append(separator);
            controls.Append(modeSwitcher);

            // Inject custom controls into primary menu as first items.
            ((((target.Popover
                ?.GetChild() as Gtk.ScrolledWindow)
                ?.GetChild() as Gtk.Viewport)
                ?.GetChild() as Gtk.Stack)
                ?.VisibleChild as Gtk.Box)
                ?.Prepend(controls);
        }

        InjectPrimaryMenuTopControls(splitView.MenuButton);
        InjectPrimaryMenuTopControls(gridView.MenuButton);
        InjectPrimaryMenuTopControls(gridView.DetailsMenuButton);
        InjectPrimaryMenuTopControls(emptyView.MenuButton);
    }

    private Gtk.MenuButton? GetOpenPrimaryMenuButton()
    {
        if (splitView.MenuButton.Active)
            return splitView.MenuButton;

        if (gridView.MenuButton.Active)
            return gridView.MenuButton;

        if (gridView.DetailsMenuButton.Active)
            return gridView.DetailsMenuButton;

        if (emptyView.MenuButton.Active)
            return emptyView.MenuButton;

        return null;
    }

    private Gtk.MenuButton GetPrimaryMenuButtonForCurrentState()
    {
        if (!model.HasTickers)
            return emptyView.MenuButton;

        return mode.Current == BrowseMode.Grid
            ? gridView.MenuButton
            : splitView.MenuButton;
    }

    private void ReopenPrimaryMenu(Gtk.MenuButton? triggerButton)
    {
        if (triggerButton is null)
            return;

        GLib.Functions.IdleAdd(100, () =>
        {
            var target = GetPrimaryMenuButtonForCurrentState();
            target.Active = true;
            return false;
        });
    }

    private void SetupUpdatesOnWindowResize()
    {
        Application!.ActiveWindow!.OnNotify += (_, args) =>
        {
            var propertyName = args.Pspec.GetName();
            if (propertyName != "default-width" && propertyName != "width")
                return;

            UpdateResponsiveState();
        };

        GLib.Functions.IdleAdd(100, () =>
        {
            UpdateResponsiveState();
            return false;
        });
    }

    private void UpdateResponsiveState()
    {
        var width = GetWindowWidth();

        if (mode.Current == BrowseMode.List)
            SetNarrow(width <= 1000);
        else
            SetNarrow(width <= 700);
    }

    private int GetWindowWidth()
    {
        var width = Application?.ActiveWindow?.GetWidth() ?? GetWidth();
        return width > 0 ? width : 1100;
    }

    private void SetNarrow(bool enable)
    {
        if (enable == isNarrow)
            return;

        isNarrow = enable;
        splitView.SetIsNarrow(enable);
        gridView.SetIsNarrow(enable);
    }

    private void UpdateUI()
    {
        if (model.HasTickers)
            stack.VisibleChild = mode.Current == BrowseMode.Grid ? gridView : splitView;
        else
            stack.VisibleChild = emptyView;
    }
}
