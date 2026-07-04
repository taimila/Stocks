// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later
using Stocks.Model;

namespace Stocks.UI;

[GObject.Subclass<Gtk.Box>(qualifiedName: nameof(BrowseModeSwitcher))]
[Gtk.Template<Gtk.AssemblyResource>("BrowseModeSwitcher.ui")]
public partial class BrowseModeSwitcher
{
    [Gtk.Connect] private Gtk.ToggleButton listButton;
    [Gtk.Connect] private Gtk.ToggleButton gridButton;

    private AppMode model = null!;
    private bool syncingUi;

    public static BrowseModeSwitcher NewWithModel(AppMode model)
    {
        var switcher = NewWithProperties([]);
        switcher.SetModel(model);

        return switcher;
    }

    private void SetModel(AppMode model)
    {
        this.model = model;

        listButton.OnToggled += (_, _) => OnModeSelected(listButton, BrowseMode.List);
        gridButton.OnToggled += (_, _) => OnModeSelected(gridButton, BrowseMode.Grid);

        model.OnChanged += UpdateButtons;
        UpdateButtons(model.Current);
    }

    private void OnModeSelected(Gtk.ToggleButton source, BrowseMode mode)
    {
        if (syncingUi || !source.GetActive())
            return;

        model.SetBrowseMode(mode);
    }

    private void UpdateButtons(BrowseMode mode)
    {
        syncingUi = true;
        listButton.SetActive(mode == BrowseMode.List);
        gridButton.SetActive(mode == BrowseMode.Grid);
        syncingUi = false;
    }
}
