using Freitty.Domain.Companies;

namespace Freitty.Application.Common;

public sealed record UserDto(string Name, string Initials, UserRole Role)
{
    // A projection reads the name and the role; projecting User.Initials would make EF Core load the whole user.
    public static UserDto Of(string name, UserRole role)
    {
        return new UserDto(name, User.InitialsOf(name), role);
    }
}
