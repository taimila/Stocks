// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace Stocks.Model;

public class AliasStorage
{
    private readonly string filePath;
    private Dictionary<Symbol, string> aliases = [];

    public AliasStorage()
    {
        filePath = GetAliasFilePath();
        LoadFromDisk();
    }

    public string? GetAlias(Symbol symbol)
    {
        return aliases.TryGetValue(symbol, out var alias) ? alias : null;
    }

    // Setting alias to empty string removes it!
    public void SetAlias(Symbol symbol, string alias)
    {
        var trimmedAlias = alias.Trim();
        if (string.IsNullOrWhiteSpace(trimmedAlias))
        {
            RemoveAlias(symbol);
            return;
        }

        aliases[symbol] = trimmedAlias;
        SaveToDisk();
    }

    public void RemoveAlias(Symbol symbol)
    {
        if (aliases.Remove(symbol))
            SaveToDisk();
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
            {
                aliases = data
                    .Select(pair => Symbol.TryCreate(pair.Key, out var symbol)
                        ? new KeyValuePair<Symbol, string>?(
                            new KeyValuePair<Symbol, string>(symbol, pair.Value))
                        : null)
                    .Where(pair => pair.HasValue)
                    .Select(pair => pair!.Value)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
            }
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

            var json = JsonSerializer.Serialize(aliases.ToDictionary(pair => pair.Key.Value, pair => pair.Value));
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
