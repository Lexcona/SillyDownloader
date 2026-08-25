using System.Net;
using System.Net.Http;
using SillyDownloader.Extractors;
using SillyDownloader.Helper;
using SillyDownloader.Logging;

namespace SillyDownloader.Downloaders;

public class General:Downloader
{
    public override string Name => "General";
    
    public static General Instance { get; } = new();

    public override void Download(string url, string filename, string output="", Dictionary<string, string> headers = null, ExtractionData extractionData=null)
    {
        base.Download(url, filename, output, headers, extractionData);
        if (headers == null)
        {
            headers = new Dictionary<string, string>();
        }

        if (!headers.ContainsKey("User-Agent"))
        {
            headers.Add("User-Agent", Extra.GetUserAgent());
        }
        
        if (!headers.ContainsKey("Host"))
        {
            headers.Add("Host", Extra.GetDomain(url));
        }
        
        if (!headers.ContainsKey("Referer"))
        {
            headers.Add("Referer", url);
        }
        
        if (!headers.ContainsKey("Range"))
        {
            headers.Add("Range", "bytes=0-");
        }

        string fullOutputPath = Path.Join(output, filename);
        
        if (File.Exists(fullOutputPath) && !string.IsNullOrEmpty(fullOutputPath))
        {
            Debug.Warning($"Skipping (Exists): {filename}, {url}");
            return;
        }
        
        Response response = Requests.Get(url, headers:headers);
        response.response.EnsureSuccessStatusCode();
        
        byte[] data = response.content;

        if (!Directory.Exists(output) && !string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
        }
        
        
        File.WriteAllBytes(fullOutputPath, data);
        Debug.Success($"Downloaded: {filename}, {url}");
        
        if (extractionData.data.TryGetValue("metadata", out var value))
        {
            Debug.Success($"Applying Metadata: {filename}, {url}");
            ApplyMetadata(fullOutputPath, (Metadata)value);
            Debug.Success($"Applied Metadata: {filename}, {url}");
        }
    }
    
    private static void ApplyMetadata(string path, Metadata metadata)
    {
        TagLib.File file = TagLib.File.Create(path);

        file.Tag.Title = metadata.title;
        file.Tag.Performers = [metadata.artist];
        file.Tag.Album = metadata.album;
        file.Tag.Track = (uint)metadata.trackNumber;

        if (!string.IsNullOrEmpty(metadata.albumArtUrl))
        {
            byte[] cover = Requests.Get(metadata.albumArtUrl).content;

            TagLib.Picture picture = new TagLib.Picture(new TagLib.ByteVector(cover));

            picture.Type = TagLib.PictureType.FrontCover;
            picture.MimeType = "image/jpeg";

            file.Tag.Pictures = [picture];
        }

        file.Save();
    }
    
    public class Metadata
    {
        public string artist;
        public string album;
        public string title;
        public int trackNumber;
        public string albumArtUrl;
    }
}