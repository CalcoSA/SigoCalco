using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Meetings.Test;

public sealed class CommandCounter : DbCommandInterceptor
{
    public int Count { get; private set; }
    public List<string> Sql { get; } = [];
    public void Reset() { Count = 0; Sql.Clear(); }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Count++;
        Sql.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }
}

