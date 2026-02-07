// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using Stocks.Model;

namespace Stocks.UI;

public class TickerDetails : Gtk.Box
{
    [Gtk.Connect] private readonly Gtk.Box cardContent;

    // Main containers to switch between details / no-details
    [Gtk.Connect] private readonly Gtk.Box noDetails;
    [Gtk.Connect] private readonly Adw.Clamp detailsCard;
    [Gtk.Connect] private readonly Adw.Clamp detailsFooter;

    // Header
    [Gtk.Connect] private readonly Gtk.Grid narrowHeader;
    [Gtk.Connect] private readonly Gtk.Grid wideHeader;

    [Gtk.Connect] private readonly Gtk.Label name;
    [Gtk.Connect] private readonly Gtk.Label symbol;
    [Gtk.Connect] private readonly Gtk.Box symbolDisplayBox;
    [Gtk.Connect] private readonly Gtk.Box symbolEditBox;
    [Gtk.Connect] private readonly Gtk.Entry aliasEntry;
    [Gtk.Connect] private readonly Gtk.Button aliasSaveButton;
    [Gtk.Connect] private readonly Gtk.Label currentValue;
    [Gtk.Connect] private readonly Gtk.Label changePercentage;
    [Gtk.Connect] private readonly Gtk.Label name2;
    [Gtk.Connect] private readonly Gtk.Label currentValue2;
    [Gtk.Connect] private readonly Gtk.Label changePercentage2;

    // Graph
    [Gtk.Connect] private readonly Adw.ToggleGroup rangeToggles;
    [Gtk.Connect] private readonly Adw.Bin chartContainer;
    [Gtk.Connect] private readonly Adw.Bin rangeGroupDropdown;

    // Details
    [Gtk.Connect] private readonly Gtk.Label open;
    [Gtk.Connect] private readonly Gtk.Label high;
    [Gtk.Connect] private readonly Gtk.Label low;
    [Gtk.Connect] private readonly Gtk.Label marketStatus;
    [Gtk.Connect] private readonly Gtk.Label open2;
    [Gtk.Connect] private readonly Gtk.Label high2;
    [Gtk.Connect] private readonly Gtk.Label low2;
    [Gtk.Connect] private readonly Gtk.Label marketStatus2;

    // Footer
    [Gtk.Connect] private readonly Gtk.Grid narrowFooter;
    [Gtk.Connect] private readonly Gtk.Grid wideFooter;

    [Gtk.Connect] private readonly Gtk.Label updatedLabel;
    [Gtk.Connect] private readonly Gtk.Label readMore;

    private bool isNarrow = false;
    private bool isEditingAlias = false;
    private Ticker? activeTicker;
    private KeyValueDropDown rangeDropdown;
    private readonly PeriodicTimer timer = new(System.TimeSpan.FromSeconds(1));
    
    private AppModel model;
    private TickerChart chart;
    private Gtk.Overlay chartOverlay;
    private Adw.StatusPage noDataView;
    private Action<Ticker>? updateHandler;

    private TickerDetails(Gtk.Builder builder, string name) : base(new Gtk.Internal.BoxHandle(builder.GetPointer(name), false))
    {
        builder.Connect(this);
    }

    public TickerDetails(AppModel model) : this(Builder.FromFile("TickerDetails.ui"), "tickerDetails")
    {
        this.model = model;

        SetupChart();
        SetupRangeDropDown();
        SetupRangeToggles();
        SetupPeriodicUpdates();
        SetupAliasEditor();

        model.OnActiveTickerChanged += async (previous, current) => {
            if (previous is Ticker t && updateHandler != null)
                t.OnUpdated -= updateHandler;

            updateHandler = CreateUiUpdateHandler(UpdateUI);
            current.OnUpdated += updateHandler;
            await current.Refresh(model.ActiveRange);
        };

        model.OnActiveTickerRangeChanged += async (range) =>
        {            
            var name = range.ToString();
            var dropdownSelection = (rangeDropdown.SelectedItem as DropdownItem)?.Key;
            var togglesSelection = rangeToggles.GetActiveName();

            // Update dropdown UI to match model value when user used toggles
            if (name != dropdownSelection)
                rangeDropdown.SetSelectedItem(name);

            // Update toggles UI to match model value when user used dropdown
            if (name != togglesSelection)
                rangeToggles.SetActiveName(name);

            if (model.SelectedTicker is Ticker ticker)
                await ticker.Refresh(range);
        };       
    }

