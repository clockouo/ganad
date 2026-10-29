/// <summary>Shared illustration scale and world positions for the house-to-park slice.</summary>
public static class AfterTheGlowWorldLayout
{
    public const float ExteriorLeftX = 6.6f;
    public const float IllustrationWidth = 18f;
    public const float ExteriorScale = IllustrationWidth / 40f;
    public const float UnitsPerPixel = IllustrationWidth / 2172f;
    public const float StreetEndX = ExteriorLeftX + IllustrationWidth;
    public const float ParkEndX = StreetEndX + IllustrationWidth;
    public const float StreetEntryX = ExteriorLeftX + 225f * UnitsPerPixel;
    // Center of the driveway opening between the two short street wall posts.
    public const float CarX = ExteriorLeftX + 932f * UnitsPerPixel;
    public const float ParkEntryX = StreetEndX + 1.2f * ExteriorScale;
    public const float FountainX = StreetEndX + IllustrationWidth * .5f;
    // Approach from the path just outside the fountain's left rim in 03_ParkSide.
    public const float FountainEntryX = StreetEndX + IllustrationWidth * .29f;
    // Return between the fountain's right rim and the nearby bench.
    public const float FountainExitX = StreetEndX + IllustrationWidth * .715f;
}
