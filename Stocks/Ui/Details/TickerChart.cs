// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;
using Cairo;

namespace Stocks.UI;

public enum HoverMode
{
    PopoverOnBottom,
    PopoverOnDataPoint,
    Minimal
}

public record Color(
    double R, 
    double G, 
    double B, 
    double A = 1.0)
{
    public Color WithAlpha(double alpha) => new(R, G, B, alpha);
};

record DragRange(
    double MinX, 
    double MaxX, 
    int MinIndex, 
    int MaxIndex, 
    Color Color
);

record Point(
    double X, 
    double Y
);

record ValueRange(
    double Min,
    double Max
) {
    // Cap range to to prevent divisions by zero with weird data
    public double Range => Math.Max(0.0000001, Max - Min); 
};

public class ChartPalette {
    public readonly Color positive = new(0.2,0.8196,0.4784,1);
    public readonly Color negative = new(0.8784,0.1059,0.1412,1);
    public readonly Color muted = new(0.5, 0.5, 0.5, 0.25);

    public Color scale => Gtk.Settings.GetDefault()!.GtkApplicationPreferDarkTheme 
            ? new Color(1,1,1,1) 
            : new Color(0,0,0,1);
}

public class TickerChart: Gtk.DrawingArea
{
    // Configuration
    public bool EnableMouseInteraction { get; set; } = true;
    public bool ShowDotOnHover { get; set; } = true;
    public bool ShowPreviousCloseLine { get; set; } = true;
    public bool ShowGradient { get; set; } = true;
    public bool ShowXScale { get; set; } = true;
    public bool ShowYScale { get; set; } = true;
    public double LineWidth { get; set; } = 2;
    public double CloseMarkerWidth  { get; set; } = 1.5;
    public double Padding { get; set; } = 0.1;
    public HoverMode HoverMode { get; set; } = HoverMode.PopoverOnBottom;
    public bool HideCursorOnHover { get; set; } = false;

    // Day chart has very few data points when market opens. These values control
    // condensing the graph not to take the whole X-axis, but be drawn on the left.
    private const int DayCompressedPointThreshold = 120;
    private static readonly TimeSpan DayCompressedVirtualSpan = TimeSpan.FromHours(6);

    private bool UseCompressedDayScale =>
            data != null &&
            data.Range == TickerRange.Day &&
            data.DataPoints.Length < DayCompressedPointThreshold;

    // Data the chart is displaying
    private TickerData? data;

    // Cursor state
    private bool isDragging = false;
    private Point dragStarted = new(0,0);
    private Point cursor = new(0,0);
    private bool hover = false;
    private bool IsHovering 
    {
        get => hover;
        set
        {
            hover = value;
            if (HideCursorOnHover && value)
                HideCursor();
            else
                ShowCursor();
        }
    }

    private readonly ChartPalette palette = new();

    // Chart size (can be subset of the canvas or full canvas)
    private int chartWidth = 0;
    private int chartHeight = 0;
    private double dataWidth = 0;
    private double interactiveWidth = 0;
    private double HoverWidth => interactiveWidth > 0 ? interactiveWidth : chartWidth;

    // Popover showing hover data of the current data point.
    private TickerChartPopover popover;

    // Fired on user interaction (on hover and drag operation)
    // first data point, second data point (if clicked and dragging) and isPopover used
    public event Action<DataPoint?, DataPoint?, bool> OnHover;
    private void NotifyHover(DataPoint? d1, DataPoint? d2)
    {
        OnHover?.Invoke(d1, d2, HoverMode == HoverMode.PopoverOnBottom);
    }

    public TickerChart()
    {
        SetDrawFunc(Draw);
        TrackMouse();
        SetupPopover();
    }

    public void Set(TickerData data, bool showPreviousCloseLine)
    {
        this.data = data;
        ShowPreviousCloseLine = showPreviousCloseLine;
        QueueDraw();
    }
    
    public void Clear()
    {
        data = null;
        QueueDraw();
    }

