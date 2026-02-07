// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks.UI;

enum Theme
{
    System,
    Light,
    Dark
}

public class ThemeSwitcher : Gtk.Box 
{
    [Gtk.Connect] private readonly Gtk.CheckButton system;
    [Gtk.Connect] private readonly Gtk.CheckButton light;
    [Gtk.Connect] private readonly Gtk.CheckButton dark;

    private readonly Gio.Settings settings;

    private ThemeSwitcher(Gtk.Builder builder, string name) : base(new Gtk.Internal.BoxHandle(builder.GetPointer(name), false))
    {
        builder.Connect(this);
        system!.OnToggled += (_, _) => { if (system.GetActive()) Enable(Theme.System); };
        light!.OnToggled += (_, _) => { if (light.GetActive()) Enable(Theme.Light); };
        dark!.OnToggled += (_, _) => { if (dark.GetActive()) Enable(Theme.Dark); };
    }

    public ThemeSwitcher(Gio.Settings settings) : this(Builder.FromFile("ThemeSwitcher.ui"), "themeSwitcher")
    {
        this.settings = settings;

        this.settings.OnChanged += (_, args) =>
        {
            if (args.Key == "theme")
                EnableThemeFromGSettings();
        };

        EnableThemeFromGSettings();
    }

    private void EnableThemeFromGSettings()
    {
        var theme = (Theme)settings.GetEnum("theme");
        Enable(theme);
    }

    private void Enable(Theme theme)
    {
        var manager = Adw.StyleManager.GetDefault();
        var scheme = manager.GetColorScheme();

        switch (theme)
        {
            case Theme.System:
                if (scheme == Adw.ColorScheme.PreferLight) return; // Prevents infinite loop
                system.SetActive(true);
                manager.SetColorScheme(Adw.ColorScheme.PreferLight);
                settings.SetEnum("theme", 0);
                break;
            case Theme.Light:
                if (scheme == Adw.ColorScheme.ForceLight) return; // Prevents infinite loop
                light.SetActive(true);
                manager.SetColorScheme(Adw.ColorScheme.ForceLight);
                settings.SetEnum("theme", 1);
                break;
            case Theme.Dark:
                if (scheme == Adw.ColorScheme.ForceDark) return; // Prevents infinite loop
                dark.SetActive(true);
                manager.SetColorScheme(Adw.ColorScheme.ForceDark);
                settings.SetEnum("theme", 2);
                break;
        }
    }
} 
