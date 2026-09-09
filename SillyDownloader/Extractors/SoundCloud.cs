using System.Net;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using SillyDownloader.Downloaders;
using SillyDownloader.Helper;
using SillyDownloader.Logging;

namespace SillyDownloader.Extractors;

public class SoundCloud:Extractor
{
    public override string Name => "SoundCloud";
    public override Downloader Downloader => Downloaders.General.Instance;

    private string songRegex = @"^https?://(?:www\.)?soundcloud\.com/([^/?#]+)/([^/?#]+)/?$";
    private string albumRegex = @"^https?://(?:www\.)?soundcloud\.com/([^/?#]+)/sets/([^/?#]+)/?$";
    private string profileRegex = @"^https?://(?:www\.)?soundcloud\.com/([^/?#]+)/?$";
    
    public override string[] URLs => [
        songRegex,
        profileRegex,
        albumRegex
    ];

    private ExtractionData GetSongData(string url, string? album=null)
    {
        Debug.Info($"Getting song data: {url}");
        Response response = Requests.Get(url);
        response.response.EnsureSuccessStatusCode();
        
        string responseText = response.text;
        
        Dictionary<string, JsonElement> hydration = GetHydrationData(responseText);
        
        File.WriteAllText("dump.json", JsonSerializer.Serialize(hydration));
        
        JsonElement soundData = hydration["sound"];
        JsonElement profileData = hydration["user"];
        JsonElement apiClientData = hydration["apiClient"];
        
        JsonElement.ArrayEnumerator mediaList = soundData.GetProperty("media").GetProperty("transcodings").EnumerateArray();
        string baseURL = "";
        string preset = "";
        foreach (JsonElement media in mediaList)
        {
            baseURL = media.GetProperty("url").GetString();
            preset = media.GetProperty("preset").GetString();
            
            if (preset == "mp3_0_1" && baseURL.EndsWith("/progressive"))
            {
                break;
            }
            
            baseURL = "";
            preset = "";
        }

        if (string.IsNullOrEmpty(preset) && string.IsNullOrEmpty(baseURL))
        {
            Debug.Error("Unable to find the base song URL.");
            return null;
        }

        string? trackAuthorization = soundData.GetProperty("track_authorization").GetString();
        string? clientId = apiClientData.GetProperty("id").GetString();

        Response getSoundURLResponse = Requests.Get(baseURL, parameters:new()
        {
            {"client_id", clientId},
            {"track_authorization", trackAuthorization}
        });
        
        getSoundURLResponse.response.EnsureSuccessStatusCode();

        JsonElement getSoundURLResponseJSON = getSoundURLResponse.json();
        
        string? title = soundData.GetProperty("title").GetString();
        string? soundURL = getSoundURLResponseJSON.GetProperty("url").GetString();
        string? albumArtUrl = soundData.GetProperty("artwork_url").GetString() ?? "";
        long soundID = soundData.GetProperty("id").GetInt64();
        
        string authorName = profileData.GetProperty("username").GetString();
        string authorUsername = profileData.GetProperty("permalink").GetString();
        long authorID = profileData.GetProperty("id").GetInt64();

        string filename = $"{authorName} - {title}.mp3";
        
        Debug.Info($"Found: {authorName} - {authorUsername} - {authorID} - {title} - {soundID}");
        Debug.Info($"Audio: {soundURL}", verbose:true);
        
        //Environment.Exit(0);

        Downloaders.General.Metadata metadata = new Downloaders.General.Metadata()
        {
            artist = authorName,
            albumArtUrl = albumArtUrl,
            title = title
        };

        ExtractionData extractionData = new ExtractionData()
        {
            url = soundURL,
            filename = filename,
            data = new Dictionary<string, object>()
            {
                {
                    "metadata",
                    metadata
                }
            }
        };

        if (string.IsNullOrEmpty(album))
        {
            extractionData.defaultOutput = album;
            metadata.album = album;
        }
        
        return extractionData;
    }

    private (string[], string?) GetAlbumData(string url)
    {
        Debug.Info($"Getting album data: {url}");
        Response response = Requests.Get(url);
        response.response.EnsureSuccessStatusCode();
        
        string responseText = response.text;
        
        Dictionary<string, JsonElement> hydration = GetHydrationData(responseText);
        
        File.WriteAllText("dump.json", JsonSerializer.Serialize(hydration, new JsonSerializerOptions { WriteIndented = true }));

        List<string> permURLs = new();
        
        JsonElement playlistData = hydration["playlist"];
        JsonElement tracksData = playlistData.GetProperty("tracks");

        string? playListName = playlistData.GetProperty("title").GetString();
        
        foreach (JsonElement soundData in tracksData.EnumerateArray())
        {
            string? permalink = soundData.GetProperty("permalink_url").GetString();
            if (permalink != null)
            {
                Debug.Info($"Found: {permalink}");
                permURLs.Add(permalink);
            }
        }
        return (permURLs.ToArray(), playListName);
    }

