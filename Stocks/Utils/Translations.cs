// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace Stocks;

public static partial class Translations
{
    private const int LC_ALL = 6;
    private const string LOCALE_DIRECTORY = "/app/share/locale";
    private const string TRANSLATION_DOMAIN = "Stocks";

    private static NGettext.Catalog? catalog;

    public static void Initialize()
    {
        SetLocale(LC_ALL, "");
        BindTextDomain(TRANSLATION_DOMAIN, LOCALE_DIRECTORY);
        BindTextDomainCodeset(TRANSLATION_DOMAIN, "UTF-8");
        TextDomain(TRANSLATION_DOMAIN);

        catalog = new NGettext.Catalog(TRANSLATION_DOMAIN, LOCALE_DIRECTORY);
    }

    public static string _(string text)
    {
        return catalog?.GetString(text) ?? "Translations were not intialized.";
    }

    public static string C_(string context, string text)
    {
        return catalog?.GetParticularString(context, text) ?? "Translations were not intialized.";
    }

    [LibraryImport("libc.so.6", EntryPoint = "setlocale", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr SetLocale(int category, string locale);

    [LibraryImport("libc.so.6", EntryPoint = "bindtextdomain", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr BindTextDomain(string domainName, string directoryName);

    [LibraryImport("libc.so.6", EntryPoint = "bind_textdomain_codeset", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr BindTextDomainCodeset(string domainName, string codeset);

    [LibraryImport("libc.so.6", EntryPoint = "textdomain", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr TextDomain(string domainName);
}
