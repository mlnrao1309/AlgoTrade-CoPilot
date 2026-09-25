# NSE calendar import and runtime selection

`NseTradingCalendarImporter` downloads the official NSE holiday-master JSON, preserves the original response,
expands the requested year into one `ExchangeCalendarDay` per date, then publishes the immutable revision through
`ITradingCalendarRepository`.

Special sessions are deliberately explicit. Construct each `NseSpecialSession` from an official NSE circular and
include its original `CalendarSourceDocument` in `NseCalendarImportRequest`. The importer saves each distinct source
before it publishes the revision. An NSE holiday description containing `*` fails closed unless a matching special
session with explicit open and close times is supplied; the system never guesses Muhurat or disaster-recovery hours.

```csharp
var importer = new NseTradingCalendarImporter(
    new NseHolidaySourceClient(httpClient,
        new Uri("https://www.nseindia.com/api/holiday-master?type=trading")),
    new NseTradingCalendarParser(), repository);

await importer.ImportAsync(new NseCalendarImportRequest(
    "CM", 2026, "NSE-CM-2026-v1",
    new TimeOnly(9, 15), new TimeOnly(15, 30),
    officialSpecialSessions));
```

At runtime, `RepositoryTradingCalendarProvider` resolves the configured `TradingCalendarSelection` by exchange,
segment, revision and `asOf`, and materializes instrument-specific sessions. `App.xaml.cs` registers the SQL
repository, provider and the `TradingCalendar` selection from `appsettings.json`.

The focused regression coverage is in `AlgoTrading.RuleTests/CalendarImportTests.cs`.
