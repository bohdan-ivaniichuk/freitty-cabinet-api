using System.Globalization;
using Freitty.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Infrastructure.Persistence;

// A UNION of order ids: each branch is served by its own trigram index, where "number ILIKE … OR EXISTS (…)" would
// read every order of the company. A sub-order label typed in full (FR001676-2) is found by the third branch.
internal sealed class OrderSearch(AppDbContext db) : IOrderSearch
{
    private const string EscapeCharacter = "\\";
    private const char LabelSeparator = '-';

    public IQueryable<Guid> MatchingOrderIds(Guid companyId, string searchText)
    {
        string pattern = $"%{Escape(searchText)}%";
        IQueryable<Guid> matchingIds = NumberMatches(companyId, pattern)
            .Union(RefNumberMatches(companyId, pattern));

        return TryParseLabel(searchText, out string orderNumber, out int position)
            ? matchingIds.Union(LabelMatches(companyId, Escape(orderNumber), position))
            : matchingIds;
    }

    // The two-argument ILike has no escape character at all, so the text's own wildcards are escaped here and the
    // escape character is passed explicitly.
    private static string Escape(string text)
    {
        return text
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);
    }

    // "<number>-<position>", split at the last hyphen. Digits that do not fit an int are no position.
    private static bool TryParseLabel(string text, out string orderNumber, out int position)
    {
        int separatorIndex = text.LastIndexOf(LabelSeparator);
        orderNumber = separatorIndex > 0 ? text[..separatorIndex] : string.Empty;
        position = 0;
        return separatorIndex > 0
            && int.TryParse(
                text.AsSpan(separatorIndex + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out position);
    }

    private IQueryable<Guid> NumberMatches(Guid companyId, string pattern)
    {
        return db.Orders
            .Where(order => order.CompanyId == companyId
                && EF.Functions.ILike(order.Number, pattern, EscapeCharacter))
            .Select(order => order.Id);
    }

    private IQueryable<Guid> RefNumberMatches(Guid companyId, string pattern)
    {
        return db.Orders
            .Where(order => order.CompanyId == companyId)
            .SelectMany(order => order.Shipments)
            .Where(shipment => EF.Functions.ILike(shipment.RefNumber, pattern, EscapeCharacter))
            .Select(shipment => shipment.OrderId);
    }

    // An ILIKE without a wildcard is a case-insensitive equality.
    private IQueryable<Guid> LabelMatches(Guid companyId, string exactNumber, int position)
    {
        return db.Orders
            .Where(order => order.CompanyId == companyId
                && EF.Functions.ILike(order.Number, exactNumber, EscapeCharacter)
                && order.Shipments.Any(shipment => shipment.Position == position))
            .Select(order => order.Id);
    }
}
