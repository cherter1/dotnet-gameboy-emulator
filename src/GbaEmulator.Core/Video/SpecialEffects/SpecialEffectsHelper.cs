namespace GbaEmulator.Core.Video.SpecialEffects;

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

    public static ushort LightenBlend(ushort t1Bgr555Color, ushort blendYReg)
    {
        var coEf = (float)(blendYReg & 0x1f) / 16;

        var t1Red = t1Bgr555Color & 0x1f;
        var t1Green = (t1Bgr555Color >> 5) & 0x1f;
        var t1Blue = (t1Bgr555Color >> 10) & 0x1f;

        var redInc = t1Red + (31 - t1Red) * coEf;
        var blueInc = t1Blue + (31 - t1Blue) * coEf;
        var greenInc = t1Green + (31 - t1Green) * coEf;

        return (ushort)((uint)redInc | ((uint)greenInc << 5) | ((uint)blueInc << 10));
    }

    public static ushort DarkenBlend(ushort t1Bgr555Color, ushort blendYReg)
    {
        var coEf = (float)(blendYReg & 0x1f) / 16;

        var t1Red = t1Bgr555Color & 0x1f;
        var t1Green = (t1Bgr555Color >> 5) & 0x1f;
        var t1Blue = (t1Bgr555Color >> 10) & 0x1f;

        var redDec = t1Red - t1Red * coEf;
        var blueDec = t1Blue - t1Blue * coEf;
        var greenDec = t1Green - t1Green * coEf;

        return (ushort)((uint)redDec | ((uint)greenDec << 5) | ((uint)blueDec << 10));
    }
}