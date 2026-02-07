// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public class TickerDataParser
{
    public TickerData Parse(Result result, TickerRange range)
    {
        var numberOfDecimals = result.Meta.PriceHint ?? 2;

        var open = result.Indicators.Quote.FirstOrDefault()?.Open ?? [];
        var close = result.Indicators.Quote.FirstOrDefault()?.Close ?? [];

        var closeValues = close.Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        var fallbackPrice = result.Meta.RegularMarketPrice;

        var startPrice = closeValues.Length > 0 ? closeValues.First() : fallbackPrice;
        var endPrice = closeValues.Length > 0 ? closeValues.Last() : fallbackPrice;

        var dataPoints = CreateDataPoints(result);
        if (dataPoints.Length == 0)
        {
            dataPoints = [CreateFallbackDataPoint(result)];
        }

        IPercentageChange percentage = range == TickerRange.Day || range == TickerRange.FiveDays ? 
            new ChangeFromPreviousClose(result.Meta.RegularMarketPrice, result.Meta.ChartPreviousClose) :
            new ChangeBetweenTwoPrices(startPrice, endPrice);

        var data = new TickerData {
            Range = range,
            PreviousClose = result.Meta.ChartPreviousClose,
            MarketPrice = new Amount(result.Meta.RegularMarketPrice, result.Meta.Currency, numberOfDecimals),
            MarketDayHigh = new Amount(result.Meta.RegularMarketDayHigh, result.Meta.Currency, numberOfDecimals),
            MarketDayLow = new Amount(result.Meta.RegularMarketDayLow, result.Meta.Currency, numberOfDecimals),
            MarketDayOpen = new Amount(open?.FirstOrDefault() ?? 0, result.Meta.Currency, numberOfDecimals), // Is this correct?
            PercentageChange = percentage,
            DataPoints = dataPoints
        };
        
        return data;
    }

    private static DataPoint[] CreateDataPoints(Result result)
    {
        var quote = result.Indicators.Quote?.FirstOrDefault();
        
        if (quote == null) 
            return [];

        var timestamps = result.Timestamp?.Select(ts => DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime).ToArray() ?? [];
        var open = ReplaceNullsWithPrevious(quote.Open ?? []);
        var close = ReplaceNullsWithPrevious(quote.Close ?? []);
        var high = ReplaceNullsWithPrevious(quote.High ?? []);
        var low = ReplaceNullsWithPrevious(quote.Low ?? []);
        var volume = quote.Volume?.Select(v => v ?? 0L).ToArray() ?? [];

        var minLength = new[] { timestamps.Length, open.Length, high.Length, low.Length, close.Length, volume.Length }
            .Where(len => len > 0)
            .DefaultIfEmpty(0)
            .Min();

        if (minLength == 0)
        {
            return [];
        }

        return timestamps.Take(minLength).Select((t, i) => new DataPoint(
            Timestamp: t,
            Open: open[i],
            High: high[i],
            Low: low[i],
            Close: close[i],
            Volume: volume[i]
        )).ToArray();
    }

    private static DataPoint CreateFallbackDataPoint(Result result)
    {
        var timestamp = result.Meta.RegularMarketTime is long marketTime
            ? DateTimeOffset.FromUnixTimeSeconds(marketTime).UtcDateTime
            : DateTime.UtcNow;

        var price = result.Meta.RegularMarketPrice;
        var high = result.Meta.RegularMarketDayHigh;
        var low = result.Meta.RegularMarketDayLow;
        var open = result.Meta.ChartPreviousClose;
        var volume = result.Meta.RegularMarketVolume ?? 0;

        return new DataPoint(
            Timestamp: timestamp,
            Open: open,
            High: high,
            Low: low,
            Close: price,
            Volume: volume
        );
    }

    // We want to keep data sets same size. Missing values will be replaced
    // with previous one which makes chart draw horizonal line when data point
    // is missing. This is the best we can do.
    private static double[] ReplaceNullsWithPrevious(double?[] data)
    {
        if (data.Length == 0) return [];
        
        var result = new double[data.Length];
        double last = 0;
        
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].HasValue)
            {
                last = data[i].Value;
                result[i] = data[i].Value;
            }
            else
            {
                result[i] = last;
            }
        }
        
        return result;
    }
}