    private void SetupPeriodicUpdates()
    {
        Task.Run(async () => {
            do { 
                GLib.Functions.IdleAdd(100, () =>
                {
                    UpdateTimestamp();
                    UpdateMarketStatus();
                    return false;
                });
            }
            while (await timer.WaitForNextTickAsync());    
        });
    }

    private void SetupRangeToggles()
    {
        rangeToggles.OnActiveNameChanged(n =>
        {
            if (rangeToggles.GetActiveName() is string name)
                SetRangeFromName(name);
            else
                model.ActiveRange = TickerRange.Day;
        });
    }

    private void SetupRangeDropDown()
    {
        var items = new Dictionary<string, string>();
        
        foreach (var key in Enum.GetNames<TickerRange>())
        {
            items[key] = Enum.Parse<TickerRange>(key).GetDisplayName();
        }

        rangeDropdown = new KeyValueDropDown(items);
        rangeDropdown.Hexpand = false;
        rangeDropdown.Halign = Gtk.Align.Start;
        rangeDropdown.OnValueSelected += SetRangeFromName;

        rangeGroupDropdown.SetChild(rangeDropdown);
    }

    private void SetRangeFromName(string name)
    {
        var newValue = Enum.Parse<TickerRange>(name);
        if (newValue == model.ActiveRange)
            return;

        model.ActiveRange = newValue;
    }

    private void SetupChart()
    {
        void OnChartHover(DataPoint? dp1, DataPoint? dp2, bool hideDetailsWhileHover)
        {
            if (hideDetailsWhileHover)
            {
                if (dp1 == null && dp2 == null)
                {
                    wideFooter.Opacity = 1;
                    narrowFooter.Opacity = 1;
                }
                else
                {
                    wideFooter.Opacity = 0;
                    narrowFooter.Opacity = 0;
                }
            }
            else
            {
                if (model.SelectedTicker is Ticker ticker)
                {
                    var (amount, percentage) = ticker.GetAmountAndChangeFor(model.ActiveRange, dp1, dp2);
                    currentValue.SetLabel(amount?.ToStringWithCurrency() ?? "");
                    UpdatePercentage(percentage);
                }
            }
        }
     
        chart = new TickerChart { Vexpand = true, Hexpand = true };
        chart.OnHover += OnChartHover;

        noDataView = new Adw.StatusPage
        {
            Title = _("Chart not available"),
            IconName = "emblem-important-symbolic",
            WidthRequest = 400,
            Halign = Gtk.Align.Center,
            Valign = Gtk.Align.Center,
            Hexpand = true,
            Vexpand = true
        };

        chartOverlay = new Gtk.Overlay();
        chartOverlay.Hexpand = true;
        chartOverlay.SetChild(chart);
        chartOverlay.AddOverlay(noDataView);
        noDataView.Visible = false;

        chartContainer.SetChild(chartOverlay);
    }

    public void SetIsNarrow(bool enable)
    {
        isNarrow = enable;
        SetAliasEditMode(false);

        if (model.SelectedTicker is Ticker ticker)
        {
            UpdateUI(ticker);
        }
    }
    
