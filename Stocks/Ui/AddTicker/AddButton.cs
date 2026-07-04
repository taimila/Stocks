// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;
using static Stocks.Translations;

namespace Stocks.UI;

[GObject.Subclass<Gtk.Button>(qualifiedName: nameof(AddButton))]
public partial class AddButton
{
    // If window is narrower than this treshold, add is shown as dialog instead of popover.
    private const int AddDialogThreshold = 370;
    
    private AppModel model = null!;

    public static AddButton NewWithModel(AppModel model)
    {
        var button = NewWithProperties([]);
        button.SetModel(model);
        return button;
    }

    private void SetModel(AppModel model)
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
        var popover = AddTickerPopover.NewWithModel(model);
        popover.SetParent(this);
        popover.Show();
    }

    private void ShowAddDialog()
    {
        if (Root is not Gtk.Widget parent)
            return;

        var dialog = AddTickerDialog.NewWithModel(model);
        dialog.Present(parent);
    }
}
