public static class SystemTypeProbe
{
    public static string Run() { return System.AppDomain.CurrentDomain.FriendlyName; }
}
