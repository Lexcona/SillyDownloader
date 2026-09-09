namespace SillyDownloader.Extentions;

public static class ArrayExtentions
{
    public static string Join<T>(this T[] value, string separator)
    {
        string big = "";
        foreach (T thing in value)
        {
            if (thing != null)
            {
                big += thing.ToString()+separator;
            }
        }

        return big;
    }
}