    private bool IsInHoverInteractionArea(double x, double y) =>
            x >= 0 && x <= HoverWidth && y >= 0 && y <= chartHeight;

    // Update chart state based on mouse actions (position and click)
    private void TrackMouse()
    {
        var motion = new Gtk.EventControllerMotion();

        motion.OnMotion += (o, args) =>
        {
            if (!EnableMouseInteraction) return;

            cursor = new(args.X, args.Y);
            IsHovering = isDragging || IsInHoverInteractionArea(args.X, args.Y);

            if (!IsHovering)
            {
                HideHoverPopover();
                NotifyHover(null, null);
                QueueDraw();
                return;
            }

            if (isDragging)
            {
                var data = GetDataPoint((int)args.X);
                var data2 = GetDataPoint((int)dragStarted.X);
                UpdateRangePopover();
                NotifyHover(data, data2);
            }
            else
            {
                var data = GetDataPoint((int)args.X);
                UpdatePopover(data, args.X);
                NotifyHover(data, null);
            }

            QueueDraw();
        };
        
        motion.OnEnter += (o, args) =>
        {
            if (!EnableMouseInteraction) return;
            IsHovering = IsInHoverInteractionArea(args.X, args.Y);
            QueueDraw();
        };

        motion.OnLeave += (o, args) =>
        {
            if (!EnableMouseInteraction) return;
            if (isDragging) return;
            IsHovering = false;
            HideHoverPopover();
            QueueDraw();
            NotifyHover(null, null);
        };

        AddController(motion);

        var click = Gtk.GestureClick.New();
        
        click.OnPressed += (_, a) => 
        { 
            if (!EnableMouseInteraction) return;
            isDragging = true;    
            dragStarted = new(a.X, a.Y);
            cursor = new(a.X, a.Y);
            IsHovering = IsInHoverInteractionArea(a.X, a.Y);
            UpdateRangePopover();
        };

        click.OnReleased += (_, a) => 
        { 
            if (!EnableMouseInteraction) return;
            isDragging = false;
            cursor = new(a.X, a.Y);
            IsHovering = IsInHoverInteractionArea(a.X, a.Y);
            QueueDraw();
            NotifyHover(IsHovering ? GetDataPoint((int)cursor.X) : null, null);
            
            if (IsHovering)
            {
                // Dragging was released and mouse is still on chart, we keep showing popover for one data point
                UpdatePopover(GetDataPoint((int)cursor.X), cursor.X);
                NotifyHover(GetDataPoint((int)cursor.X), null);
            }
            else
            {
                // Dragging was released and mouse is not on chart
                HideHoverPopover();
                NotifyHover(null, null);
            }
        }; 

        AddController(click);
    }

    void HideCursor() => (Root as Gtk.Window)?.SetCursorFromName("none");
    void ShowCursor() => (Root as Gtk.Window)?.SetCursorFromName(null);

    #region Popover

    private void SetupPopover()
    {
        popover = new TickerChartPopover();
        popover.SetParent(this);
    }

    private void HideHoverPopover()
    {
        popover?.Hide();
    }

    private void UpdatePopover(DataPoint? dp, double x)
    {
        if (HoverMode == HoverMode.Minimal)
            return;

        if (!IsHovering || data == null || dp == null ||  data.DataPoints.Length == 0)
        {
            HideHoverPopover();
            return;
        }

        var point = new Gdk.Rectangle
        {
            X = (int)Math.Clamp(x, 0, chartWidth),
            Y = chartHeight + 3,
            Width = 1,
            Height = 1
        };

        IPercentageChange percentage = data.Range.IsShort()
            ? new ChangeFromPreviousClose(dp.Close, data.PreviousClose)
            : new ChangeBetweenTwoPrices(data.DataPoints.First().Close, dp.Close);

        popover.SetValues(data.Range, GetTimestamp(dp), data.MarketPrice.Format(dp.Close), percentage);
        popover.SetPointingTo(point);
        popover.Show();
    }

