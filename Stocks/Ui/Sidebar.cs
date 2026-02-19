// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class Sidebar: Gtk.ListBox
{
    private readonly AppModel model;
    private readonly Dictionary<string, SidebarItem> items = [];

    // Container for all on-going animations during drag operation
    private readonly Dictionary<Gtk.ListBoxRow, (Adw.TimedAnimation Animation, Adw.CallbackAnimationTarget Target)> activeRowAnimations = [];
    
    // Contains information during drag operation for reordering sidebar items.
    private DragState? dragState;

    // Suppress row selection when it's not done by the user.
    private bool suppressRowSelection = false;

    private bool isCollapsed = false;

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
            if (isCollapsed && args.Row?.Child is SidebarItem item)
            {
                model.SetActive(item.Ticker);
            }
        };

        // When in full width mode, we want to navigate on selection
        OnRowSelected += (_, args) =>
        {
            if (suppressRowSelection)
                return;

            if (!isCollapsed && args.Row?.Child is SidebarItem item)
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

    // Hosting SplitView uses this to communicate it's state to the sidebar.
    public void SetLayoutState(bool collapsed)
    {
        if (isCollapsed == collapsed)
            return;

        isCollapsed = collapsed;
        UpdateSelectionState();
    }

    private void AddTicker(Ticker ticker)
    {
        Add(ticker);

        // In wide layout we show newly added ticker immediately.
        if (!isCollapsed && GetRowAtIndex(model.Tickers.Count - 1) is Gtk.ListBoxRow row)
        {
            SelectRow(row);
        }
    }

    private void SetSelectedRowTo(Ticker ticker)
    {
        if (isCollapsed)
            return;

        if (GetListBoxRowOf(ticker) is not Gtk.ListBoxRow row)
            return;

        suppressRowSelection = true;
        SelectRow(row);
        suppressRowSelection = false;
    }

    private void UpdateSelectionState()
    {
        suppressRowSelection = true;

        if (isCollapsed)
        {
            SelectionMode = Gtk.SelectionMode.None;
            SelectRow(null);
        }
        else
        {
            SelectionMode = Gtk.SelectionMode.Single;

            if (model.SelectedTicker is Ticker selectedTicker && GetListBoxRowOf(selectedTicker) is Gtk.ListBoxRow selectedRow)
            {
                SelectRow(selectedRow);
            }
            else if (GetRowAtIndex(0) is Gtk.ListBoxRow firstRow)
            {
                SelectRow(firstRow);
            }
        }

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

            StopAllRowAnimations();

            var previewWidth = 290; //TODO: Does not work correctly when large fonts accessibility feature is enabled.
            var previewHeight = row.GetHeight();
            var preview = CreatePreviewFor(item.Ticker, previewWidth, previewHeight);

            Gtk.DragIcon
                .GetForDrag(args.Drag)
                .SetChild(preview);

            dragState = new DragState(row, item, previewHeight);            
            
            row.HeightRequest = previewHeight;
            row.SetChild(null);
            row.AddCssClass("drag-placeholder");
        };

        dragSource.OnDragEnd += (_, _) =>
        {
            StopAllRowAnimations();
            CleanupDragState();
        };

        row.AddController(dragSource);

        var dropTarget = Gtk.DropTarget.New(GObject.Type.Object, Gdk.DragAction.Move);
        dropTarget.OnDrop += (_, _) =>
        {
            if (dragState?.Ticker is not Ticker ticker)
                return false;

            if (row != dragState.Row)
                MoveDraggedRowTo(row.GetIndex());

            int targetIndex = dragState.Row.GetIndex();
            model.MoveTicker(ticker, targetIndex);
            return true;
        };

        dropTarget.OnEnter += (_, _) =>
        {
            if (dragState == null)
                return Gdk.DragAction.Move;

            // Flying row must not act as drop target, it can mask rows below.
            if (activeRowAnimations.ContainsKey(row))
                return Gdk.DragAction.Move;

            if (row != dragState.Row)
                MoveDraggedRowTo(row.GetIndex());

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
            if (isCollapsed)
                SelectRow(null);

            StopRowAnimation(row);
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

    private void MoveDraggedRowTo(int targetIndex)
    {
        if (dragState == null)
            return;

        int fromIndex = dragState.Row.GetIndex();
        if (fromIndex == targetIndex)
            return;

        var affectedRows = new List<Gtk.ListBoxRow>();

        if (targetIndex < fromIndex)
        {
            for (int i = targetIndex; i < fromIndex; i++)
            {
                if (GetRowAtIndex(i) is Gtk.ListBoxRow row)
                    affectedRows.Add(row);
            }
        }
        else
        {
            for (int i = fromIndex + 1; i <= targetIndex; i++)
            {
                if (GetRowAtIndex(i) is Gtk.ListBoxRow row)
                    affectedRows.Add(row);
            }
        }

        Remove(dragState.Row);
        Insert(dragState.Row, targetIndex);

        var startOffset = targetIndex < fromIndex
            ? -dragState.PlaceholderHeight
            : dragState.PlaceholderHeight;

        foreach (var row in affectedRows)
            AnimateRowToTargetPosition(row, startOffset);
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

    private void AnimateRowToTargetPosition(Gtk.ListBoxRow row, double startOffset)
    {
        StopRowAnimation(row, resetOffset: false);
        row.CanTarget = false; // Without this moving sidebar item can block OnEnter of antoher sidebar item breaking the animation.
        SetRowVerticalOffset(row, startOffset);

        var target = Adw.CallbackAnimationTarget.New(value => SetRowVerticalOffset(row, value));
        var animation = Adw.TimedAnimation.New(row, startOffset, 0, 300, target);
        animation.Easing = Adw.Easing.EaseOutCubic;
        animation.FollowEnableAnimationsSetting = true;
        animation.OnDone += (_, _) =>
        {
            if (!activeRowAnimations.TryGetValue(row, out var state) || state.Animation != animation)
                return;

            SetRowVerticalOffset(row, 0);
            row.CanTarget = true;
            activeRowAnimations.Remove(row);
        };

        activeRowAnimations[row] = (animation, target);
        animation.Play();
    }

    private void StopRowAnimation(Gtk.ListBoxRow row, bool resetOffset = true)
    {
        if (activeRowAnimations.Remove(row, out var animationState))
            animationState.Animation.Skip();

        row.CanTarget = true;

        if (resetOffset)
            SetRowVerticalOffset(row, 0);
    }

    private void StopAllRowAnimations()
    {
        foreach (var row in activeRowAnimations.Keys.ToList())
            StopRowAnimation(row);
    }

    private static void SetRowVerticalOffset(Gtk.ListBoxRow row, double offset)
    {
        int roundedOffset = (int)Math.Round(offset);
        row.MarginTop = roundedOffset;
        row.MarginBottom = -roundedOffset;
    }

    private record DragState(Gtk.ListBoxRow Row, SidebarItem SidebarItem, int PlaceholderHeight)
    {
        public Ticker Ticker { get; } = SidebarItem.Ticker;
        public Gtk.Widget? DragIcon { get; set; }
    }
}
