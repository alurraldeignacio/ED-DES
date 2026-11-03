namespace UrgentCare.Core.Queues;

/// <summary>
/// Resolves the <c>queuePolicy</c> string from scenario configuration to a policy instance.
/// This is the single place a policy name becomes behavior, so switching a department from
/// FIFO to UrgencyFirst is a configuration edit and never a code edit (FR-012).
/// </summary>
public static class QueuePolicyFactory
{
    /// <summary>Policy names this build understands, as they appear in configuration.</summary>
    public static IReadOnlyList<string> SupportedNames { get; } =
        ["FIFO", "UrgencyFirst", "UrgencyPlusWait"];

    /// <summary>
    /// Creates the named policy. An unknown name throws rather than falling back to a default:
    /// silently running a scenario under the wrong rule would invalidate every comparison
    /// drawn from it.
    /// </summary>
    public static IQueuePolicy Create(string name) => name switch
    {
        "FIFO" => new FifoPolicy(),
        "UrgencyFirst" => new UrgencyFirstPolicy(),
        "UrgencyPlusWait" => new UrgencyPlusWaitPolicy(),
        _ => throw new NotSupportedException(
            $"Queue policy '{name}' is not supported. Available policies: "
            + string.Join(", ", SupportedNames) + ".")
    };
}
