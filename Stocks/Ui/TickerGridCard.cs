// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class TickerGridCard : Gtk.Box
{
    [Gtk.Connect] private readonly Gtk.Label displayName;
    [Gtk.Connect] private readonly Gtk.Label name;
    [Gtk.Connect] private readonly Gtk.Label value;
    [Gtk.Connect] private readonly Gtk.Label change;
    [Gtk.Connect] private readonly Adw.Bin chartBin;

    private readonly TickerChart chart;

    public Ticker Ticker { get; }

    private TickerGridCard(Gtk.Builder builder, string name)
        : base(new Gtk.Internal.BoxHandle(builder.GetPointer(name), false))
    {
        builder.Connect(this);
    }

    public TickerGridCard(Ticker ticker)
        : this(Builder.FromFile("TickerGridCard.ui"), "ticker-grid-card")
    {
        Ticker = ticker;
        TickerContextMenu.Attach(this, () => Ticker);

        chart = new TickerChart
        {
            EnableMouseInteraction = false,
            ShowDotOnHover = false,
            ShowPreviousCloseLine = true,
            ShowGradient = true,
            ShowXScale = false,
            ShowYScale = false,
            LineWidth = 1.5,
            Hexpand = true,
            Vexpand = true
        };

        chartBin.SetChild(chart);

        ticker.OnUpdated += t =>
        {
            GLib.Functions.IdleAdd(100, () =>
            {
                UpdateUI(t);
                return false;
            });
        };

        UpdateUI(ticker);
    }

    private void UpdateUI(Ticker ticker)
    {
        if (!ticker.TryGetData(TickerRange.Day, out var data))
            return;

        displayName.SetLabel(ticker.DisplayName);
        displayName.TooltipText = ticker.Symbol;

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
