using FastEndpoints;
using Freitty.Domain.Common;

namespace Freitty.Application.Orders.SubmitOrder;

// The draft is named by its draft number. ExpectedVersion is the version the caller saw, with the rule of recording
// an operation: a stale board, a double click and a retry are refused instead of submitting twice.
public sealed record SubmitOrderCommand(string OrderNumber, int ExpectedVersion)
    : ICommand<Result<SubmittedOrderDto>>;
