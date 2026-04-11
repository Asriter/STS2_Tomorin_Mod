namespace STS2_Tomorin_Mod.Extensions;

public static class StringExtensions
{
    public static string CardImagePath(this string path) =>
        Path.Join(MainFile.ModId, "images", "card_portraits", path);

    public static string BigCardImagePath(this string path) =>
        Path.Join(MainFile.ModId, "images", "card_portraits", "big", path);
}
