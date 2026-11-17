using Freitty.Domain.Common;

namespace Freitty.Domain.Billing;

public static class InvoiceErrors
{
    public static DomainError OrderIsDraft(string orderNumber)
    {
        return DomainError.Conflict(
            "invoice.order_is_draft",
            $"Order {orderNumber} is a draft; an invoice is issued only for a submitted order.");
    }

    public static DomainError AlreadyPaid(string invoiceNumber)
    {
        return DomainError.Conflict("invoice.already_paid", $"Invoice {invoiceNumber} is already paid.");
    }
}
