using System.Globalization;

namespace UrgentCare.Web.Session;

/// <summary>Reads a seed typed by the user. Anything but a whole 32-bit number is refused with a reason.</summary>
public static class SeedInput
{
    public static bool TryParse(string? text, out int seed, out string? error)
    {
        if (int.TryParse(text?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out seed))
        {
            error = null;
            return true;
        }

        error = $"The seed must be a whole number between {int.MinValue} and {int.MaxValue}.";
        return false;
    }
}
