// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public enum BrowseMode
{
    List,
    Grid
}

public sealed class AppMode
{
    private readonly Gio.Settings settings;

    public BrowseMode Current { get; private set; }
    public event Action<BrowseMode>? OnChanged;

    public AppMode(Gio.Settings settings)
    {
        this.settings = settings;
        this.settings.OnChanged += (_, args) =>
        {
            if (args.Key == "browse-mode")
                LoadFromSettings();
        };

        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var mode = (BrowseMode)settings.GetEnum("browse-mode");
        if (mode == Current)
            return;

        Current = mode;
        OnChanged?.Invoke(mode);
    }

    public void SetBrowseMode(BrowseMode mode)
    {
        if (mode == Current)
            return;

        Current = mode;
        settings.SetEnum("browse-mode", (int)mode);
        OnChanged?.Invoke(mode);
    }
}
