using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Domain.Billing;

// A separate aggregate. It has no company of its own: the paying company is the one of its order.
public sealed class Invoice
{
    private Invoice(string number, Guid orderId, decimal amount, DateTimeOffset issuedAt)
    {
        Id = Guid.CreateVersion7();
        Number = number;
        OrderId = orderId;
        Amount = amount;
        IssuedAt = issuedAt;
    }

    private Invoice()
    {
    }

    public Guid Id { get; private set; }

    public string Number { get; private set; } = null!;

    public Guid OrderId { get; private set; }

    public decimal Amount { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public Order Order { get; private set; } = null!;

    public static Result<Invoice> Issue(string number, Order order, decimal amount, DateTimeOffset now)
    {
        string trimmedNumber = Guard.Text(number);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        if (order.Status == OrderStatus.Draft)
        {
            return InvoiceErrors.OrderIsDraft(order.Number);
        }

        return new Invoice(trimmedNumber, order.Id, amount, StoredInstant.From(now));
    }

    public Result MarkPaid(DateTimeOffset now)
    {
        DateTimeOffset instant = StoredInstant.From(now);
        ArgumentOutOfRangeException.ThrowIfLessThan(instant, IssuedAt, nameof(now));

        if (PaidAt is not null)
        {
            return InvoiceErrors.AlreadyPaid(Number);
        }

        PaidAt = instant;
        return Result.Success();
    }
}