    private void UpdateRangePopover()
    {
        if (HoverMode == HoverMode.Minimal)
            return;

        if (!IsHovering || data == null || data.DataPoints.Length < 2)
        {
            HideHoverPopover();
            return;
        }

        double dpWidth = GetDataPointWidth();
        if (dpWidth <= 0)
        {
            HideHoverPopover();
            return;
        }

        var maxX = (double)chartWidth;
        if (UseCompressedDayScale)
        {
            var computedWidth = GetDataWidth(dpWidth);
            maxX = Math.Min(chartWidth, computedWidth);
        }

        var dragRange = CreateDragRange(dpWidth, maxX);

        if (dragRange == null)
        {
            HideHoverPopover();
            return;
        }

        var start = data.DataPoints[dragRange.MinIndex];
        var end = data.DataPoints[dragRange.MaxIndex];
        var rangeLabel = GetTimestamp(start) + " \u2013 " + GetTimestamp(end); // Use en dash as recommended by Gnome HIG
        var percentage = new ChangeBetweenTwoPrices(start.Close, end.Close);

        var centerX = (dragRange.MinX + dragRange.MaxX) / 2.0;
        var point = new Gdk.Rectangle
        {
            X = (int)Math.Clamp(centerX, 0, chartWidth),
            Y = chartHeight + 3 + 12, // 12 compensates removal of arrow in popover.
            Width = 1,
            Height = 1
        };

        popover.SetRangeValues(rangeLabel, percentage);
        popover.SetPointingTo(point);
        popover.Show();
    }

    #endregion

    
    // Calculate how many pixels each data point should use on X axis. For most
    // cases this is simply chart width / number of data points. However, day
    // chart is drawn with partial width when market opens to improve readability.
    // In that case we need to do bit more math to figure out the correct width.
    private double GetDataPointWidth()
    {
        if (data == null || data.DataPoints.Length < 2 || chartWidth <= 0)
            return 0;

        double dpWidth = ((double)chartWidth) / ((double)(data.DataPoints.Length - 1));

        if (!UseCompressedDayScale)
            return dpWidth;

        var minTime = data.DataPoints.First().Timestamp;
        var maxTime = data.DataPoints.Last().Timestamp;
        var span = maxTime - minTime;

        if (span <= TimeSpan.Zero || span >= DayCompressedVirtualSpan)
            return dpWidth;

        var scale = span.TotalSeconds / DayCompressedVirtualSpan.TotalSeconds;
        return dpWidth * scale;
    }

    private double GetDataWidth(double dpWidth)
    {
        if (data == null || data.DataPoints.Length < 2 || dpWidth <= 0)
            return 0;

        return dpWidth * (data.DataPoints.Length - 1);
    }

    // Returns index that can be used to get data point from data[] for given X coordinate.
    private int GetIndexAtX(double x)
    {
        if (data == null || data.DataPoints.Length == 0)
            return -1;

        // If there is only one data point, always return index 0 no matter what X value is request for.
        if (data.DataPoints.Length == 1)
            return 0;

        double dpWidth = GetDataPointWidth();
        if (dpWidth <= 0)
            return -1;

        var index = (int)Math.Round(x / dpWidth);
        return Math.Max(0, Math.Min(index, data.DataPoints.Length - 1));
    }

    private DataPoint? GetDataPoint(int x)
    {
        if (data == null) 
            return null;
        
        int index = GetIndexAtX(x);
        return index >= 0 ? data.DataPoints[index] : null;
    }

    // Defines a range for Y scale of the chart based on data.
    private ValueRange CreateValueRange()
    {
        double min = 0;
        double max = 0;

        if (ShowPreviousCloseLine)
        {
            min = Math.Min(data.DataPoints.Min(x => x.Close), data.PreviousClose);
            max = Math.Max(data.DataPoints.Max(x => x.Close), data.PreviousClose);
        } 
        else
        {
            min = data.DataPoints.Min(x => x.Close);
            max = data.DataPoints.Max(x => x.Close);    
        }

        return new(min, max);
    }

