SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Persisted critical levels produced by every calculator.
IF OBJECT_ID(N'dbo.CriticalLevels', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CriticalLevels
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_CriticalLevels PRIMARY KEY,
        InstrumentToken bigint NOT NULL,
        TradingSymbol varchar(50) NOT NULL,
        Timeframe varchar(10) NOT NULL,
        LevelType varchar(50) NOT NULL,
        Price decimal(18,4) NOT NULL,
        LevelTimestamp datetime NOT NULL,
        ConfirmedAtTimestamp datetime NOT NULL,
        MethodCode varchar(50) NOT NULL,
        MetaDataJson nvarchar(500) NULL,
        CreatedUtc datetime NOT NULL CONSTRAINT DF_CriticalLevels_CreatedUtc DEFAULT GETUTCDATE(),
        CONSTRAINT CK_CriticalLevels_ConfirmedAfterLevel CHECK (ConfirmedAtTimestamp >= LevelTimestamp)
    );

    CREATE NONCLUSTERED INDEX IX_CriticalLevels_PagedRead
        ON dbo.CriticalLevels (InstrumentToken, Timeframe, LevelTimestamp DESC)
        INCLUDE (TradingSymbol, LevelType, Price, ConfirmedAtTimestamp, MethodCode);
END;

-- 2. Ingestion resumption state; one row per instrument and stream.
IF OBJECT_ID(N'dbo.IngestionSyncState', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IngestionSyncState
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_IngestionSyncState PRIMARY KEY,
        InstrumentToken bigint NOT NULL,
        StreamType varchar(20) NOT NULL,
        LastCompletedChunkStartDate datetime NOT NULL,
        LastCompletedChunkEndDate datetime NOT NULL,
        UpdatedUtc datetime NOT NULL CONSTRAINT DF_IngestionSyncState_UpdatedUtc DEFAULT GETUTCDATE(),
        CONSTRAINT UK_IngestionSyncState UNIQUE (InstrumentToken, StreamType)
    );
END;

-- 3. Timeframe configuration; disabling a row halts that timeframe without a deployment.
IF OBJECT_ID(N'dbo.Config_Timeframes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Config_Timeframes
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Config_Timeframes PRIMARY KEY,
        TimeframeCode varchar(10) NOT NULL,
        MinutesMultiplier int NOT NULL,
        SourceStream varchar(20) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_Config_Timeframes_IsActive DEFAULT 1,
        CONSTRAINT UQ_Config_Timeframes_TimeframeCode UNIQUE (TimeframeCode)
    );
END;

-- 4. Critical level rule configuration.
IF OBJECT_ID(N'dbo.Config_CriticalLevels', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Config_CriticalLevels
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Config_CriticalLevels PRIMARY KEY,
        MethodCode varchar(50) NOT NULL,
        AppliedTimeframe varchar(10) NOT NULL,
        ReferenceTimeframe varchar(10) NULL,
        ParametersJson nvarchar(max) NOT NULL,
        MinimumBarsRequired int NOT NULL CONSTRAINT DF_Config_CriticalLevels_MinimumBars DEFAULT 1,
        IsActive bit NOT NULL CONSTRAINT DF_Config_CriticalLevels_IsActive DEFAULT 1
    );
END;

COMMIT TRANSACTION;
