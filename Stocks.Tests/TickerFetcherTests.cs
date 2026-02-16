using System.Reflection;
using System.Runtime.CompilerServices;
using System.Net;
using System.Text.Json;
using Stocks.Model;

namespace Stocks.Tests;

public class TickerFetcherTests
{
    private const string Symbol = "SXR8.DE";
    private const string UserAgent = "Mozilla/4.0 (Stocks.Tests)";

    [Test]
    public async Task FetchUsesCorrectUrlForDayRange()
    {
        string? requestUrl = null;
        var client = CreateClientWithJson(CreateChartResponseJson(), request => requestUrl = request.RequestUri?.ToString());
        var sut = CreateFetcher(client);

        await sut.Fetch(Symbol, TickerRange.Day);

        Assert.That(requestUrl, Is.EqualTo("https://query1.finance.yahoo.com/v8/finance/chart/SXR8.DE?range=1d&interval=2m"));
    }

    [Test]
    public async Task FetchReturnsResultFromResponse()
    {
        var client = CreateClientWithJson(CreateChartResponseJson(symbol: "TSLA"));
        var sut = CreateFetcher(client);

        var result = await sut.Fetch(Symbol, TickerRange.Day);

        Assert.That(result.Meta.Symbol, Is.EqualTo("TSLA"));
    }

    [Test]
    public void FetchThrowsTickerFetchFailedExceptionOnHttpFailure()
    {
        var client = CreateClientWithException(new HttpRequestException("boom"));
        var sut = CreateFetcher(client);

        var exception = Assert.ThrowsAsync<TickerFetchFailedException>(async () => await sut.Fetch(Symbol, TickerRange.Day));

        Assert.That(exception!.InnerException, Is.TypeOf<HttpRequestException>());
    }

    [Test]
    public void FetchThrowsTickerFetchFailedExceptionOnInvalidPayload()
    {
        var client = CreateClientWithJson("{\"chart\":{\"result\":null,\"error\":null}}");
        var sut = CreateFetcher(client);

        Assert.ThrowsAsync<TickerFetchFailedException>(async () => await sut.Fetch(Symbol, TickerRange.Day));
    }

    [Test]
    public async Task SearchTickersUsesEscapedQuery()
    {
        Uri? requestUri = null;
        var client = CreateClientWithJson(CreateSearchResponseJson(), request => requestUri = request.RequestUri);
        var sut = CreateFetcher(client);

        await sut.SearchTickers("AAPL two words");

        Assert.That(requestUri, Is.Not.Null);
        Assert.That(requestUri!.Query, Is.EqualTo("?q=AAPL%20two%20words"));
    }

    [Test]
    public async Task SearchTickersReturnsQuotes()
    {
        var client = CreateClientWithJson(CreateSearchResponseJson(symbol: "TSLA"));
        var sut = CreateFetcher(client);

        var results = await sut.SearchTickers("Tesla");

        Assert.That(results.First().Symbol, Is.EqualTo("TSLA"));
    }

    [Test]
    public async Task SearchTickersReturnsEmptyListOnFailure()
    {
        var client = CreateClientWithException(new HttpRequestException("boom"));
        var sut = CreateFetcher(client);

        var results = await sut.SearchTickers("Tesla");

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task SearchTickersReturnsEmptyListOnInvalidPayload()
    {
        var client = CreateClientWithJson("{\"quotes\":null}");
        var sut = CreateFetcher(client);

        var results = await sut.SearchTickers("Tesla");

        Assert.That(results, Is.Empty);
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };
    }

    private static HttpClient CreateClientWithJson(string json, Action<HttpRequestMessage>? captureRequest = null)
    {
        return new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            captureRequest?.Invoke(request);
            return Task.FromResult(CreateJsonResponse(json));
        }));
    }

    private static HttpClient CreateClientWithException(Exception exception)
    {
        return new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(exception)));
    }

    private static TickerFetcher CreateFetcher(HttpClient client, string userAgent = UserAgent)
    {
        client.DefaultRequestHeaders.Add("user-agent", userAgent);

        var fetcher = (TickerFetcher)RuntimeHelpers.GetUninitializedObject(typeof(TickerFetcher));
        SetPrivateField(fetcher, "baseUrl", "https://query1.finance.yahoo.com/v8/finance/chart/");
        SetPrivateField(fetcher, "client", client);
        return fetcher;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on {target.GetType().Name}.");
        field!.SetValue(target, value);
    }

    private static string CreateSearchResponseJson(
        string symbol = "AAPL",
        string shortName = "Apple",
        string longName = "Apple Inc",
        string exchange = "NASDAQ")
    {
        var response = new YahooSearchResults(
        [
            new SearchResult(shortName, symbol, longName, exchange)
        ]);

        return JsonSerializer.Serialize(response);
    }

    private static string CreateChartResponseJson(string symbol = Symbol)
    {
        var response = new ChartResponse(
            new ChartData(
            [
                new Result(
                    new Meta(
                        Currency: "EUR",
                        Symbol: symbol,
                        ExchangeName: "GER",
                        FullExchangeName: "XETRA",
                        InstrumentType: "ETF",
                        FirstTradeDate: 1,
                        RegularMarketTime: 2,
                        Gmtoffset: 0,
                        Timezone: "UTC",
                        ExchangeTimezoneName: "UTC",
                        RegularMarketPrice: 100,
                        PreviousClose: 99,
                        ChartPreviousClose: 99,
                        RegularMarketDayHigh: 101,
                        RegularMarketDayLow: 98,
                        RegularMarketVolume: 10,
                        LongName: "Name",
                        ShortName: "Short",
                        PriceHint: 2,
                        Scale: 0,
                        HasPrePostMarketData: false,
                        CurrentTradingPeriod: null,
                        TradingPeriods: null,
                        DataGranularity: "1d",
                        Range: "1d",
                        ValidRanges: ["1d", "5d"]),
                    Timestamp: [1],
                    Indicators: new Indicators(
                        Quote:
                        [
                            new Quote(
                                Open: [1d],
                                High: [1d],
                                Low: [1d],
                                Close: [1d],
                                Volume: [1])
                        ],
                        AdjClose: null))
            ],
            Error: null));

        return JsonSerializer.Serialize(response);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            this.responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return responder(request, cancellationToken);
        }
    }
}
