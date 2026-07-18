using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace EMS.Infrastructure.Persistence;

/// <summary>
/// SQLite (used as the local/sandbox dev fallback - see Program.cs) cannot translate SUM over decimal columns
/// server-side and throws NotSupportedException; SQL Server has no such restriction. Summing client-side after
/// projecting just the column keeps behavior identical and correct on both providers at demo/report scale.
/// </summary>
public static class QueryExtensions
{
    public static async Task<decimal> SumDecimalAsync<T>(this IQueryable<T> query, Expression<Func<T, decimal>> selector, CancellationToken ct = default)
    {
        var values = await query.Select(selector).ToListAsync(ct);
        return values.Sum();
    }
}
