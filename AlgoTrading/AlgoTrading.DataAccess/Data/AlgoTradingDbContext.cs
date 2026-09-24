using System;
using Microsoft.EntityFrameworkCore;
using AlgoTrading.DataAccess.Entities;

namespace AlgoTrading.DataAccess.Data
{
    public class AlgoTradingDbContext : DbContext
    {
        public AlgoTradingDbContext(DbContextOptions<AlgoTradingDbContext> options) : base(options)
        {
        }

        public DbSet<CandleEntity> Candles { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var ce = modelBuilder.Entity<CandleEntity>();
            ce.HasKey(e => e.Id);
            ce.Property(e => e.Timestamp).IsRequired();
            ce.Property(e => e.InstrumentToken).IsRequired();
            ce.Property(e => e.Timeframe).IsRequired();

            // Index to support queries by instrument, timeframe and timestamp
            ce.HasIndex(e => new { e.InstrumentToken, e.Timeframe, e.Timestamp });
        }
    }
}
