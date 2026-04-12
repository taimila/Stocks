// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

internal static class TickerContextMenu
{
    public static void Attach(Gtk.Widget widget, Ticker ticker)
    {
        var rightClick = Gtk.GestureClick.New();
        rightClick.SetButton(3);
        rightClick.OnPressed += (_, args) =>
        {
            var remove = new Gio.MenuItem();
            remove.SetLabel(Stocks.Translations._("Remove"));
            remove.SetActionAndTargetValue("app.remove", GLib.Variant.NewString(ticker.Symbol));

            var refresh = new Gio.MenuItem();
            refresh.SetLabel(Stocks.Translations._("Refresh"));
            refresh.SetActionAndTargetValue("app.refreshticker", GLib.Variant.NewString(ticker.Symbol));

            var point = new Gdk.Rectangle
            {
                X = (int)args.X,
                Y = (int)args.Y,
                Width = 1,
                Height = 1
            };

            var menu = Gio.Menu.New();
            menu.AppendItem(remove);
            menu.AppendItem(refresh);

            var popover = Gtk.PopoverMenu.NewFromModel(menu);
            popover.HasArrow = false;
            popover.SetPointingTo(point);
            popover.SetParent(widget);
            popover.Show();
        };

        widget.AddController(rightClick);
    }
}
