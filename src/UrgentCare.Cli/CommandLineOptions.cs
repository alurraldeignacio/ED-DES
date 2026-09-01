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

        return options;
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
          -h, --help              Show this help

        Interactive keys (when not headless):
          space   pause / resume
          r       reset and run again
          + / -   faster / slower
          q       quit
        """;

    private static string RequireValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{flag} requires a value.");
        }

        return args[++index];
    }
}
