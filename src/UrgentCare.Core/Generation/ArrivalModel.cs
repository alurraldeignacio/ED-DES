using UrgentCare.Core.Configuration;

namespace UrgentCare.Core.Generation;

/// <summary>
/// Turns a base hourly arrival rate into the rate in force at a given simulated minute,
/// applying hourly and daily variation when the scenario enables them.
/// </summary>
public sealed class ArrivalModel
{
    private const double MinutesPerHour = 60.0;
    private const double MinutesPerDay = 1440.0;
    private const double MinutesPerWeek = MinutesPerDay * 7;

    private readonly ArrivalModelConfig _config;

    public ArrivalModel(ArrivalModelConfig config) => _config = config;

    /// <summary>Arrivals per minute at this moment, never zero so an interarrival draw stays finite.</summary>
    public double RatePerMinuteAt(double currentTime)
    {
        var perMinute = _config.BaseRatePerHour / MinutesPerHour;
        var multiplier = 1.0;

        if (_config.HourlyVariation)
        {
            multiplier *= HourOfDayMultiplier(currentTime);
        }

        if (_config.DailyVariation)
        {
            multiplier *= DayOfWeekMultiplier(currentTime);
        }

        if (_config.SeasonalVariation)
        {
            multiplier *= SeasonalMultiplier(currentTime);
        }

        return Math.Max(perMinute * multiplier, 1e-6);
    }

    /// <summary>
    /// Emergency departments are quiet overnight and busiest late morning through early evening.
    /// Modelled as a smooth cycle peaking around 15:00 and troughing around 03:00.
    /// </summary>
    private static double HourOfDayMultiplier(double currentTime)
    {
        var hourOfDay = currentTime % MinutesPerDay / MinutesPerHour;
        var phase = 2.0 * Math.PI * (hourOfDay - 3.0) / 24.0;
        return 1.0 - (0.6 * Math.Cos(phase));
    }

    /// <summary>Mondays and weekends run heavier than midweek in most urgent-care settings.</summary>
    private static double DayOfWeekMultiplier(double currentTime)
    {
        var dayOfWeek = (int)(currentTime % MinutesPerWeek / MinutesPerDay);
        return dayOfWeek switch
        {
            0 => 1.15,  // Monday
            5 => 1.10,  // Saturday
            6 => 1.20,  // Sunday
            _ => 0.95
        };
    }

    /// <summary>A slow annual cycle standing in for winter respiratory demand.</summary>
    private static double SeasonalMultiplier(double currentTime)
    {
        const double MinutesPerYear = MinutesPerDay * 365;
        var phase = 2.0 * Math.PI * (currentTime % MinutesPerYear) / MinutesPerYear;
        return 1.0 + (0.2 * Math.Cos(phase));
    }
}
