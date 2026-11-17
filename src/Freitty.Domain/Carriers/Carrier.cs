using Freitty.Domain.Common;

namespace Freitty.Domain.Carriers;

public sealed class Carrier
{
    public Carrier(string name, string? phone)
    {
        Id = Guid.CreateVersion7();
        Name = Guard.Text(name);
        Phone = phone is null ? null : Guard.Text(phone);
    }

    private Carrier()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Phone { get; private set; }
}