    // Defines a current user selected range. (Click and hover action)
    DragRange? CreateDragRange(double dpWidth, double limitX) 
    {
        if (!isDragging) return null;
        if (dpWidth <= 0) return null;

        var minLimit = LineWidth / 2;
        var maxLimit = Math.Max(minLimit, limitX);
        var startX = Math.Clamp(dragStarted.X, minLimit, maxLimit);
        var currentX = Math.Clamp(cursor.X, minLimit, maxLimit);
        double minX = Math.Min(startX, currentX);
        double maxX = Math.Max(startX, currentX);

        var minIndex = (int)Math.Round(minX / dpWidth);
        minIndex = Math.Max(0, Math.Min(minIndex, data.DataPoints.Length - 1));
        
        var maxIndex = (int)Math.Round(maxX / dpWidth);
        maxIndex = Math.Max(0, Math.Min(maxIndex, data.DataPoints.Length - 1));
        
        var rangeColor = data.DataPoints[minIndex].Close <= data.DataPoints[maxIndex].Close ? palette.positive : palette.negative;

        return new(minX, maxX, minIndex, maxIndex, rangeColor);
    }

    void Draw(Gtk.DrawingArea area, Context ctx, int w, int h)
    {
        // Do not draw anything without data or with just one data point.
        if (data == null || data.DataPoints.Length < 2)
            return;

        // Reserve space for scales at bottom and right if they are enabled
        chartWidth = ShowYScale ? w - 30 : w;
        chartHeight = ShowXScale ? h - 24 : h;

        var range = CreateValueRange();

        // Defines the width of one data value when drawing on X axis.
        double dpWidth = GetDataPointWidth();

        if (dpWidth <= 0)
            return;

        dataWidth = GetDataWidth(dpWidth);
        interactiveWidth = UseCompressedDayScale ? Math.Min(chartWidth, dataWidth) : chartWidth;

        var drag = CreateDragRange(dpWidth, interactiveWidth);

        // Helper function to get chart Y value for given data value. (maps data value to drawing value)
        double GetY(double val) => ((double)chartHeight) - ((val - range.Min) / range.Range * ((double)chartHeight));

        // Draw scales
        if (ShowYScale) DrawYScale(ctx, range, w, chartHeight);
        if (ShowXScale) DrawXScale(ctx); 

        /// Define the color to use when drawing non-muted parts of the graph
        var color = drag?.Color ?? (data!.IsPositive ? palette.positive : palette.negative);

        if (ShowPreviousCloseLine && !isDragging)
        {
            DrawPreviousCloseMarker(ctx, chartWidth, GetY(data.PreviousClose), color);
        }

        void DrawGraphBetween(int startIndex, int endIndex, Color color)
        {
            var startX = startIndex * dpWidth;
            var startY = GetY(data.DataPoints[startIndex].Close);

            ctx.NewPath();
            ctx.MoveTo(startX, startY);

            double x = 0;

            for (int i = startIndex; i < endIndex; i++)
            {
                x = i * dpWidth;
                var y = GetY(data.DataPoints[i].Close);
                ctx.LineTo(x, y);
            }

            // Draw chart line
            ctx.SetColor(color);
            ctx.LineJoin = LineJoin.Round;
            ctx.LineWidth = LineWidth;
            ctx.StrokePreserve();
            
            if (ShowGradient) {
                // Draw gradient below line
                ctx.LineTo(x, chartHeight);
                ctx.LineTo(startX, chartHeight);
                ctx.ClosePath();
                FillWithGradient(ctx, chartHeight, color, color.A == 0.25 ? 0.15 : 0.4); //TODO: Refactor and clean up how gradient is managed.
            }
        }

        if(drag is DragRange dragRange)
        {
            DrawGraphBetween(0, dragRange.MinIndex, palette.muted);
            DrawGraphBetween(dragRange.MinIndex, dragRange.MaxIndex + 1, color);
            DrawGraphBetween(dragRange.MaxIndex, data.DataPoints.Length, palette.muted);
            DrawDivider(ctx, chartWidth, chartHeight, dragRange.MinX, GetY(data.DataPoints[dragRange.MinIndex].Close), data.DataPoints[dragRange.MinIndex], color);
            DrawDivider(ctx, chartWidth, chartHeight, dragRange.MaxX, GetY(data.DataPoints[dragRange.MaxIndex].Close), data.DataPoints[dragRange.MaxIndex], color);
        }
        else if(IsHovering) 
        {
            var hoverIndex = GetIndexAtX(cursor.X);            
            DrawGraphBetween(0, hoverIndex + 1, color);
            DrawGraphBetween(hoverIndex, data.DataPoints.Length, palette.muted);
            DrawDivider(ctx, chartWidth, chartHeight, cursor.X, GetY(data.DataPoints[hoverIndex].Close), data.DataPoints[hoverIndex], color);
        }
        else
        {
            DrawGraphBetween(0, data.DataPoints.Length, color);
        }

        //ctx.DrawDebugBorders(w, h);
    }

