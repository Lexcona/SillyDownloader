using System.Reflection;
using System.Text.RegularExpressions;
using SillyDownloader.Downloaders;
using SillyDownloader.Logging;

namespace SillyDownloader.Extractors;

public class ExtractionData
{
    public string url;
    public string filename;
    public string defaultOutput;
    public Dictionary<string, object> data = new Dictionary<string, object>();
}

public abstract class Extractor
{
    public static List<Extractor> Extractors = new();
    public abstract string Name { get; }
    public abstract string[] URLs { get; }
    public virtual Downloader Downloader { get; } = Downloaders.General.Instance;
    public static Extractor General;

    protected Extractor()
    {
        if (Name == "General")
        {
            General = this;
        }
        Extractors.Add(this);
    }
    
    public bool Detect(string url)
    {
        foreach (string urlRegex in URLs)
        {
            Debug.Info($"Detecting if {url} follows {urlRegex}", verbose:true);
            Match match = Regex.Match(url, urlRegex);
            if (match.Success)
            {
                return true;
            }
        }
        return false;
    }

    public static void Load()
    {
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsAbstract)
            {
                continue;
            }
            
            if (typeof(Extractor).IsAssignableFrom(type))
            {
                Activator.CreateInstance(type);
            }
        }
    }

    public virtual ExtractionData[] Extract(string url)
    {
        Debug.Info($"Extracting based on {Name}");
        return [];
    }

    public virtual ExtractionData[] ExtractAndDownload(string url, string? outputPath=null)
    {
        if (outputPath == null)
        {
            outputPath = "";
        }
        ExtractionData[] extractedDataList = Extract(url);
        int extractedDataListLength = extractedDataList.Length;
        string newOutputPath = "";
        foreach (ExtractionData extractedData in extractedDataList)
        {
            if (extractedDataListLength > 1)
            {
                newOutputPath = Path.Join(outputPath, extractedData.defaultOutput);
            }
            Downloader.Download(extractedData.url, extractedData.filename, newOutputPath, extractionData:extractedData);
        }
        return extractedDataList;
    }
}