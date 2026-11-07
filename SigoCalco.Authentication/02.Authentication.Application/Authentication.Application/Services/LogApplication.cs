using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Application.Services
{
    public class LogApplication : ILogApplication
    {
        private readonly ILogRepository _logRepository;

        public LogApplication(ILogRepository logRepository)
        {
            _logRepository = logRepository;
        }

        public async Task<long> CreateLog(CreateLogDto request)
        {
            if (request is null ||
                string.IsNullOrWhiteSpace(request.Action) ||
                string.IsNullOrWhiteSpace(request.Module) ||
                string.IsNullOrWhiteSpace(request.Description) ||
                string.IsNullOrWhiteSpace(request.UserName))
            {
                return 0;
            }

            var log = new AuthenticationLog
            {
                Action = request.Action.Trim(),
                Module = request.Module.Trim(),
                Description = request.Description.Trim(),
                UserName = request.UserName.Trim()
            };

            return await _logRepository.CreateLog(log);
        }

        public async Task<PagedDto<LogDto>?> GetPaged(
            DateTime? from,
            DateTime? to,
            int page,
            int take)
        {
            if (page <= 0)
                page = 1;

            if (take <= 0)
                take = 10;

            if (take > 100)
                take = 100;

            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
                return null;

            var logs = await _logRepository.GetPaged(from, to, page, take);

            return new PagedDto<LogDto>
            {
                Items = logs.Items
                    .Select(log => new LogDto
                    {
                        LogId = log.LogId,
                        Action = log.Action,
                        Module = log.Module,
                        Description = log.Description,
                        UserName = log.UserName,
                        CreatedAt = log.CreatedAt
                    })
                    .ToList(),
                Total = logs.Total,
                Page = logs.Page,
                Take = logs.Take,
                Pages = logs.Pages
            };
        }
    }
}
