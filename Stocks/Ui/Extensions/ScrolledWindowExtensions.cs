// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.UI;

public static class ScrolledWindowExtensions 
{
    public static void ScrollToBottom(this Gtk.ScrolledWindow sw)
    {
        var adj = sw.Vadjustment;

        double start = adj!.Value;
        double end = adj.Upper - adj.PageSize;
        double duration = 0.3; // seconds

        var clock = sw.GetFrameClock();
        long startTime = clock!.GetFrameTime();

        sw.AddTickCallback((widget, frameClock) =>
        {
            long now = frameClock.GetFrameTime();
            double t = (now - startTime) / 1_000_000.0 / duration;
            if (t >= 1.0) t = 1.0;

            double eased = EaseOutCubic(t);
            adj.Value = start + (end - start) * eased;

            return t < 1.0; // false removes callback
        });
    }

    private static double EaseOutCubic(double t)
    {
        return 1.0 - Math.Pow(1.0 - t, 3);
    }
}