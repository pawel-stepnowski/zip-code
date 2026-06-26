namespace ZipCode.Cli;

class CliOptionsParser
{
    public static CliOptions Parse(string[] args)
    {
        var command = "pack";
        string? configPath = null;
        var scopes = new List<string>();
        string? outputPath = null;
        var printFiles = false;
        var dryRun = false;
        var showHelp = false;
        var index = 0;

        if (args.Length > 0 && !args[0].StartsWith('-'))
        {
            command = args[0];
            index = 1;
        }

        while (index < args.Length)
        {
            var arg = args[index];
            switch (arg)
            {
                case "-c":
                case "--config":
                    configPath = ReadValue(args, ref index, arg);
                    break;
                case "-s":
                case "--scope":
                    AddScopeValues(scopes, ReadValue(args, ref index, arg));
                    break;
                case "-o":
                case "--output":
                    outputPath = ReadValue(args, ref index, arg);
                    break;
                case "--print-files":
                    printFiles = true;
                    index++;
                    break;
                case "--dry-run":
                    dryRun = true;
                    index++;
                    break;
                case "-h":
                case "--help":
                    showHelp = true;
                    index++;
                    break;
                default:
                    throw new CliUsageException($"Unknown option '{arg}'.");
            }
        }

        return new CliOptions(command, configPath, scopes, outputPath, printFiles, dryRun, showHelp);
    }

    private static void AddScopeValues(List<string> scopes, string value)
    {
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!scopes.Contains(part, StringComparer.OrdinalIgnoreCase))
            {
                scopes.Add(part);
            }
        }
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException($"Option '{optionName}' requires a value.");
        }

        var value = args[index + 1];

        if (value.StartsWith('-'))
        {
            throw new CliUsageException($"Option '{optionName}' requires a value.");
        }

        index += 2;
        return value;
    }
}
