// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class AddButton : Gtk.Button
{
    // If window is narrower than this treshold, add is shown as dialog instead of popover.
    private const int AddDialogThreshold = 370;
    
    private readonly AppModel model;

    public AddButton(AppModel model)
    {
        TooltipText = _("Add symbol");
        IconName = "list-add-symbolic";
        
        this.model = model;
        this.OnClicked += (b, _) => ShowAdd();
    }

    private void ShowAdd()
    {
        if (ShouldUseDialog())
            ShowAddDialog();
        else
            ShowAddPopover();
    }

    private bool ShouldUseDialog()
    {
        var width = (Root as Gtk.Window)?.GetWidth() ?? GetWidth();
        return width <= AddDialogThreshold;
    }

    private void ShowAddPopover()
    {
        var popover = new AddTickerPopover(model);
        popover.SetParent(this);
        popover.Show();
    }

    private void ShowAddDialog()
    {
        if (Root is not Gtk.Widget parent)
            return;

        var dialog = new AddTickerDialog(model);
        dialog.Present(parent);
    }
}
