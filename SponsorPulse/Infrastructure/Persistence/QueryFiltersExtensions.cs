namespace SponsorPulse.Infrastructure.Persistence;

using SponsorPulse.Domain.Entities;

public static class QueryFiltersExtensions
{
    /// <summary>
    /// Set the current user id on the context so global EF Core query filters can apply.
    /// Returns the same context for fluent calls.
    /// </summary>
    public static TContext SetCurrentUser<TContext>(this TContext ctx, Guid? userId)
        where TContext : SponsorPulseDbContext
    {
        ctx.CurrentUserId = userId;
        return ctx;
    }

    /// <summary>
    /// Explicit query filter helper for events (preferred to be explicit in queries).
    /// </summary>
    public static IQueryable<Event> WhereOwnedBy(this IQueryable<Event> query, Guid userId)
    {
        return query.Where(e => e.OwnerId == userId);
    }
}