    private void UpdateUI(Ticker t)
    {
        if (model.SelectedTicker is Ticker ticker)
        {
            if (!ReferenceEquals(activeTicker, ticker))
            {
                activeTicker = ticker;
                SetAliasEditMode(false, resetEntry: true);
            }

            noDetails.Visible = false;
            detailsCard.Visible = true;
            detailsFooter.Visible = true;

            MarginStart = isNarrow ? 8 : 20;
            MarginEnd = isNarrow ? 8 : 20;

            cardContent.MarginStart = isNarrow ? 8 : 20;
            cardContent.MarginEnd = isNarrow ? 8 : 20;
            cardContent.MarginTop = isNarrow ? 8 : 20;
            cardContent.MarginBottom = isNarrow ? 8 : 20;

            wideHeader.Visible = !isNarrow;
            rangeToggles.Visible = !isNarrow;
            wideFooter.Visible = !isNarrow;

            narrowHeader.Visible = isNarrow;
            narrowFooter.Visible = isNarrow;

            UpdateChart(ticker);
            UpdateHeader();
            UpdateFooter();
            UpdateMarketStatus();
            UpdateYahooLink();
            UpdateTimestamp();
        }
        else
        {
            noDetails.Visible = true;
            detailsCard.Visible = false;
            detailsFooter.Visible = false;
        }
    }

    private void UpdateChart(Ticker ticker)
    {
        chart.ShowYScale = !isNarrow;

        if (ticker.TryGetData(model.ActiveRange, out var activeData) && activeData.DataPoints.Length >= 2)
        {
            var isDayChart = model.ActiveRange == TickerRange.Day;
            chart.Set(activeData, showPreviousCloseLine: isDayChart);
            SetChartAvailability(true);
        }
        else
        {
            chart.Clear();
            SetChartAvailability(false);
        }
    }

    private void SetChartAvailability(bool hasData)
    {
        noDataView.Visible = !hasData;
        chart.EnableMouseInteraction = hasData;

        if (!hasData)
        {
            wideFooter.Opacity = 1;
            narrowFooter.Opacity = 1;
        }
    }

    private void UpdateHeader()
    {
        if (model.SelectedTicker is Ticker ticker)
        {
            if (!ticker.TryGetData(model.ActiveRange, out var data))
                return;

            // Update toggles based on range availability.
            foreach (TickerRange value in Enum.GetValues<TickerRange>())
            {
                var toggle = rangeToggles.GetToggleByName(value.ToString());
                toggle!.Enabled = ticker.AvailableRanges.Contains(value);
            }

            // Update dropdown based on range availability.
            var items = new Dictionary<string, string>();
            foreach (var range in ticker.AvailableRanges)
            {
                items[range.ToString()] = range.GetDisplayName();
            }
            rangeDropdown.SetItems(items);

            name.SetLabel(ticker.Name);
            name2.SetLabel(ticker.Name);
            symbol.SetLabel(ticker.DisplayName);
            symbol.TooltipText = ticker.Symbol;

            if (!isEditingAlias)
            {
                aliasEntry.SetText(ticker.UserGivenAlias);
                aliasEntry.PlaceholderText = ticker.Symbol;
            }
            currentValue.SetLabel(data.MarketPrice.ToStringWithCurrency());
            currentValue2.SetLabel(data.MarketPrice.ToStringWithCurrency());

            UpdatePercentage(data.PercentageChange);
        }
    }

    private void SetupAliasEditor()
    {
        var click = Gtk.GestureClick.New();
        click.SetButton(1);
        click.OnPressed += (_, _) => BeginAliasEdit();
        symbolDisplayBox.AddController(click);

        aliasEntry.OnActivate += (_, _) => CommitAliasEdit(); // Allows hitting Enter to commit
        aliasSaveButton.OnClicked += (_, _) => CommitAliasEdit();
    }

    private void BeginAliasEdit()
    {
        if (model.SelectedTicker is not Ticker ticker)
            return;

        aliasEntry.SetText(ticker.UserGivenAlias);
        aliasEntry.PlaceholderText = ticker.Symbol;
        SetAliasEditMode(true);
        aliasEntry.GrabFocus();
    }

