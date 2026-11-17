using Freitty.Domain.Billing;
using Freitty.Domain.Common;
using Freitty.Domain.Orders;

namespace Freitty.Domain.Tests.Billing;

public sealed class InvoiceTests
{
    private const string InvoiceNumber = "INV-1001";

    [Fact]
    public void InvoiceIsRefusedForADraftOrder()
    {
        Order order = TestOrders.SubmittableDraft();

        Result<Invoice> result = Invoice.Issue(InvoiceNumber, order, 250m, TestOrders.At(1));

        AssertRefusal.Conflict(result.Error, "invoice.order_is_draft");
    }

    [Fact]
    public void InvoiceIsIssuedForASubmittedOrder()
    {
        Order order = TestOrders.Ready();

        Invoice invoice = Invoice.Issue(InvoiceNumber, order, 250m, TestOrders.At(1)).MustSucceed();

        Assert.Equal(order.Id, invoice.OrderId);
        Assert.Equal(InvoiceNumber, invoice.Number);
        Assert.Equal(250m, invoice.Amount);
        Assert.Equal(TestOrders.At(1), invoice.IssuedAt);
        Assert.Null(invoice.PaidAt);
    }

    [Fact]
    public void MarkPaidSetsPaidAt()
    {
        Invoice invoice = Invoice.Issue(InvoiceNumber, TestOrders.Ready(), 250m, TestOrders.At(1)).MustSucceed();

        Result result = invoice.MarkPaid(TestOrders.At(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(TestOrders.At(2), invoice.PaidAt);
    }

    [Fact]
    public void PayingTwiceIsRefused()
    {
        Invoice invoice = Invoice.Issue(InvoiceNumber, TestOrders.Ready(), 250m, TestOrders.At(1)).MustSucceed();
        invoice.MarkPaid(TestOrders.At(2)).MustSucceed();

        Result result = invoice.MarkPaid(TestOrders.At(3));

        AssertRefusal.Conflict(result.Error, "invoice.already_paid");
        Assert.Equal(TestOrders.At(2), invoice.PaidAt);
    }

    [Fact]
    public void InvoiceInstantsAreStoredInUtc()
    {
        var issuedHubLocal = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.FromHours(-5));
        var paidAbroad = new DateTimeOffset(2026, 3, 3, 16, 0, 0, TimeSpan.FromHours(2));
        Invoice invoice = Invoice.Issue(InvoiceNumber, TestOrders.Ready(), 250m, issuedHubLocal).MustSucceed();

        invoice.MarkPaid(paidAbroad).MustSucceed();

        Assert.Equal(issuedHubLocal, invoice.IssuedAt);
        Assert.Equal(TimeSpan.Zero, invoice.IssuedAt.Offset);
        Assert.Equal(paidAbroad, invoice.PaidAt);
        Assert.Equal(TimeSpan.Zero, invoice.PaidAt?.Offset);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyInvoiceNumberIsAContractViolation(string number)
    {
        Order order = TestOrders.Ready();

        Assert.ThrowsAny<ArgumentException>(() => Invoice.Issue(number, order, 250m, TestOrders.At(1)));
    }

    [Fact]
    public void NegativeAmountIsAContractViolation()
    {
        Order order = TestOrders.Ready();

        Assert.ThrowsAny<ArgumentException>(() => Invoice.Issue(InvoiceNumber, order, -0.01m, TestOrders.At(1)));
    }

    [Fact]
    public void PaymentBeforeTheInvoiceWasIssuedIsAContractViolation()
    {
        Invoice invoice = Invoice.Issue(InvoiceNumber, TestOrders.Ready(), 250m, TestOrders.At(5)).MustSucceed();

        Assert.ThrowsAny<ArgumentException>(() => invoice.MarkPaid(TestOrders.At(4)));
    }
}
