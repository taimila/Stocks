// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

[GObject.Subclass<Gtk.Box>(qualifiedName: nameof(TickerGridCard))]
[Gtk.Template<Gtk.AssemblyResource>("TickerGridCard.ui")]
public partial class TickerGridCard
{
    [Gtk.Connect] private Gtk.Label displayName;
    [Gtk.Connect] private Gtk.Label name;
    [Gtk.Connect] private Gtk.Label value;
    [Gtk.Connect] private Gtk.Label change;
    [Gtk.Connect] private Adw.Bin chartBin;

    private TickerChart chart = null!;

    public Ticker Ticker { get; private set; } = null!;

    public static TickerGridCard NewWithTicker(Ticker ticker)
    {
        var card = NewWithProperties([]);
        card.SetTicker(ticker);

        return card;
    }

    private void SetTicker(Ticker ticker)
    {
        Ticker = ticker;
        TickerContextMenu.Attach(this, Ticker);

        chart = TickerChart.New();
        chart.EnableMouseInteraction = false;
        chart.ShowDotOnHover = false;
        chart.ShowPreviousCloseLine = true;
        chart.ShowGradient = true;
        chart.ShowXScale = false;
        chart.ShowYScale = false;
        chart.LineWidth = 1.5;
        chart.Hexpand = true;
        chart.Vexpand = true;

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
        displayName.TooltipText = ticker.Symbol.Value;

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
