// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later
using Stocks.Model;

namespace Stocks.UI;

[GObject.Subclass<Gtk.Box>(qualifiedName: nameof(ThemeSwitcher))]
[Gtk.Template<Gtk.AssemblyResource>("ThemeSwitcher.ui")]
public partial class ThemeSwitcher
{
    [Gtk.Connect] private Gtk.CheckButton system;
    [Gtk.Connect] private Gtk.CheckButton light;
    [Gtk.Connect] private Gtk.CheckButton dark;

    private AppTheme model = null!;
    private bool syncingUi = false;

    public static ThemeSwitcher NewWithModel(AppTheme model)
    {
        var switcher = NewWithProperties([]);
        switcher.SetModel(model);

        return switcher;
    }

    private void SetModel(AppTheme model)
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