    public Dictionary<string, string[]> GetArtistData(string url)
    {
        List<string> foundAlbums = new();
        List<string> foundTracks = new();
        
        List<string> ignoreTracks = new();
        
        Debug.Info($"Getting artist data: {url}");
        Dictionary<string, string[]> data = new();
        
        Response response = Requests.Get(url);
        response.response.EnsureSuccessStatusCode();
        
        string responseText = response.text;
        
        Dictionary<string, JsonElement> hydration = GetHydrationData(responseText);
        
        File.WriteAllText("dump.json", JsonSerializer.Serialize(hydration, new JsonSerializerOptions { WriteIndented = true }));
        
        JsonElement apiClientData = hydration["apiClient"];
        JsonElement artistData = hydration["user"];
        JsonElement statsigClientInitializeResponseData = hydration["statsigClientInitializeResponse"];
        
        JsonElement userClientData = statsigClientInitializeResponseData.GetProperty("user");
        
        string? clientId = apiClientData.GetProperty("id").GetString();
        string? appVersion = userClientData.GetProperty("appVersion").GetString();
        string? urn = artistData.GetProperty("urn").GetString();
            
        string artistID = urn.Split(":")[3];
        
        Debug.Info($"Grabbing album list...");
            
        Response albumRequest = Requests.Get($"https://api-v2.soundcloud.com/users/{artistID}/albums", parameters: new Dictionary<string, string>()
        {
            {"client_id", clientId},
            {"app_version", appVersion},
            {"limit", "100000"},
            {"offset", "0"},
            {"linked_partitioning", "1"},
            {"app_locale", "en"}
        });

        albumRequest.response.EnsureSuccessStatusCode();
        
        Debug.Success($"Got album list");

        JsonElement albumListData = albumRequest.json();
        JsonElement.ArrayEnumerator albumList = albumListData.GetProperty("collection").EnumerateArray();
        
        foreach (JsonElement albumData in albumList)
        {
            string? permalink = albumData.GetProperty("permalink_url").GetString();
            if (permalink != null)
            {
                Debug.Info($"Found: {permalink}");
                foundAlbums.Add(permalink);
            }
            
            
        }
        
        return data;
    }
    
    public override ExtractionData[] Extract(string url)
    {
        base.Extract(url);

        List<ExtractionData> allData = new();

        if (Extra.RegexCheck(url, songRegex))
        {
            ExtractionData songData = GetSongData(url);
            if (songData == null)
            {
                return [];
            }
            allData.Add(songData);
        }
        else if (Extra.RegexCheck(url, albumRegex))
        {
            (string[], string?) albumData = GetAlbumData(url);
            foreach (string song in albumData.Item1)
            {
                ExtractionData songData = GetSongData(song, albumData.Item2);
                if (songData == null)
                {
                    return [];
                }
                allData.Add(songData);
            }
        }
        else if (Extra.RegexCheck(url, profileRegex))
        {
            GetArtistData(url);
        }
        
        return allData.ToArray();
    }
    
    private JsonDocument ParseSoundCloudHydration(string html)
    {
        const string marker = "window.__sc_hydration";

        int markerIndex = html.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex == -1)
        {
            throw new Exception("Could not find window.__sc_hydration");
        }

        int jsonStart = html.IndexOf('[', markerIndex);
        if (jsonStart == -1)
        {
            throw new Exception("Could not find hydration JSON");
        }

        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = jsonStart; i < html.Length; i++)
        {
            char character = html[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (character == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }

            if (character == '[')
            {
                depth++;
            }
            else if (character == ']')
            {
                depth--;

                if (depth == 0)
                {
                    string json = html[jsonStart..(i + 1)];
                    return JsonDocument.Parse(json);
                }
            }
        }

        throw new Exception("Could not find end of hydration JSON");
    }
    
    private Dictionary<string, JsonElement> GetHydrationData(string html)
    {
        using JsonDocument doc = ParseSoundCloudHydration(html);

        Dictionary<string, JsonElement> result = new();

        foreach (JsonElement item in doc.RootElement.EnumerateArray())
        {
            string? name = item.GetProperty("hydratable").GetString();

            if (name == null)
            {
                continue;
            }

            result[name] = item.GetProperty("data").Clone();
        }

        return result;
    }
}