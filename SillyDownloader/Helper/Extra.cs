using System.Text.RegularExpressions;
using SillyDownloader.Extentions;
using SillyDownloader.Logging;

namespace SillyDownloader.Helper;

public class Extra
{
    static string[] userAgents;
    
    public static bool RegexCheck(string data, string regex)
    {
        return Regex.Match(data, regex).Success;
    }

    public static string GetDomain(string url)
    {
        string coolURL = url;
        coolURL = coolURL.Split("://")[1];
        coolURL = coolURL.Split("/")[0];
        return coolURL;
    }

    public static string GetUserAgent()
    {
        if (userAgents == null)
        {
            Debug.Info("[GetUserAgent] Grabbing user agent list.", verbose:true);
            Response response = Requests.Get("https://raw.githubusercontent.com/MichaelJorky/top-user-agents-latest/refs/heads/main/combined/top-100.txt");
            response.response.EnsureSuccessStatusCode();
            userAgents = response.text.SplitLines();
        }
        string userAgent = Helper.Random.RandomChoice(userAgents);

        Debug.Info($"[GetUserAgent] Got user agent: {userAgent}", verbose:true);
        
        return userAgent;
    }

    public static string FormatJson(string data, int indent=4)
    {
        int currentIndent = 0;
        string text = data;
        
        text = text.Split(",").Join(",\n");
        text = text.Split("\":").Join("\": ");
        
        text = text.Split('{').Join("{\n");
        text = text.Split('}').Join("\n}");
        
        text = text.Split('[').Join("[\n");
        text = text.Split(']').Join("\n]");
        
        return text;
    }
}