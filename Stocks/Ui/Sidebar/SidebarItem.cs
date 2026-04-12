// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class SidebarItem : Gtk.Grid
{
    [Gtk.Connect] private readonly Gtk.Label symbol;
    [Gtk.Connect] private readonly Gtk.Label name;
    [Gtk.Connect] private readonly Gtk.Label value;
    [Gtk.Connect] private readonly Gtk.Label change;
    [Gtk.Connect] private readonly Adw.Bin chartBin;

    private readonly TickerChart chart;

    public Ticker Ticker { get; private set; }
  
    private SidebarItem(Gtk.Builder builder, string name) : base(new Gtk.Internal.GridHandle(builder.GetPointer(name), false))
    {
        builder.Connect(this);
    }

    public SidebarItem(Gtk.SizeGroup g1, Gtk.SizeGroup g2, Gtk.SizeGroup g3, Ticker ticker) : this(Builder.FromFile("SidebarItem.ui"), "sidebar-item")
    {
        // Add widgets to groups so that GTK can align these over all sidebar items.
        g1.AddWidget(symbol);
        g1.AddWidget(name);
        g2.AddWidget(chartBin);
        g3.AddWidget(value);

        chart = new TickerChart
        {
            EnableMouseInteraction = false,
            ShowDotOnHover = false,
            ShowPreviousCloseLine = true,
            ShowGradient = true,
            ShowXScale = false,
            ShowYScale = false,
            LineWidth = 1,
            CloseMarkerWidth = 1,
            Padding = 1,
            MarginTop = 10,
            MarginBottom = 10
        };
        chartBin.SetChild(chart);

        Ticker = ticker;
        TickerContextMenu.Attach(this, Ticker);

        ticker.OnUpdated += ticker =>
        {
            GLib.Functions.IdleAdd(100, () =>
            {
                UpdateUI(ticker);
                return false;
            });
        };

        UpdateUI(ticker);
    }

    private void UpdateUI(Ticker ticker)
    {
        // Skip UI update if there is no data availabe.
        if (!ticker.TryGetData(TickerRange.Day, out var data))
            return;
    
        symbol.SetLabel(ticker.DisplayName);
        symbol.TooltipText = ticker.Symbol; 

        name.SetLabel(ticker.Name);
        name.TooltipText = ticker.Name;
        
        value.SetLabel(data.MarketPrice.ToStringWithoutCurrency());
        
        change.SetLabel(data.PercentageChange.ToString() ?? "");
        change.RemoveCssClass("positive-label");
        change.RemoveCssClass("negative-label");
        change.AddCssClass(data.PercentageChange.IsPositive ? "positive-label" : "negative-label");

        chart.Set(data, true);
    }
}
