namespace Freitty.Application.Orders.Cards;

// The zone travels with the hub: instants are UTC and the page shows them in the hub's local time.
public sealed record CardHubDto(string Name, string ShortName, string TimeZone);
