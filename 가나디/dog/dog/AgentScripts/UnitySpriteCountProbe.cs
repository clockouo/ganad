public static class UnitySpriteCountProbe
{
    public static int Run() { return UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Sprite>().Length; }
}
