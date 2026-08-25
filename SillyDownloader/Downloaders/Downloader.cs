using SillyDownloader.Extractors;
using SillyDownloader.Logging;

namespace SillyDownloader.Downloaders;

public abstract class Downloader
{
    public abstract string Name { get; }

    public virtual void Download(string url, string filename, string output = "", Dictionary<string, string> headers = null, ExtractionData extractionData=null)
    {
        Debug.Info($"Downloading: {url} using {Name} downloader");
    }
}