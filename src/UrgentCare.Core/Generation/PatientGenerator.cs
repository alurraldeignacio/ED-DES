using UrgentCare.Core.Configuration;
using UrgentCare.Core.Distributions;
using UrgentCare.Core.Domain;
using UrgentCare.Core.Random;

namespace UrgentCare.Core.Generation;

/// <summary>
/// Samples every patient attribute from the configured parameterized distributions. No
/// distribution parameter is hard-coded here: changing patient behavior is a configuration
/// edit, never a code edit (FR-012).
/// </summary>
public sealed class PatientGenerator : IPatientGenerator
{
    private readonly IDistribution _urgency;
    private readonly IDistribution _age;
    private readonly IDistribution _serviceDuration;
    private readonly IDistribution _abandonmentThreshold;
    private readonly IRandomSource _random;
    private readonly ArrivalModel _arrivalRate;

    public PatientGenerator(
        ScenarioConfig config,
        IDistributionFactory distributionFactory,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(distributionFactory);
        ArgumentNullException.ThrowIfNull(random);

        _random = random;
        _arrivalRate = new ArrivalModel(config.ArrivalModel);

        var patientModel = config.PatientModel;
        _urgency = distributionFactory.Create(patientModel.UrgencyDistribution, random);
        _age = distributionFactory.Create(patientModel.AgeDistribution, random);
        _serviceDuration = distributionFactory.Create(patientModel.ServiceDurationDistribution, random);
        _abandonmentThreshold = distributionFactory.Create(
            patientModel.AbandonmentThresholdDistribution, random);
    }

    /// <summary>
    /// Draws an exponential interarrival time at the rate in force right now, so demand varies
    /// with the time of day rather than staying flat across the run (FR-001).
    /// </summary>
    public double NextInterarrivalTime(double currentTime)
    {
        var ratePerMinute = _arrivalRate.RatePerMinuteAt(currentTime);

        // Guard against log(0) so the draw is always finite.
        var uniform = 1.0 - _random.NextDouble();
        return -Math.Log(uniform) / ratePerMinute;
    }

    public Patient GeneratePatient(string id, double arrivalTime)
    {
        // Urgency comes back as a category value on the ESI scale, 1 (most severe) to 5.
        var urgency = Math.Clamp((int)Math.Round(_urgency.Sample()), 1, 5);
        var age = Math.Max(0, (int)Math.Round(_age.Sample()));

        // A treatment cannot take zero minutes, and a patient cannot tolerate a negative wait.
        var serviceDuration = Math.Max(0.1, _serviceDuration.Sample());
        var abandonmentThreshold = Math.Max(0.0, _abandonmentThreshold.Sample());

        return new Patient(id, urgency, age, arrivalTime, serviceDuration, abandonmentThreshold);
    }
}
