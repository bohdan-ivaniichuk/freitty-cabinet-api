using FastEndpoints;
using Freitty.Domain.Common;

namespace Freitty.Application.Orders.ListPhotos;

// The number is matched as a whole and case-insensitively, within the current company.
public sealed record ListPhotosQuery(string OrderNumber) : ICommand<Result<IReadOnlyList<PhotoDto>>>;
