// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.Model;

public enum Theme
{
    System,
    Light,
    Dark
}

public sealed class AppTheme
{
    private readonly Gio.Settings settings;
    private bool hasTheme;

    public Theme Current { get; private set; }

    public event Action<Theme>? OnChanged;

    public AppTheme(Gio.Settings settings)
    {
        this.settings = settings;
        this.settings.OnChanged += (_, args) =>
        {
            if (args.Key == "theme")
                LoadThemeFromSettings();
        };

        LoadThemeFromSettings();
    }

    public void SetTheme(Theme theme)
    {
        if (hasTheme && theme == Current)
            return;

        ApplyTheme(theme, persistToSettings: true);
    }

    private void LoadThemeFromSettings()
    {
        var theme = (Theme)settings.GetEnum("theme");
        if (hasTheme && theme == Current)
            return;

        ApplyTheme(theme, persistToSettings: false);
    }

    private void ApplyTheme(Theme theme, bool persistToSettings)
    {
        Current = theme;
        hasTheme = true;

        var manager = Adw.StyleManager.GetDefault();
        switch (theme)
        {
            case Theme.System:
                manager.SetColorScheme(Adw.ColorScheme.PreferLight);
                break;
            case Theme.Light:
                manager.SetColorScheme(Adw.ColorScheme.ForceLight);
                break;
            case Theme.Dark:
                manager.SetColorScheme(Adw.ColorScheme.ForceDark);
                break;
        }

        if (persistToSettings && settings.GetEnum("theme") != (int)theme)
            settings.SetEnum("theme", (int)theme);

        OnChanged?.Invoke(theme);
    }
}
