using System.Runtime.CompilerServices;

namespace Freitty.Domain.Common;

// Guards protect the calling contract: a value that breaks one comes from a validator or a clock, not from a
// decision of the user, so it is an exception and not a Result.
internal static class Guard
{
    public static string Text(string value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim();
    }

    // Trailers are counted by distinct number, so "ab 12 " and "AB 12" must be the same trailer.
    public static string VehicleNumber(
        string value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        return Text(value, paramName).ToUpperInvariant();
    }

    public static TEnum Defined<TEnum>(TEnum value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{value} is not a member of {typeof(TEnum).Name}.");
        }

        return value;
    }
}
