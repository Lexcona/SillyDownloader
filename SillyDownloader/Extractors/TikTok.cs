using System.Net;
using System.Text.Json;
using HtmlAgilityPack;
using SillyDownloader.Downloaders;
using SillyDownloader.Helper;
using SillyDownloader.Logging;

namespace SillyDownloader.Extractors;

public class TikTok:Extractor
{
    public override string Name => "TikTok";
    public override Downloader Downloader => Downloaders.General.Instance;

    private string videoRegex = @"^https?://(?:www\.)?tiktok\.com/@[^/?#]+/video/\d+/?(?:\?.*)?$";
    private string profileRegex = @"^https?://(?:www\.)?tiktok\.com/@[^/?#]+/?(?:\?.*)?$";
    
    public override string[] URLs => [
        videoRegex,
        //profileRegex,
    ];

    private ExtractionData GetVideoData(string url)
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
        
        //File.WriteAllText("test.html", responseText);
        
        HtmlDocument doc = new HtmlDocument();
        doc.LoadHtml(responseText);
        
        Debug.Info("Looking for __UNIVERSAL_DATA_FOR_REHYDRATION__", verbose:true);
        HtmlNode? dataScript = doc.DocumentNode.SelectSingleNode("//script[@id='__UNIVERSAL_DATA_FOR_REHYDRATION__']");
        string json = dataScript.InnerText;
        json = WebUtility.HtmlDecode(json);
        json = json.Replace("\\u002F", "/");
        
        using JsonDocument jsonDoc = JsonDocument.Parse(json);
        JsonElement root = jsonDoc.RootElement;

        JsonElement scope = root.GetProperty("__DEFAULT_SCOPE__");
        JsonElement context = scope.GetProperty("webapp.app-context");
        JsonElement videoDetails = scope.GetProperty("webapp.video-detail").GetProperty("itemInfo").GetProperty("itemStruct");
        JsonElement videoData = videoDetails.GetProperty("video");
        JsonElement authorData = videoDetails.GetProperty("author");
        
        Debug.Info("Grabbing video data...");
        long videoID = long.Parse(videoDetails.GetProperty("id").GetString());
        string? title = videoDetails.GetProperty("desc").GetString();
        string? videoURL = videoData.GetProperty("playAddr").GetString();
        
        long authorID = long.Parse(authorData.GetProperty("id").GetString());
        string? authorNickName = authorData.GetProperty("nickname").GetString();
        string? authorUsername = authorData.GetProperty("uniqueId").GetString();
        
        ExtractionData data = new ExtractionData();
        data.url = videoURL;
        data.filename = $"{title} ({videoID}).mp4";
        data.defaultOutput = $"@{authorUsername} ({authorID})";
        
        Debug.Info($"Found: {title} - {videoID} - {authorNickName} - {authorUsername} - {authorID}", verbose:true);
        Debug.Info($"Video URL: {data.url}");
        
        return data;
    }

    public override ExtractionData[] Extract(string url)
    {
        base.Extract(url);

        List<ExtractionData> allData = new();
        ExtractionData songData = GetVideoData(url);
        if (songData == null)
        {
            return [];
        }
        allData.Add(songData);
        
        return allData.ToArray();
    }
}