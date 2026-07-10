using GbaEmulator.Core.Common;
using GbaEmulator.Core.Dma;
using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Video;

public sealed class Ppu
{
    private const int ScreenWidth = 240;
    private const int ScreenHeight = 160;
    private const int CyclesPerScanline = 1232;
    private const int ScanLinesPerFrame = 228;
    public const int CyclesPerFrame = CyclesPerScanline * ScanLinesPerFrame;

    private readonly InterruptController _interrupts;
    private readonly DmaController _dma;
    private readonly GbaMemory _memory;
    private byte[] _vram = [];
    private byte[] _paletteRam = [];
    private int _cycleAccumulator;

    public Ppu(InterruptController interrupts, DmaController dma, GbaMemory memory)
    {
        _interrupts = interrupts;
        _memory = memory;
        _dma = dma;
        FrameBuffer = new FrameBuffer(ScreenWidth, ScreenHeight);
    }

    public FrameBuffer FrameBuffer { get; }

    public void ConnectMemory(byte[] vRam, byte[] paletteRam)
    {
        _vram = vRam;
        _paletteRam = paletteRam;
    }

    public void Step(int cycles, GbaBus bus)
    {
        _cycleAccumulator += cycles;
        while (_cycleAccumulator >= CyclesPerScanline)
        {
            _cycleAccumulator -= CyclesPerScanline;
            _memory.Io.REG_VCOUNT++;

            if (_memory.Io.REG_VCOUNT == 160)
            {
                _memory.Io.REG_DISPSTAT = (ushort)BitUtils.SetBit(_memory.Io.REG_DISPSTAT, 0, true);
                _dma.RunDmas(DmaTimingType.VBlank, bus);
                var vBlankIrqEnabled = BitUtils.IsBitSet(_memory.Io.REG_DISPSTAT, 3);
                if (vBlankIrqEnabled)
                {
                    _interrupts.Request(InterruptType.VBlank);
                }
                RenderFrame();
            }

            if (_memory.Io.REG_VCOUNT == ((_memory.Io.REG_DISPSTAT >> 8) & 0xFF))
            {
                _memory.Io.REG_DISPSTAT = (ushort)BitUtils.SetBit(_memory.Io.REG_DISPSTAT, 2, true);
                var vCountIrqEnabled = BitUtils.IsBitSet(_memory.Io.REG_DISPSTAT, 5);
                if (vCountIrqEnabled)
                {
                    _interrupts.Request(InterruptType.VCounter);
                }
            }
            else
            {
                _memory.Io.REG_DISPSTAT = (ushort)BitUtils.SetBit(_memory.Io.REG_DISPSTAT, 2, false);
            }

            if (_memory.Io.REG_VCOUNT < ScanLinesPerFrame)
            {
                continue;
            }

            _memory.Io.REG_VCOUNT = 0;
            //leaving vBlank
            _memory.Io.REG_DISPSTAT = (ushort)BitUtils.SetBit(_memory.Io.REG_DISPSTAT, 0, false);
        }
    }

    public ushort Read16(uint address) =>
        address switch
        {
            0x04000000 => DisplayControl,
            0x04000004 => DisplayStatus,
            0x04000006 => VerticalCount,
            0x04000008 => Bg0Control,
            0x0400000A => Bg1Control,
            0x0400000C => Bg2Control,
            0x0400000E => Bg3Control,
            0x04000010 => Bg0HorizontalOffset,
            0x04000012 => Bg0VerticalOffset,
            0x04000014 => Bg1HorizontalOffset,
            0x04000016 => Bg1VerticalOffset,
            0x04000018 => Bg2HorizontalOffset,
            0x0400001A => Bg2VerticalOffset,
            0x0400001C => Bg3HorizontalOffset,
            0x0400001E => Bg3VerticalOffset,
            _ => 0
        };

