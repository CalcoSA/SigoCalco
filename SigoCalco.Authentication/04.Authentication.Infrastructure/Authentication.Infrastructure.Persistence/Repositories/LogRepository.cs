using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Repositories
{
    public class LogRepository : ILogRepository
    {
        private readonly SigoCalcoContext _context;

        public LogRepository(SigoCalcoContext context)
        {
            _context = context;
        }

        public async Task<long> CreateLog(AuthenticationLog log)
        {
            await _context.AuthenticationLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            return log.LogId;
        }

        public async Task<PagedDto<AuthenticationLog>> GetPaged(
            DateTime? from,
            DateTime? to,
            int page,
            int take)
        {
            if (page < 1)
                page = 1;

            if (take < 1)
                take = 10;

            var query = _context.AuthenticationLogs
                .AsNoTracking()
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(log => log.CreatedAt >= from.Value);

            if (to.HasValue)
            {
                var endDate = to.Value.Date.AddDays(1);
                query = query.Where(log => log.CreatedAt < endDate);
            }

            var total = await query.CountAsync();

            var logs = await query
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.LogId)
                .Skip((page - 1) * take)
                .Take(take)
                .ToListAsync();

            var pages = total == 0
                ? 0
                : (int)Math.Ceiling(total / (double)take);

            return new PagedDto<AuthenticationLog>
            {
                Items = logs,
                Total = total,
                Page = page,
                Take = take,
                Pages = pages
            };
        }
    }
}
