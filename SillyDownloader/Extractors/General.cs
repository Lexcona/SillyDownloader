using HtmlAgilityPack;
using SillyDownloader.Downloaders;
using SillyDownloader.Helper;
using SillyDownloader.Logging;

namespace SillyDownloader.Extractors;

public class General:Extractor
{
    public override string Name => "General";
    public override Downloader Downloader => Downloaders.General.Instance;
    public override string[] URLs { get; }

    public override ExtractionData[] Extract(string url)
    {
        Dictionary<string, string> headers = new Dictionary<string, string>();
        headers.Add("User-Agent", Extra.GetUserAgent());
        headers.Add("Host", "www.tiktok.com");
        headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,* /*;q=0.8");
        headers.Add("Accept-Language", "en-US,en;q=0.9");
        headers.Add("Accept-Encoding", "gzip, deflate, br, zstd");
        
        Response response = Requests.Get(url, headers:headers);
        response.response.EnsureSuccessStatusCode();

        string responseText = response.text;
        
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(responseText);

        Debug.Info("Finding video", verbose: true);
        HtmlNodeCollection? videos = doc.DocumentNode.SelectNodes("//video");

        List<ExtractionData> data = new();
        foreach (HtmlNode node in videos)
        {
            ExtractionData extractionData = new ExtractionData()
            {
                url = node.Attributes["src"].Value,
                filename = url.Split("/").Last()
            };
            
            data.Add(extractionData);
        }

        return data.ToArray();
    }
}