    private void CommitAliasEdit()
    {
        if (!isEditingAlias)
            return;

        if (model.SelectedTicker is not Ticker ticker)
            return;

        var alias = aliasEntry.GetText().Trim();
        model.SetTickerAlias(ticker, alias);
        SetAliasEditMode(false, resetEntry: true);
    }

    private void SetAliasEditMode(bool enabled, bool resetEntry = false)
    {
        isEditingAlias = enabled;
        symbolDisplayBox.Visible = !enabled;
        symbolEditBox.Visible = enabled;

        if (resetEntry && model.SelectedTicker is Ticker ticker)
        {
            aliasEntry.SetText(ticker.UserGivenAlias);
            aliasEntry.PlaceholderText = ticker.Symbol;
        }
    }

    private void UpdatePercentage(IPercentageChange percentage)
    {
        changePercentage.SetLabel(percentage.ToString() ?? "");
        changePercentage2.SetLabel(percentage.ToString() ?? "");

        if (percentage.IsPositive)
        {
            changePercentage.RemoveCssClass("primary-value-negative");
            changePercentage.AddCssClass("primary-value-positive");
            changePercentage2.RemoveCssClass("primary-value-negative");
            changePercentage2.AddCssClass("primary-value-positive");
        }
        else
        {
            changePercentage.RemoveCssClass("primary-value-positive");
            changePercentage.AddCssClass("primary-value-negative");
            changePercentage2.RemoveCssClass("primary-value-positive");
            changePercentage2.AddCssClass("primary-value-negative");
        }
    }

    private void UpdateYahooLink()
    {
        if (model.SelectedTicker is Ticker ticker)
        {
            var yahooLink = $"https://finance.yahoo.com/quote/{ticker.Symbol.ToUpper()}/";
            var markup = string.Format(_("Read more on <a href=\"{0}\">Yahoo Finance</a>."), yahooLink);
            readMore.SetMarkup(markup);
        }
    }

    private void UpdateFooter()
    {
        if (model.SelectedTicker is Ticker ticker)
        {
            if (!ticker.TryGetData(TickerRange.Day, out var data))
                return;
            
            open.SetLabel(data.MarketDayOpen.ToStringWithCurrency());
            high.SetLabel(data.MarketDayHigh.ToStringWithCurrency());
            low.SetLabel(data.MarketDayLow.ToStringWithCurrency());

            open2.SetLabel(data.MarketDayOpen.ToStringWithCurrency());
            high2.SetLabel(data.MarketDayHigh.ToStringWithCurrency());
            low2.SetLabel(data.MarketDayLow.ToStringWithCurrency());
        }
    }

    private void UpdateMarketStatus()
    {
        if (model.SelectedTicker is Ticker ticker)
        {
            var status = ticker.MarketStatus switch
            {
                MarketStatus.Open => C_("market-status", "Open"),
                MarketStatus.Closed => _("Closed"),
                _ => _("Unknown")
            };

            marketStatus.SetLabel(status);
            marketStatus2.SetLabel(status);
        }
    }

    private void UpdateTimestamp()
    {
        if (model.SelectedTicker is Ticker ticker)
        {
            if (ticker.LastUpdated == System.DateTime.MinValue) 
                return;
        
            var minutes = (System.DateTime.Now - ticker.LastUpdated).TotalMinutes;

            if(minutes < 1)
                updatedLabel.SetLabel(_("Updated less than minute ago."));
            else
                updatedLabel.SetLabel(string.Format(_("Updated {0} minutes ago."), Math.Round(minutes)));       
        }
    }

    private Action<Ticker> CreateUiUpdateHandler(Action<Ticker> handler)
    {
        return ticker =>
        {
            GLib.Functions.IdleAdd(100, () =>
            {
                handler(ticker);
                return false;
            });
        };
    }
}
