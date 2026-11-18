using Freitty.Application.Common;

namespace Freitty.Application.Me.GetMe;

// NeedAttention is the number on the Orders badge of the shell: the Need attention total of the dashboard.
public sealed record MeDto(UserDto User, CompanyDto Company, int NeedAttention);
