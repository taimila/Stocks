// SPDX-FileCopyrightText: 2026 Lauri Taimila
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json.Serialization;

namespace Stocks.Model;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "StringDictionary")]
[JsonSerializable(typeof(WatchlistState))]
[JsonSerializable(typeof(ChartResponse))]
[JsonSerializable(typeof(YahooSearchResults))]
internal partial class StocksJsonContext : JsonSerializerContext
{
}
