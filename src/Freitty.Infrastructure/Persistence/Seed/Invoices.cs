using System.Globalization;
using Freitty.Domain.Billing;
using Freitty.Domain.Orders;

namespace Freitty.Infrastructure.Persistence.Seed;

// Invoices are numbered in the order they were issued, across the scripted orders and the history together.
internal static class Invoices
{
    // The model has no rates: the price of handling a unit is a figure of the sample data only.
    private const decimal BillableUnitRate = 14.00m;
    private const int FirstNumber = 1751;
    private const string NumberFormat = "{0:D6}";

    public static IReadOnlyList<Invoice> Issue(IEnumerable<OrderScript> scripts)
    {
        return scripts
            .Select(script => script.DueInvoice)
            .OfType<DueInvoice>()
            .OrderBy(due => due.IssuedAt)
            .ThenBy(due => due.Order.Number, StringComparer.Ordinal)
            .Select((due, position) => Issue(due, FirstNumber + position))
            .ToList();
    }

    private static Invoice Issue(DueInvoice due, int number)
    {
        string invoiceNumber = string.Format(CultureInfo.InvariantCulture, NumberFormat, number);
        Invoice invoice = Invoice.Issue(invoiceNumber, due.Order, AmountOf(due.Order), due.IssuedAt).OrThrow();
        if (due.PaidAt is { } paidAt)
        {
            invoice.MarkPaid(paidAt).OrThrow();
        }

        return invoice;
    }

    private static decimal AmountOf(Order order)
    {
        int billableUnits = order.Shipments
            .SelectMany(shipment => shipment.Operations)
            .Where(operation => operation.IsBillable)
            .Sum(operation => operation.Quantity);
        return order.SupplySubtotal + (billableUnits * BillableUnitRate);
    }
}
