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

    public void CopyToBgra32(Span<byte> destination)
    {
        for (var i = 0; i < _pixels.Length; i++)
        {
            var color = _pixels[i];
            var offset = i * 4;
            destination[offset] = (byte)(color & 0xFF);
            destination[offset + 1] = (byte)((color >> 8) & 0xFF);
            destination[offset + 2] = (byte)((color >> 16) & 0xFF);
            destination[offset + 3] = (byte)((color >> 24) & 0xFF);
        }
    }
}