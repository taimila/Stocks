// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class TickerChartPopover: Gtk.Popover
{
    private readonly Gtk.Label dateTitle;
    private readonly Gtk.Label dateValue;
    private readonly Gtk.Label priceTitle;
    private readonly Gtk.Label priceValue;
    private readonly Gtk.Label changeTitle;
    private readonly Gtk.Label changeValue;

    public TickerChartPopover()
    {
        Autohide = false;
        HasArrow = true;
        Position = Gtk.PositionType.Bottom;

        var container = Gtk.Box.New(Gtk.Orientation.Vertical, 6);
        container.MarginStart = 6;
        container.MarginEnd = 6;
        container.MarginTop = 4;
        container.MarginBottom = 4;

        var grid = Gtk.Grid.New();
        grid.ColumnSpacing = 12;
        grid.RowSpacing = 4;

        dateTitle = Gtk.Label.New(_("Date"));
        dateTitle.AddCssClass("caption");
        dateTitle.AddCssClass("dim-label");
        dateTitle.Halign = Gtk.Align.Start;
        dateTitle.Xalign = 0;

        dateValue = Gtk.Label.New("");
        dateValue.AddCssClass("caption-heading");
        dateValue.Halign = Gtk.Align.End;
        dateValue.Xalign = 1;

        priceTitle = Gtk.Label.New(_("Price"));
        priceTitle.AddCssClass("caption");
        priceTitle.AddCssClass("dim-label");
        priceTitle.Halign = Gtk.Align.Start;
        priceTitle.Xalign = 0;

        priceValue = Gtk.Label.New("");
        priceValue.AddCssClass("caption-heading");
        priceValue.Halign = Gtk.Align.End;
        priceValue.Xalign = 1;

        changeTitle = Gtk.Label.New(_("Change"));
        changeTitle.AddCssClass("caption");
        changeTitle.AddCssClass("dim-label");
        changeTitle.Halign = Gtk.Align.Start;
        changeTitle.Xalign = 0;

        changeValue = Gtk.Label.New("");
        changeValue.AddCssClass("caption-heading");
        changeValue.Halign = Gtk.Align.End;
        changeValue.Xalign = 1;

        grid.Attach(dateTitle, 0, 0, 1, 1);
        grid.Attach(dateValue, 1, 0, 1, 1);
        grid.Attach(priceTitle, 0, 1, 1, 1);
        grid.Attach(priceValue, 1, 1, 1, 1);
        grid.Attach(changeTitle, 0, 2, 1, 1);
        grid.Attach(changeValue, 1, 2, 1, 1);

        container.Append(grid);
        SetChild(container);
    }

    public void SetValues(TickerRange range, string dateLabel, string price, IPercentageChange percentage)
    {
        HasArrow = true;
        priceTitle.Visible = true;
        priceValue.Visible = true;

        var isShortRange = range == TickerRange.Day || range == TickerRange.FiveDays;
        dateTitle?.SetLabel(isShortRange ? _("Time") : _("Date"));
        dateValue?.SetLabel(dateLabel);
        
        priceValue?.SetLabel(price);
        SetChange(percentage);
    }

    public void SetRangeValues(string rangeLabel, IPercentageChange percentage)
    {
        HasArrow = false;
        priceTitle.Visible = false;
        priceValue.Visible = false;

        dateTitle?.SetLabel(_("Range"));
        dateValue?.SetLabel(rangeLabel);
        
        SetChange(percentage);
    }

    private void SetChange(IPercentageChange percentage)
    {
        changeValue?.SetLabel(percentage.ToString() ?? "");
        changeValue?.RemoveCssClass("success");
        changeValue?.RemoveCssClass("error");
        changeValue?.AddCssClass(percentage.IsPositive ? "success" : "error");
    }
}
