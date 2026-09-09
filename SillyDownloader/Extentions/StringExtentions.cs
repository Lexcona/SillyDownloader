namespace SillyDownloader.Extentions;

public static class StringExtentions
{
    public static string[] SplitLines(this string? value)
    {
        return value.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
    }

    public static bool Contains(this string? value, string[] strings)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        foreach (string word in strings)
        {
            if (value.Contains(word))
            {
                return true;
            }
        }

        return false;
    }
    
    public static string[] ContainsWhat(this string? value, string[] strings)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        List<string> cool = new();
        foreach (string word in strings)
        {
            if (value.Contains(word))
            {
                if (!cool.Contains(word))
                {
                    cool.Add(word);
                }
            }
        }

        return cool.ToArray();
    }
    
    public static Dictionary<string, int>? Repeats(this string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        
        Dictionary<string, int> result = new Dictionary<string, int>();
        foreach (char c in value)
        {
            if (!result.ContainsKey(c.ToString()))
            {
                result.Add(c.ToString(), 0);
            }
            result[c.ToString()]++;
        }

        return result;
    }
    
    public static bool DoesRepeat(this string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        
        Dictionary<string, int> result = new Dictionary<string, int>();
        foreach (char c in value)
        {
            if (!result.ContainsKey(c.ToString()))
            {
                result.Add(c.ToString(), 0);
            }
            result[c.ToString()]++;
            if (result[c.ToString()] > 1)
            {
                return true;
            }
        }

        return false;
    }

    public static int Find(this string? value, string part, int start = 0, int end = int.MaxValue)
    {
        
    }
    
    public static int FindAll(this string? value, string part)
    {
        List<int> found = new List<int>();
        
        int i = 0;
        int j = 0;
        
    }
}