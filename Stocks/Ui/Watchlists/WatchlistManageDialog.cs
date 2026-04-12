// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

internal sealed class WatchlistManageDialog
{
    private readonly WatchlistModel model;
    private readonly Adw.PreferencesDialog dialog;
    private readonly Adw.PreferencesGroup watchlistsGroup;
    private readonly Gtk.Button addButton;
    private readonly List<Adw.PreferencesRow> rows = [];

    private EditState? editState;
    private Adw.EntryRow? currentEntryRow;

    private WatchlistManageDialog(WatchlistModel model)
    {
        this.model = model;

        // Load UI from blueprint (Blueprint is bit useless here really... oh well.)
        var builder = Builder.FromFile("WatchlistManageDialog.ui");
        dialog = GetRequiredObject<Adw.PreferencesDialog>(builder, "watchlistManageDialog");
        watchlistsGroup = GetRequiredObject<Adw.PreferencesGroup>(builder, "watchlistsGroup");
        addButton = GetRequiredObject<Gtk.Button>(builder, "addButton");

        addButton.OnClicked += (_, _) => BeginAdd();
        model.OnChanged += RebuildRows;
        model.OnActiveChanged += RebuildRows;
        dialog.OnClosed += HandleClosed;

        RebuildRows();
    }

    public static void Present(WatchlistModel watchlists, Gtk.Widget? parent)
    {
        if (parent is null)
            return;

        var dialog = new WatchlistManageDialog(watchlists);
        dialog.dialog.Present(parent);
    }

    private void HandleClosed(Adw.Dialog dialog, EventArgs args)
    {
        model.OnChanged -= RebuildRows;
        model.OnActiveChanged -= RebuildRows;
    }

    private void RebuildRows()
    {
        foreach (var row in rows)
            watchlistsGroup.Remove(row);

        rows.Clear();
        currentEntryRow = null;

        var watchlists = model.GetWatchlists();
        if (editState is { WatchlistId: not null } state && !watchlists.Any(watchlist => watchlist.Id == state.WatchlistId))
        {
            editState = null;
        }

        addButton.Sensitive = editState is null;
        var allowDelete = watchlists.Count > 1;

        foreach (var watchlist in watchlists)
        {
            Adw.PreferencesRow row = editState?.WatchlistId == watchlist.Id
                ? CreateEditRow(editState)
                : CreateDisplayRow(watchlist, allowDelete);

            watchlistsGroup.Add(row);
            rows.Add(row);
        }

        if (editState is { WatchlistId: null })
        {
            var newRow = CreateEditRow(editState);
            watchlistsGroup.Add(newRow);
            rows.Add(newRow);
        }

        if (currentEntryRow is not null)
        {
            GLib.Functions.IdleAdd(100, () =>
            {
                currentEntryRow.GrabFocus();
                return false;
            });
        }
    }

    private Adw.ActionRow CreateDisplayRow(WatchlistSummary watchlist, bool allowDelete)
    {
        var row = Adw.ActionRow.New();
        row.Title = watchlist.Name;
        row.Subtitle = FormatSymbolCount(watchlist.TickerCount);
        row.Activatable = false;

        var menuButton = Gtk.MenuButton.New();
        menuButton.IconName = "view-more-symbolic";
        menuButton.Valign = Gtk.Align.Center;
        menuButton.Sensitive = editState is null;
        menuButton.AddCssClass("flat");
        menuButton.Popover = CreateRowMenu(watchlist, allowDelete);

        row.AddSuffix(menuButton);

        return row;
    }

    private Adw.EntryRow CreateEditRow(EditState state)
    {
        var row = Adw.EntryRow.New();
        row.Title = _("Watchlist name");
        row.ShowApplyButton = true;
        row.SetText(state.Name);
        row.OnApply += (_, _) => SubmitRename(row);
        row.OnChanged += (_, _) =>
        {
            editState = state with { Name = row.GetText() };
            UpdateValidationState(row);
        };

        var cancelButton = CreateIconButton("process-stop-symbolic", _("Cancel"), CancelRename);
        row.AddSuffix(cancelButton);

        UpdateValidationState(row);
        currentEntryRow = row;

        return row;
    }

