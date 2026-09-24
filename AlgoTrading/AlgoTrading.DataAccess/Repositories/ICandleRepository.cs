using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AlgoTrading.Models;

namespace AlgoTrading.DataAccess.Repositories
{
    public interface ICandleRepository
    {
        
        Task<List<Candle>> GetCandlesAsync(uint instrumentToken, string timeframeMinutes, DateTime? from = null, DateTime? to = null, int? limit = null, CancellationToken cancellationToken = default);

        // Basic CRUD signatures for completeness
        Task AddAsync(Candle candle, CancellationToken cancellationToken = default);
        Task UpdateAsync(Candle candle, CancellationToken cancellationToken = default);
        Task DeleteAsync(uint instrumentToken, string timeframeMinutes, DateTime timestamp, CancellationToken cancellationToken = default);
    }
}
