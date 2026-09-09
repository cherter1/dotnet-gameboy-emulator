using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GbaEmulator.Core.Video;

public sealed class FrameBuffer
{
    public int Width { get; }
    public int Height { get; }
    public Span<uint> Pixels => _pixels;
    private readonly uint[] _pixels;

    public FrameBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new uint[width * height];
    }

    public void SetPixel(int x, int y, uint argb)
    {
        if ((uint)x >= Width || (uint)y >= Height)
        {
            return;
        }

        _pixels[y * Width + x] = argb;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillScanline(int scanline, uint value)
    {
        Pixels.Slice(scanline * Width, Width).Fill(value);
    }

    public void CopyToBgra32(Span<byte> destination)
    {
        MemoryMarshal.AsBytes(Pixels).CopyTo(destination);
    }
}