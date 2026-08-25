using System.CommandLine;
using SillyDownloader;
using SillyDownloader.Extractors;
using SillyDownloader.Logging;
using SillyDownloader.Paths;

try
{
    Extractor.Load();

    RawArgs rawArgs = new RawArgs();

    rawArgs.url = new("--url", "-u");
    rawArgs.output = new("--output", "-o");
    rawArgs.verbose = new("--verbose", "-v");

    rawArgs.url.Required = true;

    RootCommand rootCommand = new RootCommand("SillyDownloader");

    rootCommand.Options.Add(rawArgs.url);
    rootCommand.Options.Add(rawArgs.output);
    rootCommand.Options.Add(rawArgs.verbose);

    bool foundExtractor = false;

    Directory.CreateDirectory(DATAPATHS.DATAPATH);

    rootCommand.SetAction(result =>
        {
            GLOBAL.argsThing.url = result.GetValue(rawArgs.url);
            GLOBAL.argsThing.output = result.GetValue(rawArgs.output);
            GLOBAL.argsThing.verbose = result.GetValue(rawArgs.verbose);

            foreach (Extractor extractor in Extractor.Extractors)
            {
                if (GLOBAL.argsThing.url != null && extractor.Detect(GLOBAL.argsThing.url))
                {
                    foundExtractor = true;
                    extractor.ExtractAndDownload(GLOBAL.argsThing.url, GLOBAL.argsThing.output);
                    break;
                }
            }

            if (!foundExtractor)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                string outText = "Currently that website is unsupported what we support is:\n";
                foreach (Extractor extractor in Extractor.Extractors)
                {
                    outText += $"{extractor.Name},";
                }

                Console.WriteLine(outText);
                Console.ResetColor();
            }
        }
    );

    return await rootCommand.Parse(args).InvokeAsync();
}
catch (Exception e)
{
    Debug.Error($"Unexpected error occured in the program:\n{e}");
}

return 23475;

class RawArgs
{
    public Option<string> url;
    public Option<string?> output;
    public Option<bool> verbose;
}

public class Args
{
    public string? url;
    public string? output;
    public bool verbose;
}

