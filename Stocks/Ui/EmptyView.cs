// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class EmptyView : Gtk.Box
{
    [Gtk.Connect] private readonly Adw.ToolbarView emptyView;
    [Gtk.Connect] private readonly Adw.HeaderBar header;
    [Gtk.Connect] private readonly Gtk.Button addSymbolButton;
    [Gtk.Connect] private readonly Gtk.MenuButton menuButton;

    private readonly AppModel model;

    private EmptyView(Gtk.Builder builder) : base()
    {
        builder.Connect(this);
        Hexpand = true;
        Vexpand = true;
        Append(emptyView!);
    }

    public EmptyView(AppModel model): this(Builder.FromFile("EmptyView.ui"))
    {
        this.model = model;
        header.PackStart(new AddButton(model));

        addSymbolButton.OnClicked += (_, _) =>
        {
            var dialog = new AddTickerDialog(model);
            dialog.Present(Root as Gtk.Widget);
        };
    }

    public Gtk.MenuButton MenuButton => menuButton;
}
