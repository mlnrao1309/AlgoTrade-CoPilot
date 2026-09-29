SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Eight configured timeframes from the master plan. Re-running only refreshes the metadata.
MERGE dbo.Config_Timeframes AS target
USING (VALUES
    ('5m', 5, 'INTRADAY_5M', 1),
    ('15m', 15, 'INTRADAY_5M', 1),
    ('25m', 25, 'INTRADAY_5M', 1),
    ('60m', 60, 'INTRADAY_5M', 1),
    ('75m', 75, 'INTRADAY_5M', 1),
    ('1D', 0, 'DAILY_1D', 1),
    ('1W', 0, 'DAILY_1D', 1),
    ('1M', 0, 'DAILY_1D', 1)
) AS source (TimeframeCode, MinutesMultiplier, SourceStream, IsActive)
ON target.TimeframeCode = source.TimeframeCode
WHEN MATCHED THEN
    UPDATE SET MinutesMultiplier = source.MinutesMultiplier,
               SourceStream = source.SourceStream
WHEN NOT MATCHED THEN
    INSERT (TimeframeCode, MinutesMultiplier, SourceStream, IsActive)
    VALUES (source.TimeframeCode, source.MinutesMultiplier, source.SourceStream, source.IsActive);

-- Calculator rules. MinimumBarsRequired is the data-sufficiency guard threshold.
MERGE dbo.Config_CriticalLevels AS target
USING (VALUES
    ('PIVOT_STANDARD', '1D', '1D', '{"type":"SQL_RANGE_EXTENSION_V1"}', 2, 1),
    ('EMA_CROSSOVER', '15m', NULL, '{"fastLength":5,"slowLength":20,"function":"EMA"}', 21, 1),
    ('SWING_REVERSAL', '15m', NULL, '{"leftBars":2,"rightBars":2}', 5, 0)
) AS source (MethodCode, AppliedTimeframe, ReferenceTimeframe, ParametersJson, MinimumBarsRequired, IsActive)
ON target.MethodCode = source.MethodCode AND target.AppliedTimeframe = source.AppliedTimeframe
WHEN MATCHED THEN
    UPDATE SET ReferenceTimeframe = source.ReferenceTimeframe,
               ParametersJson = source.ParametersJson,
               MinimumBarsRequired = source.MinimumBarsRequired,
               IsActive = CASE WHEN source.MethodCode = 'SWING_REVERSAL' THEN 0 ELSE target.IsActive END
WHEN NOT MATCHED THEN
    INSERT (MethodCode, AppliedTimeframe, ReferenceTimeframe, ParametersJson, MinimumBarsRequired, IsActive)
    VALUES (source.MethodCode, source.AppliedTimeframe, source.ReferenceTimeframe, source.ParametersJson,
            source.MinimumBarsRequired, source.IsActive);

COMMIT TRANSACTION;