    void FillWithGradient(Context ctx, int h, Color c, double maxAlpha)
    {
        using var gradient = new LinearGradient(0, 0, 0, h);
        gradient.AddColorStopRgba(0, c.R, c.G, c.B, maxAlpha);
        gradient.AddColorStopRgba(1, c.R, c.G, c.B, 0.0);
        ctx.SetSource(gradient);
        ctx.Fill();
    }

    void DrawPreviousCloseMarker(Context ctx, int w, double y, Color color)
    {
        // Ensure that line is fully visible in the edge case.
        if (y == 0) y = CloseMarkerWidth / 2;

        ctx.Save();
        ctx.LineWidth = CloseMarkerWidth;
        ctx.SetColor(color);
        ctx.SetDash([LineWidth * 2, LineWidth * 2], 0);
        ctx.MoveTo(0, y);
        ctx.LineTo(w, y);
        ctx.Stroke();
        ctx.Restore();
    }

    private readonly int scaleYCount = 4;
    private readonly int scaleXCount = 5;
    private readonly double scaleAlpha = 0.07;
    private readonly double scaleTextAlpha = 0.5;

    void DrawYScale(Context ctx, ValueRange range, int w, int h)
    {
        int ySteps = scaleYCount - 1;
        var yScaleStep = (h / range.Range) * (range.Range / ySteps);

        for (int i = 0; i <= ySteps; i++)
        {
            var lineY = i * yScaleStep;
            ctx.LineWidth = 1;
            ctx.SetColor(palette.scale.WithAlpha(scaleAlpha));
            ctx.MoveTo(0, lineY);
            ctx.LineTo(w, lineY);
            ctx.Stroke();
            
            var value = range.Max + i * (range.Min - range.Max) / ySteps;
            var roundedValue = value < 1 ? Math.Round(value, 4).ToString() : value.ToString("F0");

            ctx.SetColor(palette.scale.WithAlpha(scaleTextAlpha));
            ctx.SetFontSize(13);
            ctx.TextExtents(roundedValue, out TextExtents te);
            ctx.MoveTo(w - te.Width - 2, lineY + te.Height + 3);
            ctx.ShowText(roundedValue);
        }
    }

    void DrawXScale(Context ctx)
    {
        if (data == null || data.DataPoints.Length < 2)
            return;

        var minTime = data.DataPoints.First().Timestamp.ToLocalTime();
        var maxTime = data.DataPoints.Last().Timestamp.ToLocalTime();
        var timeSpan = maxTime - minTime;
        var scaleWidth = UseCompressedDayScale ? dataWidth : chartWidth;

        // Only draw X Scale if there is time scale available.
        if (timeSpan.TotalSeconds <= 0)
            return;

        var markers = data.GetMarkers(scaleXCount);

        foreach (var marker in markers)
        {
            // Calculate X position (linear interpolation)
            var fraction = (marker.Timestamp - minTime).TotalSeconds / timeSpan.TotalSeconds;
            var x = fraction * scaleWidth;

            // Do not draw markers that would not be fully visible
            if (x < 0)
                continue;

            // Draw vertical line
            ctx.LineWidth = 1;
            ctx.SetColor(palette.scale.WithAlpha(scaleAlpha));
            ctx.MoveTo(x, 0);
            ctx.LineTo(x, chartHeight);
            ctx.Stroke();

            ctx.SetColor(palette.scale.WithAlpha(scaleTextAlpha));
            ctx.SetFontSize(13);
            ctx.TextExtents(marker.Label, out TextExtents te);
            ctx.MoveTo(Math.Max(0, x - te.Width / 2), chartHeight + 12);
            ctx.ShowText(marker.Label);
        }        
    }

