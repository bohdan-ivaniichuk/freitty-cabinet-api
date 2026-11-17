using Freitty.Domain.Common;

namespace Freitty.Domain.Companies;

public sealed class Company
{
    // Top-ups and payments are outside the task: the balance is a figure kept for display and may be negative.
    public Company(string name, decimal balance)
    {
        Id = Guid.CreateVersion7();
        Name = Guard.Text(name);
        Balance = balance;
    }

    private Company()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public decimal Balance { get; private set; }
}
