// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later
using Stocks.Model;

namespace Stocks.UI;

public class ThemeSwitcher : Gtk.Box 
{
    [Gtk.Connect] private readonly Gtk.CheckButton system;
    [Gtk.Connect] private readonly Gtk.CheckButton light;
    [Gtk.Connect] private readonly Gtk.CheckButton dark;

    private readonly AppTheme model;
    private bool syncingUi = false;

    private ThemeSwitcher(Gtk.Builder builder, string name) : base(new Gtk.Internal.BoxHandle(builder.GetPointer(name), false))
    {
        builder.Connect(this);
    }

    public ThemeSwitcher(AppTheme model) : this(Builder.FromFile("ThemeSwitcher.ui"), "themeSwitcher")
    {
        this.model = model;

        system.OnToggled += (_, _) => OnUserThemeToggled(system, Theme.System);
        light.OnToggled += (_, _) => OnUserThemeToggled(light, Theme.Light);
        dark.OnToggled += (_, _) => OnUserThemeToggled(dark, Theme.Dark);

        model.OnChanged += UpdateButtons;

        UpdateButtons(model.Current);
    }

    private void OnUserThemeToggled(Gtk.CheckButton source, Theme theme)
    {
        if (syncingUi || !source.GetActive())
            return;

        model.SetTheme(theme);
    }

    private void UpdateButtons(Theme theme)
    {
        syncingUi = true;
        system.SetActive(theme == Theme.System);
        light.SetActive(theme == Theme.Light);
        dark.SetActive(theme == Theme.Dark);
        syncingUi = false;
    }
} 
