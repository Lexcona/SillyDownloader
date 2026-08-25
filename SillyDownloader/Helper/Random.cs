namespace SillyDownloader.Helper;

public class Random
{
    static System.Random random = new();
        
    public static string RandomString(int length)
    {
        string comStr = string.Empty;
            
        for (int i = 0; i < length; i++)
        {
            comStr += Convert.ToChar(random.Next(0, 26) + 65);
        }
        return comStr;
    }

    public static int RandomInt(int min, int max)
    {
        return random.Next(min, max);
    }

    public static float RandomFloat(float min, float max)
    {
        return (float)random.NextDouble() * (max - min) + min;
    }

    public static T RandomChoice<T>(T[] choices, int amount=1)
    {
        return choices[RandomInt(0, choices.Length)];
    }
    
    public static T RandomChoice<T>(List<T> choices)
    {
        return RandomChoice(choices.ToArray());
    }
    
    public static T[] RandomChoices<T>(T[] choices, int amount)
    {
        List<T> all = new List<T>();
        for (int i = 0; i < amount; i++)
        {
            all.Add(choices[RandomInt(0, choices.Length)]);
        }
        return all.ToArray();
    }
    
    public static T[] RandomChoices<T>(List<T> choices, int amount)
    {
        return RandomChoices(choices.ToArray(), amount);
    }
}