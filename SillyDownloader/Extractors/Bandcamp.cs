using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using HtmlAgilityPack;
using SillyDownloader.Downloaders;
using SillyDownloader.Logging;

namespace SillyDownloader.Extractors;

public class Bandcamp:Extractor
{
    public override string Name => "Bandcamp";
    public override Downloader Downloader => General.Instance;

    private string songRegex = @"^https?://[^.]+\.bandcamp\.com/track/[^/?#]+/?$";
    private string profileRegex = @"^https?://(?:www\.)?([a-zA-Z0-9-]+)\.bandcamp\.com/?$";
    private string albumRegex = @"^https?://[^.]+\.bandcamp\.com/album/[^/?#]+/?$";
    
    public override string[] URLs => [
        songRegex,
        albumRegex,
        profileRegex
    ];

    public override ExtractionData[] ExtractAndDownload(string url, string? outputPath=null)
    {
        return base.ExtractAndDownload(url, outputPath);
    }

    private ExtractionData GetSongData(string url)
    {
        Debug.Info($"Extracting song: {url}");

        HtmlWeb web = new HtmlWeb();
        HtmlDocument doc = web.Load(url);

        HtmlNode? dataScript = doc.DocumentNode.SelectSingleNode("//script[@data-tralbum]");

        if (dataScript == null)
        {
            Debug.Error($"Could not find Bandcamp data script: {url}");
            return null;
        }

        string tralbumJson = dataScript.GetAttributeValue("data-tralbum", "");

        if (string.IsNullOrWhiteSpace(tralbumJson))
        {
            Debug.Error($"data-tralbum is empty: {url}");
            return null;
        }

        tralbumJson = WebUtility.HtmlDecode(tralbumJson);

        string embedJson = dataScript.GetAttributeValue("data-embed", "");

        if (string.IsNullOrWhiteSpace(embedJson))
        {
            Debug.Error($"data-embed is empty: {url}");
            return null;
        }

        embedJson = WebUtility.HtmlDecode(embedJson);

        using JsonDocument tralbumDoc = JsonDocument.Parse(tralbumJson);
        using JsonDocument embedDoc = JsonDocument.Parse(embedJson);

        JsonElement root = tralbumDoc.RootElement;
        JsonElement embedRoot = embedDoc.RootElement;

        if (!root.TryGetProperty("trackinfo", out JsonElement trackInfo) || trackInfo.ValueKind != JsonValueKind.Array || trackInfo.GetArrayLength() == 0)
        {
            Debug.Error($"No track information found: {url}");
            return null;
        }

        JsonElement track = trackInfo[0];

        string title = "Unknown Title";

        if (track.TryGetProperty("title", out JsonElement titleElement) && titleElement.ValueKind == JsonValueKind.String)
        {
            title = titleElement.GetString() ?? "Unknown Title";
        }

        int trackNumber = 0;
        if (track.TryGetProperty("track_num", out JsonElement trackNumElement) &&
            trackNumElement.ValueKind == JsonValueKind.Number)
        {
            trackNumber = trackNumElement.GetInt32();
        }
        
        string mp3Url = "";

        if (track.TryGetProperty("file", out JsonElement fileElement) && fileElement.ValueKind == JsonValueKind.Object && fileElement.TryGetProperty("mp3-128", out JsonElement mp3Element) && mp3Element.ValueKind == JsonValueKind.String)
        {
            mp3Url = mp3Element.GetString() ?? "";
        }

        if (string.IsNullOrWhiteSpace(mp3Url))
        {
            Debug.Error($"No MP3 found: {url}");
            return null;
        }

        string artist = "Unknown Artist";

        if (root.TryGetProperty("artist", out JsonElement artistElement) && artistElement.ValueKind == JsonValueKind.String)
        {
            artist = artistElement.GetString() ?? "Unknown Artist";
        }
        
        if (artist == "Unknown Artist" && embedRoot.TryGetProperty("artist", out JsonElement embedArtist) && embedArtist.ValueKind == JsonValueKind.String)
        {
            artist = embedArtist.GetString() ?? "Unknown Artist";
        }
        
        string album = "Unknown Album";

        if (embedRoot.TryGetProperty("album_embed_data", out JsonElement albumData) && albumData.ValueKind == JsonValueKind.Object && albumData.TryGetProperty("album_title", out JsonElement albumTitleElement) && albumTitleElement.ValueKind == JsonValueKind.String)
        {
            album = albumTitleElement.GetString() ?? "Unknown Album";
        }

        if (album == "Unknown Album" && root.TryGetProperty("album_title", out JsonElement rootAlbumElement) && rootAlbumElement.ValueKind == JsonValueKind.String)
        {
            album = rootAlbumElement.GetString() ?? "Unknown Album";
        }

        long artId = 0;

        if (embedRoot.TryGetProperty("art_id", out JsonElement embedArtId) && embedArtId.ValueKind == JsonValueKind.Number)
        {
            artId = embedArtId.GetInt64();
        }

        string albumArtUrl = "";

        if (artId > 0)
        {
            albumArtUrl = $"https://f4.bcbits.com/img/a{artId}_10.jpg";
        }

        General.Metadata metadata = new General.Metadata
        {
            artist = artist,
            album = album,
            title = title,
            trackNumber = trackNumber,
            albumArtUrl = albumArtUrl
        };

        ExtractionData data = new ExtractionData
        {
            url = mp3Url,
            filename = trackNumber > 0
                ? $"{trackNumber}. {artist} - {title}.mp3"
                : $"{artist} - {title}.mp3",
            defaultOutput = album
        };

        data.data.Add("metadata", metadata);

        Debug.Info($"Found: {artist} - {album} - {trackNumber}. {title}", verbose:true);
        Debug.Info($"Audio: {mp3Url}");

        if (!string.IsNullOrEmpty(albumArtUrl))
        {
            Debug.Info($"Artwork: {albumArtUrl}");
        }
        else
        {
            Debug.Info("Artwork: none");
        }

        return data;
    }

