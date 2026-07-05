// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

[GObject.Subclass<Gtk.Box>(qualifiedName: nameof(EmptyView))]
[Gtk.Template<Gtk.AssemblyResource>("EmptyView.ui")]
public partial class EmptyView
{
    [Gtk.Connect] private Adw.HeaderBar header;
    [Gtk.Connect] private Gtk.Button addSymbolButton;
    [Gtk.Connect] private Gtk.MenuButton menuButton;

    public static EmptyView NewWithModel(AppModel model)
    {
        var view = NewWithProperties([]);
        view.SetModel(model);
        return view;
    }

    private void SetModel(AppModel model)
    {
        header.PackStart(AddButton.NewWithModel(model));
        header.ShowTitle = true;
        header.SetTitleWidget(WatchlistButton.NewWithModel(model.Watchlists));

        addSymbolButton.OnClicked += (_, _) =>
        {
            var dialog = AddTickerDialog.NewWithModel(model);
            dialog.Present(Root as Gtk.Widget);
        };
    }

    public Gtk.MenuButton MenuButton => menuButton;
}
