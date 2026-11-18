using FastEndpoints;
using Freitty.Domain.Common;

namespace Freitty.Application.Orders.GetPhoto;

// A photo is found only under the number of its own order, within the current company.
public sealed record GetPhotoQuery(string OrderNumber, Guid PhotoId) : ICommand<Result<PhotoContentDto>>;