    public string[] GetAlbumData(string url)
    {
        List<string> trackList = new List<string>();
        HtmlWeb web = new HtmlWeb();
        HtmlDocument doc = web.Load(url);
            
        HtmlNodeCollection tracks = doc.DocumentNode.SelectNodes("//table[@id='track_table']//td[contains(@class, 'title-col')]//a");
            
        foreach (HtmlNode track in tracks)
        {
            string name = track.InnerText.Trim();
            string href = track.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            href = new Uri(new Uri(url), href).ToString();

            Debug.Info($"Found track: {name} -> {href}");
            trackList.Add(href);
        }
        return trackList.ToArray();
    }
    
    public override ExtractionData[] Extract(string url)
    {
        base.Extract(url);

        List<string> trackList = new List<string>();
        List<ExtractionData> finalData = new List<ExtractionData>();
        if (Helper.Extra.RegexCheck(url, profileRegex))
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(url);
            HtmlNodeCollection albums = doc.DocumentNode.SelectNodes("//ol[@id='music-grid']//li[contains(@class, 'music-grid-item')]//a");
            foreach (HtmlNode album in albums)
            {
                string href = album.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                href = new Uri(new Uri(url), href).ToString();

                if (Helper.Extra.RegexCheck(href, songRegex))
                {
                    Debug.Info($"Found Track: {href}");
                    trackList.Add(href);
                }
                else if (Helper.Extra.RegexCheck(href, albumRegex))
                {
                    Debug.Info($"Found Album: {href}");
                    foreach (string track in GetAlbumData(href))
                    {
                        trackList.Add(track);
                    }
                }
            }
        }
        else if (Helper.Extra.RegexCheck(url, albumRegex))
        {
            foreach (string track in GetAlbumData(url))
            {
                trackList.Add(track);
            }
        }
        else if (Helper.Extra.RegexCheck(url, songRegex))
        {
            trackList.Add(url);
        }
        else
        {
            return [];
        }

        foreach (string track in trackList)
        {
            ExtractionData? songData = GetSongData(track);
            if (songData == null)
            {
                continue;
            }
            
            finalData.Add(songData);
        }
        return finalData.ToArray();
    }
}