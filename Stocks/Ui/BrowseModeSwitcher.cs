// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later
using Stocks.Model;

namespace Stocks.UI;

public class BrowseModeSwitcher : Gtk.Box
{
    [Gtk.Connect] private readonly Gtk.ToggleButton listButton;
    [Gtk.Connect] private readonly Gtk.ToggleButton gridButton;

    private readonly AppMode model;
    private bool syncingUi;

    private BrowseModeSwitcher(Gtk.Builder builder, string name)
        : base(new Gtk.Internal.BoxHandle(builder.GetPointer(name), false))
    {
        builder.Connect(this);
    }

    public BrowseModeSwitcher(AppMode model)
        : this(Builder.FromFile("BrowseModeSwitcher.ui"), "browseModeSwitcher")
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
