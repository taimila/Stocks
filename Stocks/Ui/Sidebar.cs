// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class Sidebar: Gtk.ListBox
{
    private readonly AppModel model;
    private readonly Dictionary<string, SidebarItem> items = [];
    
    // Contains information during drag operation for reordering sidebar items.
    private DragState? dragState;

    // Suppress row selection when it's not done by the user.
    private bool suppressRowSelection = false;

    // MainWindow keeps this up-to-date so that Sidebar can behave correctly.
    public bool IsCollapsed{ get; set; } = false;

    // Size groups to align sidebar item layouts.
    private readonly Gtk.SizeGroup g1 = Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal);
    private readonly Gtk.SizeGroup g2 = Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal);
    private readonly Gtk.SizeGroup g3 = Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal);

    public Sidebar(AppModel model)
    {
        AddCssClass("navigation-sidebar");

        // When in collapsed mode, we want to navigate on activation
        OnRowActivated += (_, args) =>
        {
            if (IsCollapsed && args.Row?.Child is SidebarItem item)
            {
                model.SetActive(item.Ticker);
            }
        };

        // When in full width mode, we want to navigate on selection
        OnRowSelected += (_, args) =>
        {
            if (suppressRowSelection)
                return;

            if (!IsCollapsed && args.Row?.Child is SidebarItem item)
            {
                model.SetActive(item.Ticker);
            }
        };

        this.model = model;
        this.model.Tickers.ForEach(Add);
        this.model.OnTickerAdded += AddTicker; 
        this.model.OnTickerRemoved += RemoveTicker; 
        this.model.OnTickerMoved += (_, _) => UpdateUItoMatchTickerOrderInModel();
        this.model.OnActiveTickerChanged += (_, ticker) => SetSelectedRowTo(ticker);
        
        if (GetRowAtIndex(0) is Gtk.ListBoxRow row)
        {
            SelectRow(row);
        }
    }

    private void AddTicker(Ticker ticker)
    {
        Add(ticker);

        // If details is visible we will show newly added ticker immediately
        if (!IsCollapsed && GetRowAtIndex(model.Tickers.Count - 1) is Gtk.ListBoxRow row)
        {
            SelectRow(row);
        }
    }

    private void SetSelectedRowTo(Ticker ticker)
    {
        if (GetListBoxRowOf(ticker) is not Gtk.ListBoxRow row)
            return;

        suppressRowSelection = true;
        SelectRow(row);
        suppressRowSelection = false;
    }

    private void SetupDragAndDrop(Gtk.ListBoxRow row)
    {
        var dragSource = Gtk.DragSource.New();
        dragSource.SetActions(Gdk.DragAction.Move);
        dragSource.SetContent(Gdk.ContentProvider.NewForValue(new GObject.Value(row)));

        dragSource.OnDragBegin += (_, args) => 
        {
            if (row.Child is not SidebarItem item)
                return;

            var previewWidth = 290; //TODO: Does not work correctly when large fonts accessibility feature is enabled.
            var previewHeight = row.GetHeight();
            var preview = CreatePreviewFor(item.Ticker, previewWidth, previewHeight);

            Gtk.DragIcon
                .GetForDrag(args.Drag)
                .SetChild(preview);

            dragState = new DragState(row, item);            
            
            row.HeightRequest = previewHeight;
            row.SetChild(null);
            row.AddCssClass("drag-placeholder");
        };

        dragSource.OnDragEnd += (_, _) =>
        {
            CleanupDragState();
            UpdateUItoMatchTickerOrderInModel();
        };

        row.AddController(dragSource);

        var dropTarget = Gtk.DropTarget.New(GObject.Type.Object, Gdk.DragAction.Move);
        dropTarget.OnDrop += (_, _) =>
        {
            if (dragState?.Ticker is not Ticker ticker)
                return false;

            if (row != dragState.Row)
                MoveDraggedTickerTo(row.GetIndex());

            int targetIndex = dragState.Row.GetIndex();
            CleanupDragState();
            model.MoveTicker(ticker, targetIndex);
            return true;
        };

        dropTarget.OnMotion += (_, _) =>
        {
            if (dragState == null)
                return Gdk.DragAction.Move;

            if (row != dragState.Row)
                MoveDraggedTickerTo(row.GetIndex());

            return Gdk.DragAction.Move;
        };

        row.AddController(dropTarget);
    }

    private Gtk.Widget CreatePreviewFor(Ticker ticker, int width, int height)
    {
        var content = new SidebarItem(
            Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal),
            Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal),
            Gtk.SizeGroup.New(Gtk.SizeGroupMode.Horizontal),
            ticker);

        // Adds background to dragged item preview
        var frame = Gtk.Frame.New(null);
        frame.AddCssClass("sidebar-drag-preview");
        frame.SetChild(content);
        frame.WidthRequest = width;
        frame.HeightRequest = height;

        // Ensures dragged item preview stays same size as actual Sidebar items
        // even when ticker name is very long or short.
        var clamp = Adw.Clamp.New();
        clamp.SetChild(frame);
        clamp.WidthRequest = width;
        clamp.MaximumSize = width;
        clamp.TighteningThreshold = width;

        return clamp;
    }

    private void Add(Ticker ticker)
    {
        var item = new SidebarItem(g1, g2, g3, ticker);
        items[ticker.Symbol] = item;

        var row = Gtk.ListBoxRow.New();
        row.SetChild(item);
        SetupDragAndDrop(row);
        
        this.Append(row);
    }

    private void RemoveTicker(Ticker ticker)
    {
        if (GetListBoxRowOf(ticker) is Gtk.ListBoxRow row)
        {
            if (IsCollapsed)
                SelectRow(null);

            this.Remove(row);
        }
    }

    private void UpdateUItoMatchTickerOrderInModel()
    {
        for (int i = 0; i < model.Tickers.Count; i++)
        {
            var ticker = model.Tickers[i];

            if (GetListBoxRowOf(ticker) is not Gtk.ListBoxRow row)
                continue;

            if (row.GetIndex() == i)
                continue;

            Remove(row);
            Insert(row, i);
        }

        if (model.SelectedTicker is Ticker selectedTicker)
            SetSelectedRowTo(selectedTicker);
    }

    private Gtk.ListBoxRow? GetListBoxRowOf(Ticker ticker)
    {
        if (!items.TryGetValue(ticker.Symbol, out var item))
            return null;

        if (item.Parent is not Gtk.ListBoxRow row)
            return null;

        return row;
    }

    private void MoveDraggedTickerTo(int dropIndex)
    {
        if (dragState == null || dragState.Row.GetIndex() == dropIndex)
            return;

        Remove(dragState.Row);
        Insert(dragState.Row, dropIndex);
    }

    private void CleanupDragState()
    {
        if (dragState == null)
            return;

        dragState.Row.RemoveCssClass("drag-placeholder");
        dragState.Row.HeightRequest = -1;

        if (dragState.Row.Child == null)
            dragState.Row.SetChild(dragState.SidebarItem);

        dragState = null;
    }


    private record DragState(Gtk.ListBoxRow Row, SidebarItem SidebarItem)
    {
        public Ticker Ticker { get; } = SidebarItem.Ticker;
        public Gtk.Widget? DragIcon { get; set; }
    }
}
