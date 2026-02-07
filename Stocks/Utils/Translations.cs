// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Stocks;

public static class Translations
{
    private static NGettext.Catalog? catalog;

    public static void Initialize()
    {
        catalog = new NGettext.Catalog("Stocks", "/app/share/locale");
    }

    public static string _(string text)
    {
        return catalog?.GetString(text) ?? "Translations were not intialized.";
    }

    public static string C_(string context, string text)
    {
        return catalog?.GetParticularString(context, text) ?? "Translations were not intialized.";
    }
}
