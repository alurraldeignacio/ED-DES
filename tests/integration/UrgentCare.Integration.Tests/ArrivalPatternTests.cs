using UrgentCare.Core.Metrics;

namespace UrgentCare.Integration.Tests;

/// <summary>
/// US2 AS3: changing the arrival pattern changes what the department experiences. Queue length
/// and abandonment are the two measures that respond most directly to demand, so they are what
/// these tests assert on (FR-001, FR-009).
/// </summary>
public class ArrivalPatternTests
{
    private static SimulationMetrics RunWith(double baseRatePerHour, bool hourlyVariation = false)
    {
        var config = ScenarioBuilder.Scenario(
            resourceCount: 2,
            roomCount: 10,
            baseRatePerHour: baseRatePerHour,
            timeHorizon: 4320.0,
            seed: 777,
            hourlyVariation: hourlyVariation);

        var (_, log) = ScenarioBuilder.Run(config);

        return new StatisticsCollector(config.Metrics)
            .Collect(log.Events, config.TimeHorizon, config.Department.ResourceCount);
    }

    [Fact]
    public void AHeavierArrivalRate_LengthensTheQueue()
    {
        var quiet = RunWith(baseRatePerHour: 2.0);
        var busy = RunWith(baseRatePerHour: 8.0);

        Assert.True(
            busy.AverageQueueLength > quiet.AverageQueueLength,
            $"Expected a heavier arrival rate to lengthen the queue, but got "
            + $"{busy.AverageQueueLength:0.00} against {quiet.AverageQueueLength:0.00}.");
    }

    [Fact]
    public void AHeavierArrivalRate_RaisesThePeakQueueLength()
    {
        var quiet = RunWith(baseRatePerHour: 2.0);
        var busy = RunWith(baseRatePerHour: 8.0);

        Assert.True(
            busy.MaxQueueLength > quiet.MaxQueueLength,
            $"Expected a heavier arrival rate to raise the peak, but got "
            + $"{busy.MaxQueueLength} against {quiet.MaxQueueLength}.");
    }

    [Fact]
    public void AHeavierArrivalRate_RaisesAbandonment()
    {
        var quiet = RunWith(baseRatePerHour: 2.0);
        var busy = RunWith(baseRatePerHour: 8.0);

        Assert.True(
            busy.AbandonmentRate > quiet.AbandonmentRate,
            $"Expected a heavier arrival rate to drive more walkouts, but got "
            + $"{busy.AbandonmentRate:P1} against {quiet.AbandonmentRate:P1}.");
    }

    [Fact]
    public void AHeavierArrivalRate_BringsMorePatientsThrough()
    {
        var quiet = RunWith(baseRatePerHour: 2.0);
        var busy = RunWith(baseRatePerHour: 8.0);

        Assert.True(busy.Arrivals > quiet.Arrivals);
        Assert.True(busy.Throughput > quiet.Throughput);
    }

    [Fact]
    public void ANonStationaryProfile_ConcentratesDemandIntoPeaks()
    {
        // Same average demand, but the hourly profile pushes arrivals into the afternoon peak,
        // so the department sees a longer queue at its worst moment (FR-001).
        var flat = RunWith(baseRatePerHour: 5.0, hourlyVariation: false);
        var peaked = RunWith(baseRatePerHour: 5.0, hourlyVariation: true);

        Assert.True(
            peaked.MaxQueueLength > flat.MaxQueueLength,
            $"Expected the peaked profile to produce a worse peak, but got "
            + $"{peaked.MaxQueueLength} against {flat.MaxQueueLength}.");
    }

    [Fact]
    public void TheSameArrivalPatternAndSeed_ReproducesTheSameQueueBehavior()
    {
        var first = RunWith(baseRatePerHour: 5.0);
        var second = RunWith(baseRatePerHour: 5.0);

        Assert.Equal(first.AverageQueueLength, second.AverageQueueLength);
        Assert.Equal(first.MaxQueueLength, second.MaxQueueLength);
        Assert.Equal(first.AbandonmentRate, second.AbandonmentRate);
    }
}
