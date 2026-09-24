using AlgoTrading.DataAccess.Data;
using AlgoTrading.DataAccess.Entities;
using AlgoTrading.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AlgoTrading.DataAccess.Repositories
{
    public class CandleRepository : ICandleRepository
    {
        private readonly AlgoTradingDbContext _db;
        private const int DefaultLimit = 1000;

        public CandleRepository(AlgoTradingDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        //public async Task<List<Candle>> GetCandlesAsync(uint instrumentToken, string reqTimeFrame, CancellationToken cancellationToken = default)
        //{   
        //    string getTimeFrame = reqTimeFrame switch
        //    {
        //        "D" => "day",
        //        "W" => "week",
        //        "M" => "month",
        //        _ => "minute"
        //    };
        //    var query = _db.Candles.AsNoTracking().Where(c => c.InstrumentToken == instrumentToken && c.Timeframe == getTimeFrame).
        //        Select(e => new Candle((int)instrumentToken, e.Timeframe, e.Timestamp, e.Open, e.High, e.Low, e.Close, e.Volume));

        //    var baseCandles = await query.ToListAsync(cancellationToken);

        //    var aggregated = baseCandles
        //         .GroupBy(c =>
        //         {
        //             var ticks = c.Timestamp.Ticks;
        //             var fiveMinuteTicks = TimeSpan.FromMinutes(Convert.ToInt32(reqTimeFrame)).Ticks;
        //             return new DateTime(ticks - (ticks % fiveMinuteTicks), c.Timestamp.Kind);
        //         })
        //         .Select(group =>
        //         {
        //             var orderedGroup = group.OrderBy(c => c.Timestamp).ToList();
        //             return new Candle(
        //                 (int)instrumentToken,
        //                 reqTimeFrame,
        //                 group.Key,
        //                 orderedGroup.First().Open,
        //                 group.Max(c => c.High),
        //                 group.Min(c => c.Low),
        //                 orderedGroup.Last().Close,
        //                 (ulong)group.Sum(g => (long)g.Volume) // Handled cleanly via casting to prevent ulong overflow errors during sum
        //             );
        //         })
        //         .OrderBy(c => c.Timestamp)
        //         .ToList();

        //    return aggregated;
        //}



        public async Task<List<Candle>> GetCandlesAsync(uint instrumentToken, string timeframeMinutes = "minute", DateTime? from = null, DateTime? to = null, int? limit = null, CancellationToken cancellationToken = default)
        {
            if (from.HasValue && to.HasValue && from > to) throw new ArgumentException("from must be <= to");

            var query = _db.Candles.AsNoTracking().Where(c => c.InstrumentToken == instrumentToken && c.Timeframe == timeframeMinutes);

            if (from.HasValue)
            {
                var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
                query = query.Where(c => c.Timestamp >= fromUtc);
            }

            if (to.HasValue)
            {
                var toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);
                query = query.Where(c => c.Timestamp <= toUtc);
            }

            // If limit provided, take the most recent 'limit' rows
            if (limit.HasValue && limit.Value > 0)
            {
                var list = await query.OrderByDescending(c => c.Timestamp)
                    .Take(limit.Value)
                    .Select(e => new Candle((int)instrumentToken, e.Timeframe, e.Timestamp, e.Open, e.High, e.Low, e.Close, e.Volume))                    //InstrumentToken = e.InstrumentToken, Timeframe = e.Timeframe, Timestamp = e.Timestamp, Open = (decimal)e.Open, High = (decimal)e.High, 
                    .ToListAsync(cancellationToken);

                list.Reverse();

                return new List<Candle>();
            }

            // No explicit limit - if no date filters, apply default limit to avoid huge scans
            if (!from.HasValue && !to.HasValue)
            {
                var list = await query.OrderByDescending(c => c.Timestamp)
                    .Take(DefaultLimit)
                    .Select(e => new Candle((int)instrumentToken, e.Timeframe, e.Timestamp, e.Open, e.High, e.Low, e.Close, e.Volume))
                    .ToListAsync(cancellationToken);

                list.Reverse();
                return list;
            }

            // Date range provided and no explicit limit - return all matching rows (could be limited by DB)
            return await query.OrderBy(c => c.Timestamp)
                .Select(e => new Candle((int)e.InstrumentToken, e.Timeframe, e.Timestamp, e.Open, e.High, e.Low, e.Close, e.Volume))
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Candle candle, CancellationToken cancellationToken = default)
        {
            var entity = new CandleEntity
            {
                InstrumentToken = candle.InstrumentToken,
                Timeframe = candle.TimeframeMinutes,
                Timestamp = DateTime.SpecifyKind(candle.Timestamp, DateTimeKind.Utc),
                Open = candle.Open,
                High = candle.High,
                Low = candle.Low,
                Close = candle.Close,
                Volume = candle.Volume
            };

            _db.Candles.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(Candle candle, CancellationToken cancellationToken = default)
        {
            var entity = await _db.Candles.FirstOrDefaultAsync(c => c.InstrumentToken == candle.InstrumentToken && c.Timeframe == candle.TimeframeMinutes && c.Timestamp == candle.Timestamp, cancellationToken);
            if (entity == null) throw new InvalidOperationException("Candle not found");

            entity.Open = candle.Open;
            entity.High = candle.High;
            entity.Low = candle.Low;
            entity.Close = candle.Close;
            entity.Volume = candle.Volume;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(uint instrumentToken, string timeframeMinutes, DateTime timestamp, CancellationToken cancellationToken = default)
        {
            var entity = await _db.Candles.FirstOrDefaultAsync(c => c.InstrumentToken == instrumentToken && c.Timeframe == timeframeMinutes && c.Timestamp == timestamp, cancellationToken);
            if (entity == null) return;
            _db.Candles.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
