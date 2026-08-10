namespace GbaEmulator.Core.Video.Sprites;

public readonly struct ScanlineSpriteInfo
{
    public readonly int ScanlineStartMapTileNumber;
    public readonly int PaletteNumber; //attr2
    public readonly int YPixelOffset; //calced
    public readonly int Priority; //attr 2
    public readonly int NumXTiles; // calced
    public readonly int XCoord; //{ get; init; } //attr1
    public readonly int Mode;
    public readonly bool IsSinglePalette;
    public readonly bool HFlip;
    public readonly bool IsRotational;
    public readonly short Pa;
    public readonly short Pb;
    public readonly short Pc;
    public readonly short Pd;
    public readonly int YTiles;

    public ScanlineSpriteInfo(int scanlineStartMapTileNumber, bool isSinglePalette, int paletteNumber, int yPixelOffset,
        int priority, int numXTiles, int xCoord, int mode, bool hFlip, bool isRotational, short pa, short pb, short pc, short pd, int yTiles)
    {
        ScanlineStartMapTileNumber = scanlineStartMapTileNumber;
        IsSinglePalette = isSinglePalette;
        PaletteNumber = paletteNumber;
        YPixelOffset = yPixelOffset;
        Priority = priority;
        NumXTiles = numXTiles;
        XCoord = xCoord;
        Mode = mode;
        HFlip = hFlip;
        IsRotational = isRotational;
        Pa = pa;
        Pb = pb;
        Pc = pc;
        Pd = pd;
        YTiles = yTiles;
    }
}