    void DrawOnHoverDot(Context ctx, double y, double x, Color color)
    {
        ctx.SetColor(new Color(1,1,1,1));
        double r2 = LineWidth * 4;
        ctx.Arc(x, y, r2, 0, 2 * Math.PI);
        ctx.Fill();

        ctx.SetColor(color);
        double r = LineWidth * 3;
        ctx.Arc(x, y, r, 0, 2 * Math.PI);
        ctx.Fill();
    }   

    void DrawDivider(Context ctx, int w, int h, double x, double y, DataPoint dp, Color color)
    {
        ctx.MoveTo(x, 0);
        ctx.LineTo(x, h);
        ctx.SetColor(color);
        ctx.LineWidth = LineWidth;
        ctx.LineCap = LineCap.Square;
        ctx.Stroke();

        if (HoverMode == HoverMode.Minimal)
        {
            var date = GetTimestamp(dp);

            ctx.SelectFontFace("Adwaita Sans", FontSlant.Normal, FontWeight.Bold);
            ctx.SetFontSize(12);
            ctx.TextExtents(date, out TextExtents te);

            var pillPadding = 3;
            var pillX = Math.Min(Math.Max(10, x - (te.Width / 2)), w - te.Width - 5);
            ctx.DrawPill(pillX - (2 * pillPadding), h, te.Width +  (4 * pillPadding), te.Height + (2 * pillPadding));
            ctx.SetColor(color);
            ctx.Fill();

            ctx.SetColor(new Color(1,1,1,1));
            ctx.MoveTo(pillX, h + 13);
            ctx.ShowText(date);
            ctx.SelectFontFace("Adwaita Sans", FontSlant.Normal, FontWeight.Normal);
        }

        if (ShowDotOnHover)
        {
            DrawOnHoverDot(ctx, y, x, color);
        }
    }

    string GetTimestamp(DataPoint dp)
    {
        var ts = dp.Timestamp.ToLocalTime();

        if (data!.Range.IsShort())
        {
            if (isDragging)
                return ts.ToShortTimeString();
            else
                return ts.ToShortDateString() + " " + ts.ToShortTimeString();
        } 
        else
            return ts.ToShortDateString();
    }
}

public static class ContextExtensions
{
    public static void SetColor(this Context c, Color color)
    {
        c.SetSourceRgba(color.R, color.G, color.B, color.A);
    }

    public static void DrawPill(this Context ctx, double x, double y, double width, double height)
    {
        double radius = height / 2.0;
        ctx.NewPath();
        ctx.Arc(x + radius, y + radius, radius, Math.PI / 2, Math.PI * 3 / 2);
        ctx.LineTo(x + width - radius, y);
        ctx.Arc(x + width - radius, y + radius, radius, Math.PI * 3 / 2, Math.PI / 2);
        ctx.ClosePath();
    }

    public static void DrawDebugBorders(this Context ctx, int w, int h)
    {
        ctx.NewPath();
        ctx.MoveTo(0,0);
        ctx.LineTo(w,0);
        ctx.LineTo(w,h);
        ctx.LineTo(0,h);
        ctx.ClosePath();
        ctx.SetColor(new Color(1,0,0,1));
        ctx.LineWidth = 1;
        ctx.Stroke();
    }
}
