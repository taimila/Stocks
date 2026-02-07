// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace Stocks.Model;

public class AliasStorage
{
    private readonly string filePath;
    private Dictionary<string, string> aliases = [];

    public AliasStorage()
    {
        filePath = GetAliasFilePath();
        LoadFromDisk();
    }

    public string? GetAlias(string symbol)
    {
        var key = NormalizeSymbol(symbol);
        return aliases.TryGetValue(key, out var alias) ? alias : null;
    }

    // Setting alias to empty string removes it!
    public void SetAlias(string symbol, string alias)
    {
        var key = NormalizeSymbol(symbol);
        if (string.IsNullOrEmpty(key))
            return;

        var trimmedAlias = alias?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmedAlias))
        {
            RemoveAlias(key);
            return;
        }

        aliases[key] = trimmedAlias;
        SaveToDisk();
    }

    public void RemoveAlias(string symbol)
    {
        var key = NormalizeSymbol(symbol);
        if (string.IsNullOrEmpty(key))
            return;

        if (aliases.Remove(key))
            SaveToDisk();
    }

    private static string NormalizeSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return "";

        return symbol.Trim().ToUpperInvariant();
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(filePath))
            return;

        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (data != null)
                aliases = data;
        }
        catch
        {
            // Ignore malformed file to keep app working. User must have edited it directely.
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(aliases);
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Don't let IO failure crash the app. Storing alises is not that important.
        }
    }

    private string GetAliasFilePath()
    {
        var baseDir = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, APP_ID, "aliases.json");
    }
}