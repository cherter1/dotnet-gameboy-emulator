namespace GbaEmulator.Core.Video.Sprites;

public readonly struct ScanlineSpriteInfo
{
    public readonly int ScanlineStartMapTileNumber;
    public readonly int PaletteNumber; //attr2
    public readonly int YPixelOffset; //calced
    public readonly int Priority; //attr 2
    public readonly int NumXTiles; // calced
    public readonly int XCoord; //{ get; init; } //attr1
    public readonly bool IsSinglePalette;

    public ScanlineSpriteInfo(int scanlineStartMapTileNumber, bool isSinglePalette, int paletteNumber, int yPixelOffset,
        int priority, int numXTiles, int xCoord)
    {
        ScanlineStartMapTileNumber = scanlineStartMapTileNumber;
        IsSinglePalette = isSinglePalette;
        PaletteNumber = paletteNumber;
        YPixelOffset = yPixelOffset;
        Priority = priority;
        NumXTiles = numXTiles;
        XCoord = xCoord;
    }
}