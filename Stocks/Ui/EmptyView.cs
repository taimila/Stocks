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

    private AppModel? model;

    public static EmptyView NewWithModel(AppModel model)
    {
        var view = NewWithProperties([]);
        view.SetModel(model);
        return view;
    }

    private void SetModel(AppModel model)
    {
        if (this.model is not null)
            throw new InvalidOperationException("EmptyView dependencies have already been set.");

        this.model = model;
        header.PackStart(new AddButton(model));
        header.ShowTitle = true;
        header.SetTitleWidget(new WatchlistButton(model.Watchlists));

        addSymbolButton.OnClicked += (_, _) =>
        {
            var dialog = new AddTickerDialog(model);
            dialog.Present(Root as Gtk.Widget);
        };
    }

    public Gtk.MenuButton MenuButton => menuButton;
}
