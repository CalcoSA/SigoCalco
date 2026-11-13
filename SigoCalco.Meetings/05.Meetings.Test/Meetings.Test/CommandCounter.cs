using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Meetings.Test;

public sealed class CommandCounter : DbCommandInterceptor
{
    public int Count { get; private set; }
    public void Reset() => Count = 0;
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Count++;
        return ValueTask.FromResult(result);
    }
}

