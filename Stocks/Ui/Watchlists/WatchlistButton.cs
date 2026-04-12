// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class WatchlistButton : Gtk.MenuButton
{
    private const string ActionGroupName = "watchlist-selector";

    private readonly WatchlistModel model;
    private readonly Gtk.Label activeWatchlistName;

    public WatchlistButton(WatchlistModel model)
    {
        this.model = model;

        Halign = Gtk.Align.Center;
        Valign = Gtk.Align.Center;
        AddCssClass("flat");

        activeWatchlistName = Gtk.Label.New("");
        activeWatchlistName.Ellipsize = Pango.EllipsizeMode.End;
        activeWatchlistName.MaxWidthChars = 18;

        var arrow = Gtk.Image.NewFromIconName("pan-down-symbolic");

        var content = Gtk.Box.New(Gtk.Orientation.Horizontal, 6);
        content.Append(activeWatchlistName);
        content.Append(arrow);
        SetChild(content);

        InsertActionGroup(ActionGroupName, CreateActionGroup());

        UpdateLabel();
        UpdatePopover();

        model.OnChanged += HandleWatchlistsChanged;
        model.OnActiveChanged += HandleWatchlistsChanged;
    }

    private void HandleWatchlistsChanged()
    {
        UpdateLabel();
        UpdatePopover();
    }

    private void UpdateLabel()
    {
        activeWatchlistName.SetLabel(model.ActiveWatchlistName);
    }

    private Gio.SimpleActionGroup CreateActionGroup()
    {
        var actionGroup = Gio.SimpleActionGroup.New();

        var manageAction = Gio.SimpleAction.New("manage", null);
        manageAction.OnActivate += (_, _) => OpenManageDialog();
        actionGroup.AddAction(manageAction);

        return actionGroup;
    }

    private void UpdatePopover()
    {
        var menu = Gio.Menu.New();
        menu.Append(_("Manage Watchlists..."), $"{ActionGroupName}.manage");

        var popover = Gtk.PopoverMenu.NewFromModel(menu);
        popover.HasArrow = true;

        if (GetPopoverMenuContentBox(popover) is Gtk.Box contentBox)
        {
            var radiobuttons = new WatchlistRadioButtons(model, watchlistId =>
            {
                model.SetActiveWatchlist(watchlistId);
                Active = false;
            });

            contentBox.Prepend(Gtk.Separator.New(Gtk.Orientation.Horizontal));
            contentBox.Prepend(radiobuttons);
        }

        Popover = popover;
    }

    private void OpenManageDialog()
    {
        Active = false;

        GLib.Functions.IdleAdd(100, () =>
        {
            WatchlistManageDialog.Present(model, Root as Gtk.Widget);
            return false;
        });
    }

    private static Gtk.Box? GetPopoverMenuContentBox(Gtk.PopoverMenu popover)
    {
        return (((popover.GetChild() as Gtk.ScrolledWindow)
            ?.GetChild() as Gtk.Viewport)
            ?.GetChild() as Gtk.Stack)
            ?.VisibleChild as Gtk.Box;
    }
}
