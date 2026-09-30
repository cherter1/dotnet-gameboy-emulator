using System.Runtime.InteropServices;

namespace GbaEmulator.Core.Video.Sprites;

[StructLayout(LayoutKind.Explicit)]
public struct TempNewSpriteInfo
{
    [FieldOffset(0)]
    public bool IsAffine;

    [FieldOffset(0)] //todo
    public int Mode; //maybe smaller value

    [FieldOffset(0)] //todo
    public int Priority; //maybe smaller value

    [FieldOffset(0)] //todo
    public int PaletteNumber;

    [FieldOffset(0)] //todo
    public int XTiles;

    [FieldOffset(0)] //todo
    public int XCoord;

    [FieldOffset(0)] //todo
    public bool SinglePalette;

    [FieldOffset(4)]
    public RegularSpriteInfo Regular;

    [FieldOffset(4)]
    public AffineSpriteInfo Affine;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RegularSpriteInfo
{
    public int ScanlineStartMapTileNumber; //TileNumber on aff
    public int YPixelOffset; //RelativeY on reg
    public bool HFlip; //DoubleSize on aff
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AffineSpriteInfo
{
    public int TileNumber; //scanlineStartMapTileNumber on reg
    public int RelativeY; //YPixelOffset on reg
    public bool DoubleSize; //HFlip on reg
    public int YTiles;
    public short Pa;
    public short Pb;
    public short Pc;
    public short Pd;
}