    private Gtk.PopoverMenu CreateRowMenu(WatchlistSummary watchlist, bool allowDelete)
    {
        const string actionGroupName = "watchlist-row";

        var actionGroup = Gio.SimpleActionGroup.New();

        var renameAction = Gio.SimpleAction.New("rename", null);
        renameAction.OnActivate += (_, _) => BeginRename(watchlist);
        actionGroup.AddAction(renameAction);

        var removeAction = Gio.SimpleAction.New("remove", null);
        removeAction.Enabled = allowDelete;
        removeAction.OnActivate += (_, _) => ConfirmDelete(watchlist);
        actionGroup.AddAction(removeAction);

        var menu = Gio.Menu.New();
        menu.Append(_("Rename"), $"{actionGroupName}.rename");
        menu.Append(_("Remove"), $"{actionGroupName}.remove");

        var popover = Gtk.PopoverMenu.NewFromModel(menu);
        popover.InsertActionGroup(actionGroupName, actionGroup);
        return popover;
    }

    private void BeginAdd()
    {
        if (editState is not null)
            return;

        editState = new EditState(null, "");
        RebuildRows();
    }

    private void BeginRename(WatchlistSummary watchlist)
    {
        if (editState is not null)
            return;

        editState = new EditState(watchlist.Id, watchlist.Name);
        RebuildRows();
    }

    private void CancelRename()
    {
        editState = null;
        RebuildRows();
    }

    private void SubmitRename(Adw.EntryRow row)
    {
        if (editState is null)
            return;

        var trimmedName = row.GetText().Trim();
        var error = GetValidationError(trimmedName, editState.WatchlistId);

        if (error is not null)
        {
            SetValidationError(row, error);
            return;
        }

        var watchlistId = editState.WatchlistId;
        editState = null;

        if (watchlistId is null)
            model.CreateWatchlist(trimmedName);
        else
            model.RenameWatchlist(watchlistId, trimmedName);
    }

    private void UpdateValidationState(Adw.EntryRow row)
    {
        if (editState is null)
            return;

        var error = GetValidationError(row.GetText().Trim(), editState.WatchlistId);
        SetValidationError(row, error);
    }

    private void SetValidationError(Adw.EntryRow row, string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            row.RemoveCssClass("error");
            row.TooltipText = null;
            return;
        }

        row.AddCssClass("error");
        row.TooltipText = error;
    }

    private string? GetValidationError(string name, string? watchlistId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return _("Watchlist name is required.");

        if (!model.IsWatchlistNameAvailable(name, watchlistId))
            return _("Watchlist name must be unique.");

        return null;
    }

    private void ConfirmDelete(WatchlistSummary watchlist)
    {
        if (dialog.Root is not Gtk.Widget parent)
            return;

        var alert = Adw.AlertDialog.New(
            _("Remove Watchlist"),
            string.Format(_("Remove '{0}'?"), watchlist.Name));

        alert.AddResponse("cancel", _("Cancel"));
        alert.AddResponse("remove", _("Remove"));
        alert.SetResponseAppearance("remove", Adw.ResponseAppearance.Destructive);
        alert.SetDefaultResponse("cancel");
        alert.SetCloseResponse("cancel");
        alert.OnResponse += (_, args) =>
        {
            if (args.Response != "remove")
                return;

            model.DeleteWatchlist(watchlist.Id);
        };

        alert.Present(parent);
    }

    // Ugly way to read objects from blueprint. Make it better some day...
    private static T GetRequiredObject<T>(Gtk.Builder builder, string name) where T : class
    {
        return builder.GetObject(name) as T
            ?? throw new InvalidOperationException(
                $"Builder object '{name}' is missing or has wrong type.");
    }

    private static Gtk.Button CreateIconButton(
        string iconName,
        string tooltip,
        Action callback)
    {
        var button = Gtk.Button.New();
        button.TooltipText = tooltip;
        button.AddCssClass("flat");
        button.SetChild(Gtk.Image.NewFromIconName(iconName));
        button.OnClicked += (_, _) => callback();
        return button;
    }

    private static string FormatSymbolCount(int count)
    {
        return count == 1
            ? _("1 symbol")
            : string.Format(_("{0} symbols"), count);
    }

    private sealed record EditState(string? WatchlistId, string Name);
}
