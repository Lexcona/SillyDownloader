using System.Net;
using System.Net.Mime;
using System.Text.Json;
using HtmlAgilityPack;
using SillyDownloader.Downloaders;
using SillyDownloader.Helper;
using SillyDownloader.Logging;

namespace SillyDownloader.Extractors;

public class SoundCloud:Extractor
{
    public override string Name => "SoundCloud";
    public override Downloader Downloader => General.Instance;

    private string songRegex = @"^https?://(?:www\.)?soundcloud\.com/([^/?#]+)/([^/?#]+)/?$";
    private string profileRegex = @"^https?://(?:www\.)?soundcloud\.com/([^/?#]+)/?$";
    
    public override string[] URLs => [
        songRegex,
        profileRegex,
    ];

    private ExtractionData GetSongData(string url)
    {
        Response response = Requests.Get(url);
        response.response.EnsureSuccessStatusCode();
        
        string responseText = response.text;
        
        File.WriteAllText("test.html", responseText);
        
        Dictionary<string, JsonElement> hydration = GetHydrationData(responseText);
        
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

        General.Metadata metadata = new General.Metadata()
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
        
        return extractionData;
    }

    public override ExtractionData[] Extract(string url)
    {
        base.Extract(url);

        List<ExtractionData> allData = new();

        ExtractionData songData = GetSongData(url);
        if (songData == null)
        {
            return [];
        }
        allData.Add(songData);
        
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