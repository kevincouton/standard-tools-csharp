using StandardTools.Core;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/v1/market-data/bars", (
    string symbol,
    DateOnly startDate,
    DateOnly endDate,
    string interval,
    string? exchange,
    string? provider) =>
{
    try
    {
        var ticker = new Ticker(symbol, exchange);
        var range = new DateRange(startDate, endDate);
        var barInterval = ParseBarInterval(interval);
        return Results.Ok(new { ticker, range, interval = barInterval, provider });
    }
    catch (InvalidCommandException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();

static BarInterval ParseBarInterval(string interval)
{
    return interval.ToUpperInvariant() switch
    {
        "DAILY" or "D" => BarInterval.Daily,
        "WEEKLY" or "W" => BarInterval.Weekly,
        "MONTHLY" or "M" => BarInterval.Monthly,
        _ => throw new InvalidCommandException($"Unknown interval: {interval}")
    };
}

public partial class Program { }
