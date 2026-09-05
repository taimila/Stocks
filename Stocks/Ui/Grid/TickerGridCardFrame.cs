// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

[GObject.Subclass<Gtk.Widget>(qualifiedName: nameof(TickerGridCardFrame))]
public partial class TickerGridCardFrame
{
    // This container "frame" enforces ticker card size in the grid
    // to be between min max values defined here.
    private const int MinCardSize = 155;
    private const int MaxCardSize = 180;

    private Gtk.AspectFrame aspectFrame = null!;

    // Prevent too early garbage collection by holding a reference.
    private Gtk.Internal.CustomRequestModeFuncCallHandler requestModeHandler = null!;
    private Gtk.Internal.CustomMeasureFuncCallHandler measureHandler = null!;
    private Gtk.Internal.CustomAllocateFuncCallHandler allocateHandler = null!;

    public static TickerGridCardFrame NewWithTicker(Ticker ticker)
    {
        var frame = NewWithProperties([]);
        frame.SetupWidget(ticker);
        return frame;
    }

    private void SetupWidget(Ticker ticker)
    {
        requestModeHandler = new(RequestMode);
        measureHandler = new(Measure);
        allocateHandler = new(Allocate);

        var layout = Gtk.Internal.CustomLayout.New(
            requestModeHandler.NativeCallback,
            measureHandler.NativeCallback,
            allocateHandler.NativeCallback);

        LayoutManager = Gtk.CustomLayout.NewFromPointer(layout, ownsHandle: true);

        aspectFrame = Gtk.AspectFrame.New(0.5f, 0.5f, 1.0f, false);
        aspectFrame.SetChild(TickerGridCard.NewWithTicker(ticker));
        aspectFrame.SetParent(this);
    }

    public Ticker? Ticker => (aspectFrame.Child as TickerGridCard)?.Ticker;
    public Gtk.Widget? Content => aspectFrame.Child;
    public bool HasContent => aspectFrame.Child is not null;

    public void SetContent(Gtk.Widget content)
    {
        aspectFrame.SetChild(content);
    }

    public void RemoveContent()
    {
        aspectFrame.SetChild(null);
    }

    public void AddPlaceholderClass()
    {
        aspectFrame.AddCssClass("drag-placeholder");
    }

    public void RemovePlaceholderClass()
    {
        aspectFrame.RemoveCssClass("drag-placeholder");
    }

    public void AddCardController(Gtk.EventController controller)
    {
        aspectFrame.AddController(controller);
    }

    public override void Dispose()
    {
        if (aspectFrame is not null && aspectFrame.Parent is not null)
            aspectFrame.Unparent();

        base.Dispose();
    }

    private static Gtk.SizeRequestMode RequestMode(Gtk.Widget widget)
    {
        return Gtk.SizeRequestMode.HeightForWidth;
    }

    private static void Measure(
        Gtk.Widget widget,
        Gtk.Orientation orientation,
        int forSize,
        out int minimum,
        out int natural,
        out int minimumBaseline,
        out int naturalBaseline)
    {
        minimumBaseline = -1;
        naturalBaseline = -1;

        if (orientation == Gtk.Orientation.Horizontal || forSize < 0)
        {
            minimum = MinCardSize;
            natural = MaxCardSize;
            return;
        }

        var size = Math.Clamp(forSize, MinCardSize, MaxCardSize);
        minimum = size;
        natural = size;
    }

    private static void Allocate(Gtk.Widget widget, int width, int height, int baseline)
    {
        if (widget.GetFirstChild() is not Gtk.Widget child)
            return;

        var size = Math.Min(Math.Min(width, height), MaxCardSize);
        child.Allocate(size, size, baseline, null!);
    }
}
    
