using Freitty.Domain.Common;

namespace Freitty.Domain.Catalog;

public sealed class Service
{
    public Service(string name)
    {
        Id = Guid.CreateVersion7();
        Name = Guard.Text(name);
    }

    private Service()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;
}
