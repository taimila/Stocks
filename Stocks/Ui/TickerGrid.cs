// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class TickerGrid : Gtk.FlowBox
{
    private readonly AppModel model;
    private readonly Dictionary<string, Gtk.AspectFrame> cards = [];

    // Container for all on-going animations during drag operation
    private readonly Dictionary<Gtk.FlowBoxChild, (Adw.TimedAnimation Animation, Adw.CallbackAnimationTarget Target)> activeCardAnimations = [];

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
            {
                StopCardAnimation(child);
                Remove(child);
            }
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

            StopAllCardAnimations();
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
            StopAllCardAnimations();
            CleanupDragState();
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
                MoveDraggedCardTo(targetChild.GetIndex());

            var targetIndex = dragState.FlowBoxChild.GetIndex();
            model.MoveTicker(ticker, targetIndex);
            return true;
        };

        dropTarget.OnEnter += (_, args) =>
        {
            if (dragState == null)
                return Gdk.DragAction.Move;

            if (card.Parent is not Gtk.FlowBoxChild targetChild)
                return Gdk.DragAction.Move;

            // Flying card must not act as drop target, it messes up animations
            if (activeCardAnimations.ContainsKey(targetChild))
                return Gdk.DragAction.Move;

            if (targetChild != dragState.FlowBoxChild)
                MoveDraggedCardTo(targetChild.GetIndex());

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

    private void MoveDraggedCardTo(int targetIndex)
    {
        if (dragState == null)
            return;

        int fromIndex = dragState.FlowBoxChild.GetIndex();
        if (fromIndex == targetIndex)
            return;

        var affectedChildren = new List<(Gtk.FlowBoxChild Child, int OldIndex)>();

        if (targetIndex < fromIndex)
        {
            for (int i = targetIndex; i < fromIndex; i++)
            {
                if (GetChildAtIndex(i) is Gtk.FlowBoxChild child)
                    affectedChildren.Add((child, i));
            }
        }
        else
        {
            for (int i = fromIndex + 1; i <= targetIndex; i++)
            {
                if (GetChildAtIndex(i) is Gtk.FlowBoxChild child)
                    affectedChildren.Add((child, i));
            }
        }

        // Capture current slot positions before cards are reordered
        var slotPositions = new Dictionary<int, (double X, double Y)>();
        int minIndex = Math.Min(targetIndex, fromIndex);
        int maxIndex = Math.Max(targetIndex, fromIndex);
        for (int i = minIndex; i <= maxIndex; i++)
        {
            if (GetChildAtIndex(i) is not Gtk.FlowBoxChild child)
                continue;

            if (!child.ComputeBounds(this, out Graphene.Rect bounds))
                continue;

            slotPositions[i] = (
                bounds.GetX() - child.MarginStart,
                bounds.GetY() - child.MarginTop);
        }

        Remove(dragState.FlowBoxChild);
        Insert(dragState.FlowBoxChild, targetIndex);

        // Go through all affected cards and start animations for them.
        foreach (var (child, oldIndex) in affectedChildren)
        {
            int targetSlotIndex = targetIndex < fromIndex
                ? oldIndex + 1
                : oldIndex - 1;

            if (!slotPositions.TryGetValue(oldIndex, out var oldPosition))
                continue;

            if (!slotPositions.TryGetValue(targetSlotIndex, out var targetPosition))
                continue;

            var startOffsetX = oldPosition.X - targetPosition.X;
            var startOffsetY = oldPosition.Y - targetPosition.Y;

            if (Math.Abs(startOffsetX) < 0.01 && Math.Abs(startOffsetY) < 0.01)
                continue;

            AnimateCardToTargetPosition(child, startOffsetX, startOffsetY);
        }
    }

    private void CleanupDragState()
    {
        if (dragState == null)
            return;

        SetCardOffset(dragState.FlowBoxChild, 0, 0);
        dragState.Card.RemoveCssClass("drag-placeholder");

        if (dragState.Card.Child == null && dragState.CardContent != null)
            dragState.Card.SetChild(dragState.CardContent);

        dragState = null;
    }

    private void AnimateCardToTargetPosition(Gtk.FlowBoxChild child, double startOffsetX, double startOffsetY)
    {
        StopCardAnimation(child, resetOffset: false);
        child.CanTarget = false;  // Without this moving card can block OnEnter of another sidebar item breaking the animation.
        SetCardOffset(child, startOffsetX, startOffsetY);

        var target = Adw.CallbackAnimationTarget.New(value => SetCardOffset(child, startOffsetX * value, startOffsetY * value));

        var animation = Adw.TimedAnimation.New(child, 1, 0, 300, target);
        animation.Easing = Adw.Easing.EaseOutCubic;
        animation.FollowEnableAnimationsSetting = true;
        animation.OnDone += (_, _) =>
        {
            if (!activeCardAnimations.TryGetValue(child, out var state) || state.Animation != animation)
                return;

            SetCardOffset(child, 0, 0);
            child.CanTarget = true;
            activeCardAnimations.Remove(child);
        };

        activeCardAnimations[child] = (animation, target);
        animation.Play();
    }

    private void StopCardAnimation(Gtk.FlowBoxChild child, bool resetOffset = true)
    {
        if (activeCardAnimations.Remove(child, out var animationState))
            animationState.Animation.Skip();

        child.CanTarget = true;

        if (resetOffset)
            SetCardOffset(child, 0, 0);
    }

    private void StopAllCardAnimations()
    {
        foreach (var child in activeCardAnimations.Keys.ToList())
            StopCardAnimation(child);
    }

    private static void SetCardOffset(Gtk.FlowBoxChild child, double x, double y)
    {
        int roundedX = (int)Math.Round(x);
        int roundedY = (int)Math.Round(y);

        child.MarginStart = roundedX;
        child.MarginEnd = -roundedX;
        child.MarginTop = roundedY;
        child.MarginBottom = -roundedY;
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
    
