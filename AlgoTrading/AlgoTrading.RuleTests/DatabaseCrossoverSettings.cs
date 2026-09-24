using System.IO;
using System.Text.Json;

internal sealed class DatabaseCrossoverSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public int InstrumentToken { get; set; }
    public string Timeframe { get; set; } = string.Empty;
    public int CandleLengthMinutes { get; set; }
    public string TimestampTimeZoneId { get; set; } = string.Empty;
    public bool TimestampRepresentsCandleOpen { get; set; }

    internal static DatabaseCrossoverSettings Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "database-test.settings.json");
        DatabaseCrossoverSettings settings = JsonSerializer.Deserialize<DatabaseCrossoverSettings>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("Database test settings are empty.");
        if (string.IsNullOrWhiteSpace(settings.ConnectionString) || string.IsNullOrWhiteSpace(settings.Timeframe)
            || string.IsNullOrWhiteSpace(settings.TimestampTimeZoneId) || settings.CandleLengthMinutes <= 0)
            throw new InvalidOperationException("The database test requires a connection string, timeframe, candle duration and timestamp timezone.");
        return settings;
    }
}
