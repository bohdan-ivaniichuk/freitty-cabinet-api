namespace Freitty.Application.Orders.Cards;

// The invoice is set for AwaitingPayment and Paid only.
public sealed record NextStepDto(NextStepCode Code, string? InvoiceNumber, decimal? Amount);
