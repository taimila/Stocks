// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;

namespace Stocks.UI;

public static class Styles
{
    public static void Load()
    {
        try
        {
            var css = ReadCssFromFile();
            RegisterCssToGtk(css);
        } 
        catch
        {
            // Ignore CSS loading failure. Not worth crashing the app even if it looks bit broken.
        }
    }

    private static string ReadCssFromFile()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("styles.css");
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    private static void RegisterCssToGtk(string css)
    {
        var cssProvider = Gtk.CssProvider.New();
        cssProvider.LoadFromString(css);
        Gtk.StyleContext.AddProviderForDisplay(Gdk.Display.GetDefault()!, cssProvider, 1000);
    }
}