    public void Write16(uint address, ushort value)
    {
        switch (address)
        {
            case 0x04000000:
                DisplayControl = value;
                break;
            case 0x04000004:
                DisplayStatus = (ushort)((DisplayStatus & 0x0007) | (value & 0xfff8));
                break;
            case 0x04000008:
                Bg0Control = value;
                break;
            case 0x0400000A:
                Bg1Control = value;
                Console.WriteLine("write to bg1cnt");
                break;
            case 0x0400000C:
                Bg2Control = value;
                break;
            case 0x0400000E:
                Bg3Control = value;
                break;
            case 0x04000010:
                Bg0HorizontalOffset = value;
                break;
            case 0x04000012:
                Bg0VerticalOffset = value;
                break;
            case 0x04000014:
                Bg1HorizontalOffset = (ushort)(value & 0x1FF);
                Console.WriteLine("write to bg1hofs");
                break;
            case 0x04000016:
                Bg1VerticalOffset = (ushort)(value & 0x1FF);
                Console.WriteLine("write to bg1vofs");
                break;
            case 0x04000018:
                Bg2HorizontalOffset = value;
                break;
            case 0x0400001A:
                Bg2VerticalOffset = value;
                break;
            case 0x0400001C:
                Bg3HorizontalOffset = value;
                break;
            case 0x0400001E:
                Bg3VerticalOffset = value;
                break;
            default:
                Console.WriteLine($"Writing to unmapped ppu IO register {address:x8}");
                break;
        }
    }

    private ushort ReadPalette16(int offset)
    {
        if (offset < 0 || offset + 1 >= _paletteRam.Length)
        {
            return 0;
        }

        return (ushort)(_paletteRam[offset] | (_paletteRam[offset + 1] << 8));
    }

    private byte ReadVram8(int offset)
    {
        if (offset < 0 || offset + 1 >= _vram.Length)
        {
            return 0;
        }

        return _vram[offset];
    }

    private ushort ReadVram16(int offset)
    {
        if (offset < 0 || offset + 1 >= _vram.Length)
        {
            return 0;
        }
        return (ushort)(_vram[offset] | (_vram[offset + 1] << 8));
    }

    private uint ReadBgPaletteColor(int paletteIndex)
    {
        var offset = paletteIndex * 2;
        var bgr555 = ReadPalette16(offset);
        return ConvertBgr555ToArgb(bgr555);
    }

    private void RenderFrame()
    {
        var modeBits = _memory.Io.REG_DISPCNT & 0x7; //bits 0-2
        switch (modeBits)
        {
            case 0:
                RenderMode0();
                break;
            case 1:
                //render mode 1
                break;
            case 2:
                //render mode 2
                break;
            case 3:
                //render mode 3
                RenderMode3();
                break;
            case 4:
                //render mode 4
                break;
            case 5:
                //render mode 5
                break;
            default:
                //not valid mode just render jibber jabber
                RenderFallbackPattern();
                break;
        }
    }

