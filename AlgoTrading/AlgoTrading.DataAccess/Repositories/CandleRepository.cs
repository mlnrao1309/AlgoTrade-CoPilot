using AlgoTrading.DataAccess.Data;
using AlgoTrading.DataAccess.Entities;
using AlgoTrading.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AlgoTrading.DataAccess.Repositories
{
    public class CandleRepository : ICandleRepository
    {
        private readonly AlgoTradingDbContext database;
        private const int DefaultLimit = 1000;

        public CandleRepository(AlgoTradingDbContext db)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            this.database = db;
        }

        public async Task<List<Candle>> GetCandlesAsync(uint instrumentToken, string timeframeMinutes = "minute", DateTime? from = null, DateTime? to = null, int? limit = null, CancellationToken cancellationToken = default)
        {
            IQueryable<CandleEntity> query = from entity in this.database.Candles.AsNoTracking()
                                            where entity.InstrumentToken == instrumentToken && entity.Timeframe == timeframeMinutes
                                            select entity;
            DateTime? firstTimestamp = null;
            DateTime? lastTimestamp = null;
            if (from.HasValue)
            {
                firstTimestamp = MarketTimestamp.ToDatabase(MarketTimestamp.FromDateTime(from.Value));
                DateTime fromDatabase = firstTimestamp.Value;
                query = from entity in query where entity.Timestamp >= fromDatabase select entity;
            }
            if (to.HasValue)
            {
                lastTimestamp = MarketTimestamp.ToDatabase(MarketTimestamp.FromDateTime(to.Value));
                DateTime toDatabase = lastTimestamp.Value;
                query = from entity in query where entity.Timestamp <= toDatabase select entity;
            }
            if (firstTimestamp.HasValue && lastTimestamp.HasValue && firstTimestamp.Value > lastTimestamp.Value)
            {
                throw new ArgumentException("from must be <= to");
            }

            int maximumRows = 0;
            if (limit.HasValue && limit.Value > 0)
            {
                maximumRows = limit.Value;
            }
            else if (!from.HasValue && !to.HasValue)
            {
                maximumRows = DefaultLimit;
            }

            List<CandleEntity> rows;
            if (maximumRows > 0)
            {
                query = from entity in query orderby entity.Timestamp descending select entity;
                rows = await query.Take(maximumRows).ToListAsync(cancellationToken);
                rows.Reverse();
            }
            else
            {
                query = from entity in query orderby entity.Timestamp select entity;
                rows = await query.ToListAsync(cancellationToken);
            }

            List<Candle> candles = new List<Candle>();
            foreach (CandleEntity row in rows)
            {
                candles.Add(new Candle(row.InstrumentToken, row.Timeframe, row.Timestamp,
                    row.Open, row.High, row.Low, row.Close, row.Volume));
            }
            return candles;
        }

        public async Task AddAsync(Candle candle, CancellationToken cancellationToken = default)
        {
            CandleEntity entity = new CandleEntity();
            entity.InstrumentToken = candle.InstrumentToken;
            entity.Timeframe = candle.TimeframeMinutes;
            entity.Timestamp = MarketTimestamp.ToDatabase(candle.OpenedAt);
            entity.Open = candle.Open;
            entity.High = candle.High;
            entity.Low = candle.Low;
            entity.Close = candle.Close;
            entity.Volume = candle.Volume;
            this.database.Candles.Add(entity);
            await this.database.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(Candle candle, CancellationToken cancellationToken = default)
        {
            DateTime databaseTimestamp = MarketTimestamp.ToDatabase(candle.OpenedAt);
            IQueryable<CandleEntity> query = from row in this.database.Candles
                                            where row.InstrumentToken == candle.InstrumentToken
                                                && row.Timeframe == candle.TimeframeMinutes && row.Timestamp == databaseTimestamp
                                            select row;
            CandleEntity? entity = await query.FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                throw new InvalidOperationException("Candle not found");
            }
            entity.Open = candle.Open;
            entity.High = candle.High;
            entity.Low = candle.Low;
            entity.Close = candle.Close;
            entity.Volume = candle.Volume;
            await this.database.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(uint instrumentToken, string timeframeMinutes, DateTime timestamp, CancellationToken cancellationToken = default)
        {
            DateTime databaseTimestamp = MarketTimestamp.ToDatabase(MarketTimestamp.FromDateTime(timestamp));
            IQueryable<CandleEntity> query = from row in this.database.Candles
                                            where row.InstrumentToken == instrumentToken
                                                && row.Timeframe == timeframeMinutes && row.Timestamp == databaseTimestamp
                                            select row;
            CandleEntity? entity = await query.FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                return;
            }
            this.database.Candles.Remove(entity);
            await this.database.SaveChangesAsync(cancellationToken);
        }
    }
}
