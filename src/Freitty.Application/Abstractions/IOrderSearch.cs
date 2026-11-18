namespace Freitty.Application.Abstractions;

// The search is PostgreSQL's ILIKE over trigram indexes, so it is implemented next to the provider. It returns a
// query, not a list: the caller composes it into its own statement.
public interface IOrderSearch
{
    IQueryable<Guid> MatchingOrderIds(Guid companyId, string searchText);
}
