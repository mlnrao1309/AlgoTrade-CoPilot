-- Read-only, parameterized implementation of the supplied daily formula.
-- Bind @High, @Low, @Close, @OpenQ and @CloseQ as decimal(18,2).
-- Raw qualification uses the SQL expression precision; persisted output uses decimal(18,4).
-- The caller must validate completed, adjacent sessions and next-session activation.
WITH PivotValue AS
(
    SELECT (@High + @Low + @Close) / 3.0 AS PP, @High - @Low AS PriceRange
), Levels AS
(
    SELECT PP,
        2.0 * PP - @Low AS R1, PP + PriceRange AS R2,
        PP + 2.0 * PriceRange AS R3, PP + 3.0 * PriceRange AS R4, PP + 4.0 * PriceRange AS R5,
        2.0 * PP - @High AS S1, PP - PriceRange AS S2,
        PP - 2.0 * PriceRange AS S3, PP - 3.0 * PriceRange AS S4, PP - 4.0 * PriceRange AS S5
    FROM PivotValue
)
SELECT PP,R1,R2,R3,R4,R5,S1,S2,S3,S4,S5,
    CAST(CASE WHEN @OpenQ BETWEEN CASE WHEN PP < R1 THEN PP ELSE R1 END AND CASE WHEN PP < R1 THEN R1 ELSE PP END
        AND @CloseQ BETWEEN CASE WHEN PP < R1 THEN PP ELSE R1 END AND CASE WHEN PP < R1 THEN R1 ELSE PP END
        THEN 1 ELSE 0 END AS bit) AS PPR1,
    CAST(CASE WHEN @OpenQ BETWEEN CASE WHEN S1 < PP THEN S1 ELSE PP END AND CASE WHEN S1 < PP THEN PP ELSE S1 END
        AND @CloseQ BETWEEN CASE WHEN S1 < PP THEN S1 ELSE PP END AND CASE WHEN S1 < PP THEN PP ELSE S1 END
        THEN 1 ELSE 0 END AS bit) AS PPS1
FROM Levels;
