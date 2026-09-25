SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.TradingCalendarSource', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TradingCalendarSource
    (
        SourceId uniqueidentifier NOT NULL CONSTRAINT PK_TradingCalendarSource PRIMARY KEY,
        ExchangeCode nvarchar(16) NOT NULL,
        SourceUri nvarchar(2048) NOT NULL,
        RetrievedAt datetimeoffset(7) NOT NULL,
        ContentType nvarchar(128) NOT NULL,
        ContentSha256 char(64) NOT NULL,
        Content nvarchar(max) NOT NULL
    );
END;
IF OBJECT_ID(N'dbo.TradingCalendarRevision', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TradingCalendarRevision
    (
        CalendarId uniqueidentifier NOT NULL CONSTRAINT PK_TradingCalendarRevision PRIMARY KEY,
        ExchangeCode nvarchar(16) NOT NULL,
        SegmentCode nvarchar(32) NOT NULL,
        Revision nvarchar(80) NOT NULL,
        FirstDate date NOT NULL,
        LastDate date NOT NULL,
        SourceId uniqueidentifier NOT NULL,
        RecordedAt datetimeoffset(7) NOT NULL CONSTRAINT DF_TradingCalendarRevision_RecordedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT UQ_TradingCalendarRevision UNIQUE (ExchangeCode, SegmentCode, Revision),
        CONSTRAINT CK_TradingCalendarRevision_Dates CHECK (LastDate >= FirstDate),
        CONSTRAINT FK_TradingCalendarRevision_Source FOREIGN KEY (SourceId) REFERENCES dbo.TradingCalendarSource(SourceId)
    );
END;
IF OBJECT_ID(N'dbo.TradingCalendarDay', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TradingCalendarDay
    (
        CalendarId uniqueidentifier NOT NULL,
        TradingDate date NOT NULL,
        OpensAt time(7) NULL,
        ClosesAt time(7) NULL,
        Reason nvarchar(256) NOT NULL,
        CONSTRAINT PK_TradingCalendarDay PRIMARY KEY (CalendarId, TradingDate),
        CONSTRAINT FK_TradingCalendarDay_Revision FOREIGN KEY (CalendarId) REFERENCES dbo.TradingCalendarRevision(CalendarId),
        CONSTRAINT CK_TradingCalendarDay_Times CHECK
        ((OpensAt IS NULL AND ClosesAt IS NULL) OR
         (OpensAt IS NOT NULL AND ClosesAt IS NOT NULL AND OpensAt < ClosesAt))
    );
END;
COMMIT TRANSACTION;
