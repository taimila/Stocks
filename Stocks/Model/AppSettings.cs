// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public class AppSettings
{
    private readonly Gio.Settings settings;

    public AppSettings(Gio.Settings settings)
    {
        this.settings = settings;
    }

    public int UpdateIntervalInSeconds
    {
        get => settings.GetInt("update-interval");
        set => settings.SetInt("update-interval", value);
    }

    public string UserAgent
    {
        get => settings.GetString("user-agent");
        set => settings.SetString("user-agent", value);
    }
}