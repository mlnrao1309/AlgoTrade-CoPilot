using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlgoTrading.DataAccess.Infrastructure.DatabaseContext;
using AlgoTrading.Models;
using AlgoTrading.Models.MarketData.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlgoTrading.DataAccess.Infrastructure
{
    /// <summary>Configuration surface consumed at startup and by the hosted pipeline.</summary>
    public interface IPlatformConfigurationProvider
    {
        Task<TimeframeCatalog> GetTimeframeCatalogAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ConfigCriticalLevel>> GetCriticalLevelRulesAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Loads Config_Timeframes and Config_CriticalLevels. When the tables are empty or unreachable the caller gets
    /// the blueprint defaults instead of a silent empty pipeline.
    /// </summary>
    public sealed class PlatformConfigurationProvider : IPlatformConfigurationProvider
    {
        private readonly ApplicationDbContext context;

        public PlatformConfigurationProvider(ApplicationDbContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<TimeframeCatalog> GetTimeframeCatalogAsync(CancellationToken cancellationToken = default)
        {
            List<ConfigTimeframe> rows = await this.context.ConfigTimeframes.AsNoTracking().ToListAsync(cancellationToken);
            if (rows.Count == 0)
            {
                return TimeframeCatalog.CreateDefault();
            }

            List<TimeframeOption> options = new List<TimeframeOption>();
            for (int index = 0; index < rows.Count; index++)
            {
                ConfigTimeframe row = rows[index];
                TimeframeSourceStream stream = row.SourceStream == "DAILY_1D"
                    ? TimeframeSourceStream.DailyOneDay
                    : TimeframeSourceStream.IntradayFiveMinute;
                string canonicalName = TimeframeNaming.DeriveCanonicalName(row.TimeframeCode);
                options.Add(new TimeframeOption(row.TimeframeCode, canonicalName, row.MinutesMultiplier, stream, row.IsActive));
            }

            return new TimeframeCatalog(options);
        }

        public async Task<IReadOnlyList<ConfigCriticalLevel>> GetCriticalLevelRulesAsync(CancellationToken cancellationToken = default)
        {
            List<ConfigCriticalLevel> rows = await this.context.ConfigCriticalLevels.AsNoTracking()
                .Where(row => row.IsActive)
                .ToListAsync(cancellationToken);
            return rows.AsReadOnly();
        }
    }
}
