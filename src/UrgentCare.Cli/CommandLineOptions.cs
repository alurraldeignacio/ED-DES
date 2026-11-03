namespace UrgentCare.Cli;

/// <summary>Parsed command-line arguments.</summary>
public sealed record CommandLineOptions
{
    /// <summary>Scenario name, resolved to shared/config/&lt;name&gt;-scenario.json.</summary>
    public string Scenario { get; init; } = "default";

    /// <summary>
    /// Skip the interactive view and run straight to the end. Batch runs and CI use this so
    /// nothing waits on a keypress.
    /// </summary>
    public bool Headless { get; init; }

    public bool ShowHelp { get; init; }

    /// <summary>Directory holding scenario files; overridable so tests need not touch the repo layout.</summary>
    public string ConfigDirectory { get; init; } = Path.Combine("shared", "config");

    /// <summary>Baseline scenario for a comparison run, or null when not comparing.</summary>
    public string? CompareBaseline { get; init; }

    /// <summary>Variant scenario for a comparison run, or null when not comparing.</summary>
    public string? CompareVariant { get; init; }

    /// <summary>True when both sides of a comparison were supplied.</summary>
    public bool IsComparison => CompareBaseline is not null && CompareVariant is not null;

    /// <summary>
    /// How many times to run the scenario. One is a single run; more than one aggregates the
    /// batch and reports the spread as well as the mean.
    /// </summary>
    public int Replications { get; init; } = 1;

    /// <summary>
    /// Where to write the machine-readable event stream: a file path, or "-" for standard
    /// output. Null leaves the stream unwritten.
    /// </summary>
    public string? EventStream { get; init; }

    /// <summary>True when the event stream goes to standard output rather than a file.</summary>
    public bool EventStreamToConsole => EventStream == "-";

    public static CommandLineOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = new CommandLineOptions();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--scenario" or "-s":
                    options = options with { Scenario = RequireValue(args, ref i, "--scenario") };
                    break;

                case "--config-dir":
                    options = options with { ConfigDirectory = RequireValue(args, ref i, "--config-dir") };
                    break;

                case "--compare":
                    options = options with
                    {
                        CompareBaseline = RequireValue(args, ref i, "--compare"),
                        CompareVariant = RequireValue(args, ref i, "--compare")
                    };
                    break;

                case "--events":
                    options = options with { EventStream = RequireValue(args, ref i, "--events") };
                    break;

                case "--replications":
                    options = options with
                    {
                        Replications = ParseReplications(RequireValue(args, ref i, "--replications"))
                    };
                    break;

                case "--headless":
                    options = options with { Headless = true };
                    break;

                case "--help" or "-h":
                    options = options with { ShowHelp = true };
                    break;

                default:
                    throw new ArgumentException($"Unknown argument '{args[i]}'. Use --help for usage.");
            }
        }

        Validate(options);

        return options;
    }

    /// <summary>
    /// Rejects combinations that cannot be honored. The event stream is machine-readable
    /// telemetry and must never share the console with the interactive view, and a replication
    /// batch runs many simulations that would each overwrite the same stream.
    /// </summary>
    private static void Validate(CommandLineOptions options)
    {
        if (options.EventStream is null)
        {
            return;
        }

        if (!options.Headless)
        {
            throw new ArgumentException(
                "--events requires --headless: the event stream is machine-readable telemetry "
                + "and must not share the console with the interactive operational view.");
        }

        if (options.Replications > 1)
        {
            throw new ArgumentException(
                "--events cannot be combined with --replications: each replication would "
                + "overwrite the previous run's stream.");
        }
    }

    public static string UsageText =>
        """
        Urgent Care Queue Simulator

        Usage:
          dotnet run --project src/UrgentCare.Cli -- [options]

        Options:
          -s, --scenario <name>   Scenario to run (default: "default").
                                  Loads shared/config/<name>-scenario.json
              --config-dir <dir>  Directory holding scenario files
              --headless          Run to completion without the interactive view
              --compare <a> <b>   Run two scenarios and print a baseline-versus-variant table
              --replications <n>  Run the scenario n times and report the aggregate (implies
                                  headless)
              --events <path>     Write the machine-readable event stream as JSON Lines.
                                  Use "-" for standard output. Requires --headless
          -h, --help              Show this help

        Interactive keys (when not headless):
          space   pause / resume
          r       reset and run again
          + / -   faster / slower
          q       quit
        """;

    private static int ParseReplications(string value)
    {
        if (!int.TryParse(value, out var replications) || replications < 1)
        {
            throw new ArgumentException(
                $"--replications requires a whole number of at least 1, but got '{value}'.");
        }

        return replications;
    }

    private static string RequireValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{flag} requires a value.");
        }

        return args[++index];
    }
}
