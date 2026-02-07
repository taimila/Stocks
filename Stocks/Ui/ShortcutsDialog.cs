// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

public static class ShortcutsDialog
{
    public static Adw.ShortcutsDialog Create()
    {
        var builder = Builder.FromFile("ShortcutsDialog.ui");
        var dialog = builder.GetObject("shortcutsDialog") as Adw.ShortcutsDialog;
        return dialog!;
    }
}
