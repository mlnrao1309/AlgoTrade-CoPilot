using Microsoft.EntityFrameworkCore;
using AlgoTrading.Models.MarketData.Entities;

namespace AlgoTrading.DataAccess
{
    namespace Infrastructure.DatabaseContext
    {
        public class ApplicationDbContext : DbContext
        {
            public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
                : base(options) { }

            public DbSet<InstrumentEq> InstrumentsEq => Set<InstrumentEq>();
            public DbSet<InstrumentFo> InstrumentsFo => Set<InstrumentFo>();
            public DbSet<CriticalLevel> CriticalLevels => Set<CriticalLevel>();
            public DbSet<IngestionSyncState> IngestionSyncStates => Set<IngestionSyncState>();
            public DbSet<ConfigTimeframe> ConfigTimeframes => Set<ConfigTimeframe>();
            public DbSet<ConfigCriticalLevel> ConfigCriticalLevels => Set<ConfigCriticalLevel>();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

                // Configure Instruments_EQ (Composite Primary Key)
                modelBuilder.Entity<InstrumentEq>(entity =>
                {
                    entity.ToTable("Instruments_EQ", "dbo");

                    entity.HasKey(e => new { e.InstrumentToken, e.Exchange })
                          .HasName("PK_Instruments_EQ");

                    entity.Property(e => e.Id).HasColumnName("ID").IsRequired();
                    entity.Property(e => e.InstrumentToken).HasColumnName("InstrumentToken").IsRequired();
                    entity.Property(e => e.TradingSymbol).HasColumnName("TradingSymbol").IsRequired();
                    entity.Property(e => e.Segment).HasColumnName("Segment").IsRequired();
                    entity.Property(e => e.Exchange).HasColumnName("Exchange").IsRequired();
                    entity.Property(e => e.LotSize).HasColumnName("LotSize").IsRequired();
                });

                // Configure Instruments_FO (Single Primary Key)
                modelBuilder.Entity<InstrumentFo>(entity =>
                {
                    entity.ToTable("Instruments_FO", "dbo");

                    entity.HasKey(e => e.InstrumentToken)
                          .HasName("PK_Instruments_FO");

                    entity.Property(e => e.Id).HasColumnName("ID").IsRequired();
                    entity.Property(e => e.InstrumentToken).HasColumnName("InstrumentToken").IsRequired();
                    entity.Property(e => e.TradingSymbol).HasColumnName("TradingSymbol").IsRequired();
                    entity.Property(e => e.Segment).HasColumnName("Segment").IsRequired();
                    entity.Property(e => e.Exchange).HasColumnName("Exchange").IsRequired();
                    entity.Property(e => e.LotSize).HasColumnName("LotSize").IsRequired(false);
                    entity.Property(e => e.Expiry).HasColumnName("expiry").IsRequired();
                });
            }
        }
    }
}
