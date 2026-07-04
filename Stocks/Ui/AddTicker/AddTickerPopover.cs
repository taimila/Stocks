// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;
using static Stocks.Translations;

namespace Stocks.UI;

[GObject.Subclass<Gtk.Popover>(qualifiedName: nameof(AddTickerPopover))]
partial class AddTickerPopover
{
    public static AddTickerPopover NewWithModel(AppModel model)
    {
        var popover = NewWithProperties([]);
        popover.SetModel(model);
        return popover;
    }

    private void SetModel(AppModel model)
    {
        var content = AddTickerView.NewWithModel(model, () => Hide());
        SetChild(content);
    }
}

[GObject.Subclass<Adw.Dialog>(qualifiedName: nameof(AddTickerDialog))]
partial class AddTickerDialog
{
    public static AddTickerDialog NewWithModel(AppModel model)
    {
        var dialog = NewWithProperties([]);
        dialog.SetModel(model);
        return dialog;
    }

    private void SetModel(AppModel model)
    {
        var content = AddTickerView.NewWithModel(model, () => Close());
        content.MarginStart = 8;
        content.MarginEnd = 8;
        content.MarginTop = 8;
        content.MarginBottom = 8;
        SetChild(content);
    }
}

[GObject.Subclass<Gtk.Box>(qualifiedName: nameof(AddTickerView))]
partial class AddTickerView
{
    private List<SearchResultListRow> results = [];

    // Flag for preventing callback to fire if search has been closed already
    private bool isActive = true;

    // As user types into search entry, we fire callback OnSearchChanged.
    // It can be that user keeps typing a longer search term and multiple
    // Searches have been fired parallerl. This variable keeps count of 
    // searches and ensures that older search can't override newer one when
    // results are handled.
    private int searchCount = 0;

    // Size groups to align sidebar item layouts.
    private Gtk.SizeGroup g1 = Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal);
    private Gtk.SizeGroup g2 = Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal);
    private Gtk.SizeGroup g3 = Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal);

    private readonly Adw.StatusPage emptyState = Adw.StatusPage.New();

    public static AddTickerView NewWithModel(AppModel model, Action hideAction)
    {
        var view = NewWithProperties([]);
        view.SetModel(model, hideAction);
        return view;
    }

    private void SetModel(AppModel model, Action hideAction)
    {
        var box = Gtk.Box.New(Gtk.Orientation.Vertical, 10);

        void SafeHide()
        {
            isActive = false;
            searchCount++;
            hideAction();
        }

        SetupCloseWithEsc(SafeHide);
        
        var resultBox = Gtk.ListBox.New();
        resultBox.AddCssClass("navigation-sidebar");
        resultBox.Vexpand = true;

        var scroll = Gtk.ScrolledWindow.New();
        scroll.HscrollbarPolicy = Gtk.PolicyType.Never;
        scroll.SetChild(resultBox);

        void AddTicker(string symbol)
        {
            model.AddTicker(symbol);
            SafeHide();
        }
        
        var search = Gtk.SearchEntry.New();
        search.PlaceholderText = _("Search symbols");
        search.Hexpand = true;
        search.OnSearchChanged += async (sender, args) => {
            var currentSearch = ++searchCount;
            var result = await model.SearchTickers(sender.GetText());

            GLib.Functions.IdleAdd(100, () =>
            {
                // Ensure we don't override results with older search finishing later.
                if (!isActive || currentSearch != searchCount)
                    return false;

                g1 = Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal);
                g2 = Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal);
                g3 = Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal);

                // If there are results show them, otherwise show prompt.
                if (result.Count > 0)
                {
                    if (emptyState.Parent != null)
                        box.Remove(emptyState);
                    box.Append(scroll);
                }
                else
                {
                    if (scroll.Parent != null)
                        box.Remove(scroll);
                    if (result.Count == 0 && search.GetText().Length > 0)
                        SetEmptyStateToNoResults();
                    else
                        SetEmptyStateToPrompt();
                    box.Append(emptyState);
                }

                results.ForEach(x => x.OnAdd -= AddTicker);
                resultBox.RemoveAll();
                results = result.Select(x => SearchResultListRow.NewWithResult(g1, g2, g3, model, x)).ToList();
                results.ForEach(x => x.OnAdd += AddTicker);
                results.ToList().ForEach(x => resultBox.Append(x));
                return false;
            });
        };

        box.Append(search);

        SetEmptyStateToPrompt();
        box.Append(emptyState);

        WidthRequest = 340;
        HeightRequest = 380;
        Append(box);
    }

    // Popover / Dialog does not close with esc button automatically because
    // SearchEntry consumes the esc key. This fixes the issue and enables
    // keyboard travelsar from ticker addition back to main window.
    private void SetupCloseWithEsc(Action hideAction)
    {
        var keyController = Gtk.EventControllerKey.New();
        keyController.SetPropagationPhase(Gtk.PropagationPhase.Capture);
        keyController.OnKeyPressed += (_, e) =>
        {
            if (e.Keyval == 0xff1b)
            {
                hideAction();
                return true;
            }
            return false;
        };
        AddController(keyController);
    }

    private void SetEmptyStateToPrompt()
    {
        emptyState.IconName = "edit-find-symbolic";
        emptyState.Title = _("Search symbols");
        emptyState.Description = _("Start typing to see search results.");
    }

    private void SetEmptyStateToNoResults()
    {
        emptyState.IconName = "edit-find-symbolic";
        emptyState.Title = _("No results");
        emptyState.Description = _("No results found for current search term.");
    }

    [GObject.Subclass<Gtk.ListBoxRow>(qualifiedName: nameof(SearchResultListRow))]
    internal partial class SearchResultListRow
    {
        public event Action<string>? OnAdd;

        public static SearchResultListRow NewWithResult(Gtk.SizeGroup g1, Gtk.SizeGroup g2, Gtk.SizeGroup g3, AppModel model, SearchResult result)
        {
            var row = NewWithProperties([]);
            row.SetResult(g1, g2, g3, model, result);
            return row;
        }

        private void SetResult(Gtk.SizeGroup g1, Gtk.SizeGroup g2, Gtk.SizeGroup g3, AppModel model, SearchResult result)
        {
            Selectable = false;

            // Allow adding only for symbols that are not yet on watch list.
            var canAdd = !model.Tickers.Select(x => x.Symbol).Contains(result.Symbol);

            var add = Gtk.Button.NewFromIconName(canAdd ? "list-add-symbolic": "checkmark-symbolic");
            add.AddCssClass("suggested-action");
            add.AddCssClass("circular");
            add.Sensitive = canAdd;
            add.OnClicked += (_, _) => OnAdd?.Invoke(result.Symbol);
            add.Valign = Gtk.Align.Center;
            add.TooltipText = canAdd ? _("Add to watchlist") : _("Already on watchlist");

            var sidebarItem = SidebarItem.NewWithTicker(g1, g2, g3, model.GetEmpheralTicker(result.Symbol));
            sidebarItem.Sensitive = canAdd;

            // Hack to fix padding issue of the name label when in popover.
            if (sidebarItem.GetChildAt(0, 1) is Gtk.Label name)
            {
                name.RemoveCssClass("subtitle");
                name.AddCssClass("dim-label");
            }
            
            var hbox = Gtk.Box.New(Gtk.Orientation.Horizontal, 16);
            hbox.Append(add);
            hbox.Append(sidebarItem);

            SetChild(hbox);
        }
    }
}
