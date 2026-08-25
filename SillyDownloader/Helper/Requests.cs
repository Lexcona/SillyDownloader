using System.Net;
using System.Text;
using System.Text.Json;
using SillyDownloader.Logging;

namespace SillyDownloader.Helper;

public class Response
{
    public int statusCode;
    public byte[] content;
    public string text;
    public HttpResponseMessage response;

    public JsonElement json()
    {
        return JsonDocument.Parse(text).RootElement;
    }
}

public class Requests
{
    public static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    });

    public static Response Request(HttpMethod method, string url, Dictionary<string, string> headers = null, Dictionary<string, string> parameters = null)
    {
        if (parameters != null)
        {
            url = $"{url}?";
            foreach (var param in parameters)
            {
                Debug.Info($"[REQUESTS] Adding parameter: {param.Key}:{param.Value}", verbose:true);
                url +=  $"{param.Key}={param.Value}&";
            }
        }
        
        using HttpRequestMessage request = new(method, url);

        if (headers != null)
        {
            foreach (var header in headers)
            {
                Debug.Info($"[REQUESTS] Adding header: {header.Key}:{header.Value}", verbose:true);
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        Debug.Info($"[REQUESTS] Send request with method {method.Method} to {url}", verbose:true);
        
        HttpResponseMessage rawResponse = Client.SendAsync(request).Result;

        byte[] content = rawResponse.Content.ReadAsByteArrayAsync().Result;

        string text = Encoding.UTF8.GetString(content);
        
        Response response = new Response
        {
            response = rawResponse,
            content = content,
            text = text,
            statusCode = (int)rawResponse.StatusCode
        };
        
        Debug.Info($"[REQUESTS] Message {response.statusCode} response from {url}", verbose:true);

        return response;
    }

    public static Response Get(string url, Dictionary<string, string> headers = null, Dictionary<string, string> parameters = null)
    {
        return Request(HttpMethod.Get, url, headers, parameters);
    }
}