    private void RenderMode0()
    {
        //tiles are arrrays of indices into palette memory
        // bg are arrays of indices into tilemaps
        //charblocks tileset
        //screenblocks tilemap
        HashSet<int> graphicsOffsets = [];
        int countofG = 0;
        var bg0Enabled = BitUtils.IsBitSet(DisplayControl, 8);
        var bg1Enabled = BitUtils.IsBitSet(DisplayControl, 9);
        var bg2Enabled = BitUtils.IsBitSet(DisplayControl, 10);
        var bg3Enabled = BitUtils.IsBitSet(DisplayControl, 11);
        if (bg0Enabled || bg1Enabled || bg2Enabled || bg3Enabled)
        {
            var x = 1;
            //var z = _vram.Count(q => q != 0);
        }
        var charBaseBlock = (Bg1Control >> 2) & 0b11;
        var startOffsetOfCharTileData = charBaseBlock * 0x4000; // + 0x0600000 for address
        var screenBaseBlock = (Bg1Control >> 8) & 0x1F;
        var startOffsetOfCharTileMap = screenBaseBlock * 0x800; // + 0x0600000 for address
        // 00 = 256x256 (32x32 tiles)
        // 01 = 512x256 (64x32 tiles)
        // 10 = 256x512 (32x64 tiles)
        // 11 = 512x512 (64x64 tiles)
        var tileMapSizeText = (Bg1Control >> 14) & 0b11;
        if (tileMapSizeText != 0)
        {
            var z = 1;
        }
        var is8bpp = BitUtils.IsBitSet(Bg1Control, 7);

        for (var y = 0; y < ScreenHeight; y++)
        {
            var backgroundY = (y + Bg1VerticalOffset) & 0xFF;
            var tileMapY = backgroundY >> 3;
            var pixelYInsideTile = backgroundY & 7;

            for (var x = 0; x < ScreenWidth; x++)
            {
                var backgroundX = (x + Bg1HorizontalOffset) & 0xFF;
                var tileMapX = backgroundX / 8;
                var pixelXInsideTile = backgroundX % 8;

                var tileMapIndex = tileMapY * 32 + tileMapX;
                var tileMapEntryOffset = startOffsetOfCharTileMap + tileMapIndex * 2;

                var tileMapEntry = ReadVram16(tileMapEntryOffset);
                var hFlip = (tileMapEntry & 0x0400) != 0;
                var vFlip = (tileMapEntry & 0x0800) != 0;
                if (vFlip || hFlip)
                {
                    var f = 1;
                }
                var tileIndex = tileMapEntry & 0x03FF;
                var paletteBank = (tileMapEntry >> 12) & 0xF;

                var tileGraphicsOffset = startOffsetOfCharTileData + tileIndex * 32;

                var tileRowOffset = tileGraphicsOffset + pixelYInsideTile * 4;
                var tilePixelPairOffset = tileRowOffset + pixelXInsideTile / 2;

                graphicsOffsets.Add(tileGraphicsOffset);
                if (tileGraphicsOffset == 0x44e0)
                {
                    var l = 1;
                    countofG++;
                }
                var twoPackedPixelIndexes = ReadVram8(tilePixelPairOffset);

                var colorIndex = (pixelXInsideTile % 2) == 0
                    ? twoPackedPixelIndexes & 0x0F
                    : twoPackedPixelIndexes >> 4;

                if (colorIndex == 0)
                {
                    //var backDrop = ReadBgPaletteColor(0);
                    //FrameBuffer.SetPixel(x, y, backDrop);
                    //continue;
                }

                var paletteIndex = paletteBank * 16 + colorIndex;
                var color = ReadBgPaletteColor(paletteIndex);

                FrameBuffer.SetPixel(x, y, color);
            }
        }
    }

    public void RenderMode1()
    {
        
    }

    private void RenderMode3()
    {
        for (var y = 0; y < ScreenHeight; y++)
        {
            for (var x = 0; x < ScreenWidth; x++)
            {
                var offset = ((y * ScreenWidth) + x) * 2;
                if (offset + 1 >= _vram.Length)
                {
                    FrameBuffer.SetPixel(x, y, 0xFF000000);
                    continue;
                }

                var bgr555 = (ushort)(_vram[offset] | (_vram[offset + 1] << 8));
                FrameBuffer.SetPixel(x, y, ConvertBgr555ToArgb(bgr555));
            }
        }
    }

    private void RenderFallbackPattern()
    {
        for (var y = 0; y < ScreenHeight; y++)
        {
            for (var x = 0; x < ScreenWidth; x++)
            {
                var red = (byte)(x * 255 / ScreenWidth);
                var green = (byte)(y * 255 / ScreenHeight);
                var blue = (byte)(((x / 8) ^ (y / 8)) * 18);
                FrameBuffer.SetPixel(x, y, 0xFF000000U | ((uint)red << 16) | ((uint)green << 8) | blue);
            }
        }
    }

    private static uint ConvertBgr555ToArgb(ushort value)
    {
        var red = (byte)((value & 0x1F) * 255 / 31);
        var green = (byte)(((value >> 5) & 0x1F) * 255 / 31);
        var blue = (byte)(((value >> 10) & 0x1F) * 255 / 31);
        return 0xFF000000U | ((uint)red << 16) | ((uint)green << 8) | blue;
    }
}