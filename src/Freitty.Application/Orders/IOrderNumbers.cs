namespace Freitty.Application.Orders;

// The official numbers an order gets when it is submitted. Unique and increasing across the platform, so
// they come from the database; a number taken by a submit whose save then fails is not given out again.
public interface IOrderNumbers
{
    Task<string> NextAsync(CancellationToken cancellationToken);
}
