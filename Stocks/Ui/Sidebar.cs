// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class Sidebar: Gtk.ListBox
{
    // Shadow ledger for items, because ListBox doesn't seem to have
    // API to access it's children directly. Order does not match
    // children order of the ListBox if user has dragged items!
    private readonly List<SidebarItem> items = [];
    
    private Gtk.ListBoxRow? draggedRow;
    private Gtk.ListBoxRow? highlightedRow;
    private readonly AppModel model;

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
            if (!IsCollapsed && args.Row?.Child is SidebarItem item)
            {
                model.SetActive(item.Ticker);
            }
        };

        this.model = model;
        this.model.Tickers.ForEach(Add);

        this.model.OnTickerAdded += AddTicker; 
        this.model.OnTickerRemoved += RemoveTicker; 
        
        if (GetRowAtIndex(0) is Gtk.ListBoxRow row)
        {
            SelectRow(row);
        }
    }

    private void AddTicker(Ticker ticker)
    {
        Add(ticker);
        if (!IsCollapsed && GetRowAtIndex(items.Count - 1) is Gtk.ListBoxRow row)
        {
            SelectRow(row);
        }
    }

    private void SetupDragAndDrop(Gtk.ListBoxRow row)
    {
        var dragSource = Gtk.DragSource.New();
        dragSource.SetActions(Gdk.DragAction.Move);
        dragSource.SetContent(Gdk.ContentProvider.NewForValue(new GObject.Value(row)));
        dragSource.OnDragBegin += (_, _) => 
        { 
            row.AddCssClass("drag-row");
            var paintable = Gtk.WidgetPaintable.New(row);
            dragSource.SetIcon(paintable, 0, 0);
            draggedRow = row; 
        };
        dragSource.OnDragEnd += (_, _) =>
        {
            row.RemoveCssClass("drag-row");
        };
        row.AddController(dragSource);

        var dropTarget = Gtk.DropTarget.New(GObject.Type.Object, Gdk.DragAction.Move);
        dropTarget.OnDrop += (_, _) =>
        {
            // Cancel if item is dropped on itself
            if (draggedRow == null || draggedRow == row)
            {
                return false;
            }
            else
            {
                int targetIndex = row.GetIndex();

                this.Remove(draggedRow);
                this.Insert(draggedRow, targetIndex);
                
                var ticker = (draggedRow.Child as SidebarItem)!.Ticker;
                model.MoveTicker(ticker, targetIndex);

                draggedRow = null;
                return true;
            }
        };

        dropTarget.OnMotion += (_, _) =>
        {
            if (draggedRow != null && row != highlightedRow)
            {
                DragHighlightRow(row);
                highlightedRow = row;
            }
                
            return Gdk.DragAction.Move;
        };

        dropTarget.OnLeave += (_, _) =>
        {
            DragUnhighlightRow();
            highlightedRow = null;
        };

        row.AddController(dropTarget);
    }

    private void Add(Ticker ticker)
    {
        var row = Gtk.ListBoxRow.New();

        SetupDragAndDrop(row);
        
        var item = new SidebarItem(g1, g2, g3, ticker, model);
        items.Add(item);
        row.SetChild(item);

        this.Append(row);
    }

    private void RemoveTicker(Ticker ticker)
    {
        var item = items.FirstOrDefault(i => i.Ticker.Symbol == ticker.Symbol);
        
        if (item?.Parent is Gtk.ListBoxRow row)
        {
            items.Remove(item);
            
            if (IsCollapsed)
            {
                SelectRow(null);
            }

            this.Remove(row);
        }
    }
}
