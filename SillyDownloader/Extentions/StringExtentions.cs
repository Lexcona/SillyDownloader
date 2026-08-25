namespace SillyDownloader.Extentions;

public static class StringExtentions
{
    public static string[] SplitLines(this string? value)
    {
        return value.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
    }
}