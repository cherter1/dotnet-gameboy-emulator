namespace GbaEmulator.Core.Video;

public static class SpecialEffectsHelper
{
    public static ushort AlphaBlendPixels(ushort t1Bgr555Color, ushort t2Bgr555Color, ushort blendAlphaReg)
    {
        float coEfA = (float)(blendAlphaReg & 0x1f) / 16;
        float coEfB = (float)((blendAlphaReg >> 8) & 0x1f) / 16;

        var t1Red = t1Bgr555Color & 0x1f;
        var t1Green = (t1Bgr555Color >> 5) & 0x1f;
        var t1Blue = (t1Bgr555Color >> 10) & 0x1f;

        var t2Red = t2Bgr555Color & 0x1f;
        var t2Green = (t2Bgr555Color >> 5) & 0x1f;
        var t2Blue = (t2Bgr555Color >> 10) & 0x1f;

        //blend
        var red = Math.Min(31, (t1Red * coEfA) + (t2Red * coEfB));
        var green = Math.Min(31, (t1Green * coEfA) + (t2Green * coEfB));
        var blue = Math.Min(31, (t1Blue * coEfA) + (t2Blue * coEfB));

        return (ushort)((uint)red | ((uint)green << 5) | ((uint)blue << 10));
    }
}