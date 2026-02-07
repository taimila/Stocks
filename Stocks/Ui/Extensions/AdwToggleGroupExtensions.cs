// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.UI;

public static class AdwToggleGroupExtensions
{
    public static void OnActiveNameChanged(this Adw.ToggleGroup group, Action<string> action)
    {
        group.OnNotify += (s,a) =>
        {
            if (a.Pspec.GetName() == "active-name")
            {
                var name = group.GetActiveName();
                action.Invoke(name ?? "");
            }
        };
    }
}
