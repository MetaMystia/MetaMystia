using System.Globalization;

namespace MetaMystia.Network;

public static class RoomCode
{
    public static string Format(ushort id) => id.ToString("X4", CultureInfo.InvariantCulture);

    public static bool TryParse(string text, out ushort id)
    {
        id = 0;
        return text is { Length: 4 } && ushort.TryParse(text, NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture, out id) && id != 0;
    }
}
