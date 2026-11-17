using Freitty.Domain.Common;

namespace Freitty.Domain.Companies;

public sealed class User
{
    private const int InitialsWordCount = 2;

    public User(Guid companyId, string name, UserRole role)
    {
        Guard.Defined(role);
        Id = Guid.CreateVersion7();
        CompanyId = companyId;
        Name = Guard.Text(name);
        Role = role;
    }

    private User()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public string Name { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public Company Company { get; private set; } = null!;

    public string Initials => string.Concat(Name
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Take(InitialsWordCount)
        .Select(word => char.ToUpperInvariant(word[0])));
}
