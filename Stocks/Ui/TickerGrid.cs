// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class TickerGrid : Gtk.FlowBox
{
    private readonly AppModel model;
    private readonly Dictionary<string, Gtk.AspectFrame> cards = [];

    private DragState? dragState;

    public TickerGrid(AppModel model)
    {
        this.model = model;
        this.model.Tickers.ForEach(AddTicker);
        this.model.OnTickerAdded += AddTicker;
        this.model.OnTickerRemoved += RemoveTicker;
        this.model.OnTickerMoved += (_, _) => UpdateUItoMatchTickerOrderInModel();

        SelectionMode = Gtk.SelectionMode.None;
        ActivateOnSingleClick = true;
        FocusOnClick = false;
        MinChildrenPerLine = 2;
        MaxChildrenPerLine = 100;
        RowSpacing = 8;
        ColumnSpacing = 8;
        MarginTop = 12;
        MarginBottom = 12;
        MarginStart = 12;
        MarginEnd = 12;
        Halign = Gtk.Align.Center;
        Valign = Gtk.Align.Start;
        Hexpand = true;
        Vexpand = true;
        Homogeneous = true;

        OnChildActivated += (_, args) =>
        {
            if (args.Child?.Child is Gtk.AspectFrame card && card.Child is TickerGridCard tgc)
            {
                this.model.SetActive(tgc.Ticker);
            }
        };        
    }

    private void AddTicker(Ticker ticker)
    {
        var card = CreateCard(ticker);
        SetupDragAndDrop(card);
        cards[ticker.Symbol] = card;
        Append(card);
    }

    private Gtk.AspectFrame CreateCard(Ticker ticker)
    {
        int cardSize = 180;

        var aspectFrame = Gtk.AspectFrame.New(0.5f, 0.5f, 1.0f, false);
        aspectFrame.WidthRequest = cardSize;
        aspectFrame.HeightRequest = cardSize;
        aspectFrame.SetChild(new TickerGridCard(ticker));

        return aspectFrame;
    }

    private void RemoveTicker(Ticker ticker)
    {
        if (cards.Remove(ticker.Symbol, out var card))
        {
            if (card.Parent is Gtk.FlowBoxChild child)
                Remove(child);
        }
    }

    private void SetupDragAndDrop(Gtk.AspectFrame card)
    {
        var dragSource = Gtk.DragSource.New();
        dragSource.SetActions(Gdk.DragAction.Move);
        dragSource.SetContent(Gdk.ContentProvider.NewForValue(new GObject.Value(card)));
        dragSource.OnDragBegin += (_, args) =>
        {
            if (card.Parent is not Gtk.FlowBoxChild child)
                return;

            dragState = new DragState(child, card);

            if (dragState.Ticker != null)
            {
                dragState.DragIconCard = CreateCard(dragState.Ticker);
                
                Gtk.DragIcon
                    .GetForDrag(args.Drag)
                    .SetChild(dragState.DragIconCard);
            }
            else
            {
                var paintable = Gtk.WidgetPaintable.New(card);
                dragSource.SetIcon(paintable, 0, 0);
            }

            card.SetChild(null);
            card.AddCssClass("drag-placeholder");
        };

        dragSource.OnDragEnd += (_, _) =>
        {
            CleanupDragState();
            UpdateUItoMatchTickerOrderInModel();
        };
        
        card.AddController(dragSource);

        var dropTarget = Gtk.DropTarget.New(GObject.Type.Object, Gdk.DragAction.Move);
        dropTarget.OnDrop += (_, _) =>
        {
            if (dragState?.Ticker is not Ticker ticker)
                return false;

            if (card.Parent is not Gtk.FlowBoxChild targetChild)
                return false;

            if (targetChild != dragState.FlowBoxChild)
                MoveDraggedTickerTo(targetChild.GetIndex());

            var targetIndex = dragState.FlowBoxChild.GetIndex();
            CleanupDragState();
            model.MoveTicker(ticker, targetIndex);
            return true;
        };

        dropTarget.OnMotion += (_, _) =>
        {
            if (dragState == null)
                return Gdk.DragAction.Move;

            if (card.Parent is not Gtk.FlowBoxChild targetChild)
                return Gdk.DragAction.Move;

            if (targetChild != dragState.FlowBoxChild)
                MoveDraggedTickerTo(targetChild.GetIndex());

            return Gdk.DragAction.Move;
        };

        card.AddController(dropTarget);
    }

    private void UpdateUItoMatchTickerOrderInModel()
    {
        for (int i = 0; i < model.Tickers.Count; i++)
        {
            var ticker = model.Tickers[i];

            if (GetFloxBoxChildOf(ticker) is not Gtk.FlowBoxChild child)
                continue;

            if (child.GetIndex() == i)
                continue;

            Remove(child);
            Insert(child, i);
        }
    }

    private Gtk.FlowBoxChild? GetFloxBoxChildOf(Ticker ticker)
    {
        if (!cards.TryGetValue(ticker.Symbol, out var item))
            return null;

        if (item.Parent is not Gtk.FlowBoxChild child)
            return null;

        return child;
    }

    private void MoveDraggedTickerTo(int dropIndex)
    {
        if (dragState == null || dragState.FlowBoxChild.GetIndex() == dropIndex)
            return;

        Remove(dragState.FlowBoxChild);
        Insert(dragState.FlowBoxChild, dropIndex);
    }

    private void CleanupDragState()
    {
        if (dragState == null)
            return;

        dragState.Card.RemoveCssClass("drag-placeholder");

        if (dragState.Card.Child == null && dragState.CardContent != null)
            dragState.Card.SetChild(dragState.CardContent);

        dragState = null;
    }
}

class DragState(Gtk.FlowBoxChild fbc, Gtk.AspectFrame card)
{
    public Gtk.FlowBoxChild FlowBoxChild { get; } = fbc;
    public Gtk.AspectFrame Card { get; } = card;
    public Gtk.Widget? CardContent { get; } = card.Child;
    public Gtk.AspectFrame? DragIconCard { get; set; }
    public Ticker? Ticker => (CardContent as TickerGridCard)?.Ticker;
}
    