// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public record Marker(DateTime Timestamp, string Label);

public static class TickerRangeMarkerExtension
{
    public static (DateTime, TimeSpan, string) GetMarkerRules(this TickerRange range, DateTime start, TimeSpan? span = null)
    {
        TickerRange GetClosestThreshold(TimeSpan timeSpan)
        {
            int hours = (int)timeSpan.TotalHours;

            var thresholds = new (int Hours, TickerRange r)[]
            {
                (  1 * 24, TickerRange.Day),
                (  5 * 24, TickerRange.FiveDays),
                ( 30 * 24, TickerRange.Month),
                ( 90 * 24, TickerRange.ThreeMonths),
                (180 * 24, TickerRange.SixMonths),
                (365 * 24, TickerRange.Year)
            };

            (int Hours, TickerRange Range) closest = thresholds[0];
            int minDiff = Math.Abs(hours - closest.Hours);

            foreach (var t in thresholds)
            {
                int diff = Math.Abs(hours - t.Hours);

                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = t;
                }
            }

            return closest.Range;
        }

        switch (range)
        {
            case TickerRange.Day:
                var d1 = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
                return (d1, TimeSpan.FromHours(2), "HH:mm");
            
            case TickerRange.FiveDays:
                return (start.Date, TimeSpan.FromDays(1), "d.M");

            case TickerRange.Month:
                int daysUntilMonday = ((int)DayOfWeek.Monday - (int)start.DayOfWeek + 7) % 7;
                var d3 = daysUntilMonday switch { 0 => start.Date, _ => start.Date.AddDays(daysUntilMonday) };
                return (d3, TimeSpan.FromDays(7), "d.M");

            case TickerRange.ThreeMonths:
                return (new DateTime(start.Year, start.Month, 1), TimeSpan.FromDays(30), "d.M");
            
            case TickerRange.SixMonths:
                return (new DateTime(start.Year, start.Month, 1), TimeSpan.FromDays(60), "d.M");
            
            case TickerRange.Year:
                return (new DateTime(start.Year, start.Month, 1), TimeSpan.FromDays(90), "d.M");

            case TickerRange.TwoYears:
                return (new DateTime(start.Year, start.Month, 1), TimeSpan.FromDays(120), "d.M");

            case TickerRange.FiveYears:
                return (new DateTime(start.Year, 1, 1), TimeSpan.FromDays(365), "yyyy");

            case TickerRange.TenYears:
                return (new DateTime(start.Year, 1, 1), TimeSpan.FromDays(365 * 2), "yyyy");

            case TickerRange.All:
                return (new DateTime(start.Year, 1, 1), TimeSpan.FromDays(365 * 2), "yyyy");

            case TickerRange.Ytd:
                // Recursively use Day, FiveDays, Month, ThreeMonths, SixMonths or Year.
                return GetClosestThreshold(span.Value).GetMarkerRules(start);

            default:
                throw new Exception("Scale mapping missing for TickerRange: " + range);
        }
    }
}

public static class TickerDataExtensions
{
    public static List<Marker> GetMarkers(this TickerData data, int countLimit)
    {
        if (data == null || data.DataPoints.Length == 0) return [];

        var minTime = data.DataPoints.First().Timestamp.ToLocalTime();
        var maxTime = data.DataPoints.Last().Timestamp.ToLocalTime();
        var timeSpan = maxTime - minTime;

        var (start, interval, format) = data.Range.GetMarkerRules(minTime, timeSpan);

        // Create markers based on rules
        List<DateTime> dates;
        if (data.Range is TickerRange.ThreeMonths or TickerRange.SixMonths or TickerRange.Year or TickerRange.TwoYears)
        {
            dates = new List<DateTime>();
            var current = start;
            
            int months = data.Range switch
            {
                TickerRange.ThreeMonths => 1,
                TickerRange.SixMonths => 2,
                TickerRange.Year => 3,
                TickerRange.TwoYears => 4
            };

            while (current <= maxTime)
            {
                dates.Add(current);
                current = current.AddMonths(months);
            }
        }
        else if (data.Range is TickerRange.FiveYears or TickerRange.TenYears or TickerRange.All)
        {
            dates = [];
            var current = start;
            
            int years = data.Range switch
            {
                TickerRange.FiveYears => 1,
                TickerRange.TenYears => 2,
                TickerRange.All => 2
            };

            while (current <= maxTime)
            {
                dates.Add(current);
                current = current.AddYears(years);
            }
        }
        else
        {
            dates = [];
            for (var dt = start; dt <= maxTime; dt = dt.Add(interval))
            {
                dates.Add(dt);
            }
        }

        if (dates.Count >= countLimit)
        {
            var step = Math.Max(1, (dates.Count - 1) / (countLimit-1));
            var selectedMarkers = new List<DateTime>();
            for (int i = 0; i < dates.Count; i += step)
            {
                selectedMarkers.Add(dates[i]);
            }
            dates = selectedMarkers;
        }

        return dates.Select(d => new Marker(d, d.ToString(format))).ToList();
    }
}

