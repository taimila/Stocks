// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

internal sealed class WatchlistRadioButtons : Gtk.Box
{
    private const int MaxShortcutHints = 9;

    public WatchlistRadioButtons(WatchlistModel model, Action<string> onSelection)
    {
        SetOrientation(Gtk.Orientation.Vertical);

        Hexpand = true;
        Halign = Gtk.Align.Fill;

        var content = Gtk.Box.New(Gtk.Orientation.Vertical, 3);
        content.Hexpand = true;
        content.Halign = Gtk.Align.Fill;
        content.MarginTop = 6;
        content.MarginBottom = 6;
        content.MarginStart = 6;
        content.MarginEnd = 6;

        Gtk.CheckButton? groupRoot = null;
        var hintSizeGroup = Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal);
        var slot = 1;

        foreach (var watchlist in model.GetWatchlists())
        {
            var row = Gtk.Box.New(Gtk.Orientation.Horizontal, 12);
            row.Hexpand = true;
            row.Halign = Gtk.Align.Fill;

            var button = Gtk.CheckButton.New();
            button.SetLabel(watchlist.Name);
            button.Halign = Gtk.Align.Fill;

            if (groupRoot is null)
                groupRoot = button;
            else
                button.SetGroup(groupRoot);

            button.SetActive(watchlist.Id == model.ActiveWatchlistId);
            button.OnToggled += (_, _) =>
            {
                if (!button.GetActive())
                    return;

                onSelection(watchlist.Id);
            };

            row.Append(button);

            var spacer = Gtk.Box.New(Gtk.Orientation.Horizontal, 0);
            spacer.Hexpand = true;
            row.Append(spacer);

            if (slot <= MaxShortcutHints)
            {
                var hint = CreateShortcutHint(slot);
                hintSizeGroup.AddWidget(hint);
                row.Append(hint);
            }

            content.Append(row);
            slot++;
        }

        Append(content);
    }

    private static Gtk.Label CreateShortcutHint(int slot)
    {
        var hint = Gtk.Label.New($"Ctrl+{slot}");
        hint.Halign = Gtk.Align.End;
        hint.Xalign = 0;
        hint.Valign = Gtk.Align.Center;
        hint.AddCssClass("dim-label");
        return hint;
    }
}
