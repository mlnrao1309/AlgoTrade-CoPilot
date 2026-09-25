internal static class Program
{
    private static async Task<int> Main(string[] arguments)
    {
        try
        {
            await RunAsync(arguments);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            return 1;
        }
    }

    private static async Task RunAsync(string[] arguments)
    {
        if (await DatabaseTestCommand.TryRunAsync(arguments))
        {
            return;
        }

        RuleRegressionTests tests = new RuleRegressionTests();
        tests.Run();
        CandleTimeTests candleTimeTests = new CandleTimeTests();
        candleTimeTests.Run();
        TradingCalendarTests calendarTests = new TradingCalendarTests();
        calendarTests.Run();
        CalendarImportTests importTests = new CalendarImportTests();
        await importTests.RunAsync();
        TimeframeNormalizationTests timeframeTests = new TimeframeNormalizationTests();
        timeframeTests.Run();
        CandleAggregationTests aggregationTests = new CandleAggregationTests();
        aggregationTests.Run();
        CalendarCandleAggregationTests calendarAggregationTests = new CalendarCandleAggregationTests();
        calendarAggregationTests.Run();
        StrategyDependencyTests dependencyTests = new StrategyDependencyTests();
        dependencyTests.Run();
        IndicatorCacheTests cacheTests = new IndicatorCacheTests();
        cacheTests.Run();
        BasicIndicatorCalculatorTests indicatorTests = new BasicIndicatorCalculatorTests();
        indicatorTests.Run();
        IndicatorRecoveryTests recoveryTests = new IndicatorRecoveryTests();
        recoveryTests.Run();
        IntegratedIndicatorTests integratedTests = new IntegratedIndicatorTests();
        integratedTests.Run();
        DailyPivotTests dailyPivotTests = new DailyPivotTests();
        dailyPivotTests.Run();
    }
}

