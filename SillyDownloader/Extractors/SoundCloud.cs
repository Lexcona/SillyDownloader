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

    private ExtractionData GetSongData(string url, string? album = null)
    {
        Debug.Info($"Getting song data: {url}");

        Debug.Info("Requesting page...", verbose: true);
        Response response = Requests.Get(url);

        Debug.Info($"Page response: HTTP {response.statusCode}", verbose: true);
        response.response.EnsureSuccessStatusCode();

        string responseText = response.text;

        Debug.Info($"Received {responseText.Length:N0} bytes of page data.", verbose: true);
        Debug.Info("Extracting hydration data...", verbose: true);

        Dictionary<string, JsonElement> hydration = GetHydrationData(responseText);

        Debug.Info($"Hydration data extracted. Keys: {string.Join(", ", hydration.Keys)}", verbose: true );

        File.WriteAllText("dump.json", JsonSerializer.Serialize(hydration, new JsonSerializerOptions { WriteIndented = true }));

        Debug.Info("Hydration data dumped to dump.json.", verbose: true);

        if (!hydration.TryGetValue("sound", out JsonElement soundData))
        {
            Debug.Error("Hydration data does not contain 'sound'.");
            return null;
        }

        if (!hydration.TryGetValue("user", out JsonElement profileData))
        {
            Debug.Error("Hydration data does not contain 'user'.");
            return null;
        }

        if (!hydration.TryGetValue("apiClient", out JsonElement apiClientData))
        {
            Debug.Error("Hydration data does not contain 'apiClient'.");
            return null;
        }

        Debug.Info("Reading sound metadata...", verbose: true);

        JsonElement.ArrayEnumerator mediaList = soundData.GetProperty("media").GetProperty("transcodings").EnumerateArray();

        string baseURL = "";
        string preset = "";

        string? trackAuthorization = soundData.GetProperty("track_authorization").GetString();

        string? clientId = apiClientData.GetProperty("id").GetString();

        Debug.Info($"Client ID present: {!string.IsNullOrEmpty(clientId)}", verbose: true);

        Debug.Info($"Track authorization present: {!string.IsNullOrEmpty(trackAuthorization)}", verbose: true);

        Response getSoundURLResponse = new Response();

        for (int i = 0; i < 2; i++)
        {
            string requestedType = i == 0 ? "progressive" : "hls";

            Debug.Info(
                $"Searching for MP3 {requestedType} transcoding...",
                verbose: true
            );

            mediaList = soundData.GetProperty("media").GetProperty("transcodings").EnumerateArray();

            int mediaIndex = 0;
            bool found = false;

            foreach (JsonElement media in mediaList)
            {
                string? mediaURL = media.GetProperty("url").GetString();
                string? mediaPreset = media.GetProperty("preset").GetString();

                Debug.Info($"Transcoding [{mediaIndex}]: preset={mediaPreset}, url={mediaURL}", verbose: true );

                mediaIndex++;

                if (!string.IsNullOrEmpty(mediaPreset) && !string.IsNullOrEmpty(mediaURL) && mediaPreset.StartsWith("mp3_") && mediaURL.EndsWith($"/{requestedType}"))
                {
                    baseURL = mediaURL;
                    preset = mediaPreset;
                    found = true;

                    Debug.Info($"Selected transcoding: {preset} ({requestedType})", verbose: true);

                    break;
                }
            }

            if (!found)
            {
                Debug.Info($"No MP3 {requestedType} transcoding found.", verbose: true);

                continue;
            }

            Debug.Info($"Requesting audio URL from: {baseURL}", verbose: true);

            getSoundURLResponse = Requests.Get(baseURL, parameters: new()
                {
                    { "client_id", clientId },
                    { "track_authorization", trackAuthorization }
                }
            );

            Debug.Info($"Audio URL response: HTTP {getSoundURLResponse.statusCode}", verbose: true);

            if (getSoundURLResponse.statusCode != 404)
            {
                Debug.Info($"Successfully obtained audio URL using {requestedType}.", verbose: true);

                break;
            }

            Debug.Info($"Audio URL endpoint returned 404 for {requestedType}, trying fallback.", verbose: true);
        }

        if (string.IsNullOrEmpty(getSoundURLResponse.text))
        {
            Debug.Error("Audio URL response was empty.");
            return null;
        }

        getSoundURLResponse.response.EnsureSuccessStatusCode();

        Debug.Info("Parsing audio URL response...", verbose: true);

        JsonElement getSoundURLResponseJSON = getSoundURLResponse.json();

        string? title = soundData.GetProperty("title").GetString();
        string? soundURL = getSoundURLResponseJSON.GetProperty("url").GetString();
        string? albumArtUrl = soundData.GetProperty("artwork_url").GetString() ?? "";
        long soundID = soundData.GetProperty("id").GetInt64();
        string authorName = profileData.GetProperty("username").GetString();
        string authorUsername = profileData.GetProperty("permalink").GetString();
        long authorID = profileData.GetProperty("id").GetInt64();

        Debug.Info($"Song metadata: title={title}, artist={authorName}, id={soundID}", verbose: true);

        Debug.Info($"Artist: {authorName} (@{authorUsername}, ID {authorID})", verbose: true);

        Debug.Info($"Album art present: {!string.IsNullOrEmpty(albumArtUrl)}", verbose: true);

        Debug.Info($"Audio URL: {soundURL}", verbose: true);

        string filename = $"{authorName} - {title}.mp3";

        Debug.Info($"Output filename: {filename}", verbose: true);

        Downloaders.General.Metadata metadata = new Downloaders.General.Metadata()
            {
                artist = authorName,
                albumArtUrl = albumArtUrl,
                title = title
            };

        if (!string.IsNullOrEmpty(album))
        {
            metadata.album = album;

            Debug.Info($"Using album metadata: {album}", verbose: true);
        }
        else
        {
            Debug.Info("No album specified.", verbose: true);
        }

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

        Debug.Info($"Extraction complete: {filename}", verbose: true);

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
        int limit = 250;
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
            
        string artistID = urn.Split(":")[2];

        string offset = "0";

        Response albumRequest;
        Response trackRequest;
        while (true)
        {
            Debug.Info($"Grabbing album list with offset {offset}");
        
            albumRequest = Requests.Get($"https://api-v2.soundcloud.com/users/{artistID}/albums", parameters: new Dictionary<string, string>()
            {
                {"client_id", clientId},
                {"app_version", appVersion},
                {"limit", limit.ToString()},
                {"offset", offset},
                {"linked_partitioning", "1"},
                {"app_locale", "en"}
            });

            albumRequest.response.EnsureSuccessStatusCode();
        
            Debug.Success($"Got album list");

            JsonElement albumListData = albumRequest.json();
        
            File.WriteAllText("albums.json", JsonSerializer.Serialize(albumListData, new JsonSerializerOptions { WriteIndented = true }));
        
            JsonElement.ArrayEnumerator albumList = albumListData.GetProperty("collection").EnumerateArray();
        
            foreach (JsonElement albumData in albumList)
            {
                string? permalink = albumData.GetProperty("permalink_url").GetString();
                if (permalink != null && !foundAlbums.Contains(permalink))
                {
                    Debug.Info($"Found: {permalink}");
                    foundAlbums.Add(permalink);
                }
            
                JsonElement.ArrayEnumerator trackDataList = albumData.GetProperty("tracks").EnumerateArray();

                foreach (JsonElement trackData in trackDataList)
                {
                    //Debug.Info(trackData.ToString());
                    JsonElement permalinkTrackElement;
                    if (trackData.TryGetProperty("permalink_url", out permalinkTrackElement))
                    {
                        string? permalinkTrack = permalinkTrackElement.GetString();
                        if (permalinkTrack != null && !ignoreTracks.Contains(permalinkTrack))
                        {
                            ignoreTracks.Add(permalinkTrack);
                        }
                    }
                }
            }

            if (albumList.ToArray().Length < limit)
            {
                break;
            }
            
            if (Requests.GetParams(albumListData.GetProperty("next_href").GetString()).TryGetValue("offset", out offset))
            {
                break;
            }
        }

        offset = "0";
        
        while (true)
        {
            Debug.Info($"Grabbing track list with offset {offset}");
        
            trackRequest = Requests.Get($"https://api-v2.soundcloud.com/users/{artistID}/tracks", parameters: new Dictionary<string, string>()
            {
                {"client_id", clientId},
                {"app_version", appVersion},
                {"limit", limit.ToString()},
                {"offset", offset},
                {"linked_partitioning", "1"},
                {"app_locale", "en"}
            });

            trackRequest.response.EnsureSuccessStatusCode();
        
            Debug.Success($"Got track list");

            JsonElement trackListData = trackRequest.json();
        
            File.WriteAllText("tracks.json", JsonSerializer.Serialize(trackListData, new JsonSerializerOptions { WriteIndented = true }));
        
            JsonElement.ArrayEnumerator trackList = trackListData.GetProperty("collection").EnumerateArray();
        
            foreach (JsonElement trackData in trackList)
            {
                string? permalink = trackData.GetProperty("permalink_url").GetString();
                if (permalink != null && !foundTracks.Contains(permalink) && !ignoreTracks.Contains(permalink))
                {
                    Debug.Info($"Found: {permalink}");
                    foundTracks.Add(permalink);
                }
            }

            if (trackList.ToArray().Length < limit)
            {
                break;
            }
            
            if (Requests.GetParams(trackListData.GetProperty("next_href").GetString()).TryGetValue("offset", out offset))
            {
                break;
            }
        }

        data["tracks"] = foundTracks.ToArray();
        data["albums"] = foundAlbums.ToArray();
        
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
                    continue;
                }
                allData.Add(songData);
            }
        }
        else if (Extra.RegexCheck(url, profileRegex))
        {
            Dictionary<string, string[]> artistData = GetArtistData(url);
            foreach (string track in artistData["tracks"])
            {
                ExtractionData songData = GetSongData(track);
                if (songData == null)
                {
                    continue;
                }
                allData.Add(songData);
            }
            
            foreach (string track in artistData["albums"])
            {
                (string[], string?) albumData = GetAlbumData(url);
                foreach (string song in albumData.Item1)
                {
                    ExtractionData songData = GetSongData(song, albumData.Item2);
                    if (songData == null)
                    {
                        continue;
                    }
                    allData.Add(songData);
                }
            }
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