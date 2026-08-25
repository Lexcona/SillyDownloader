namespace SillyDownloader.Paths;

public class DATAPATHS
{
    public static string APPDATA = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public static string LOCALAPPDATA = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string HOME = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    
    public static string DATAPATH = Path.Join(LOCALAPPDATA, "SillyDownloader"); 
}