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
    private const int HBlankStartCycle = 1006;
    public const int CyclesPerFrame = CyclesPerScanline * ScanLinesPerFrame;

    private readonly InterruptController _interrupts;
    private readonly DmaController _dma;
    private readonly GbaMemory _memory;

    public Ppu(InterruptController interrupts, DmaController dma, GbaMemory memory)
    {
        _interrupts = interrupts;
        _memory = memory;
        _dma = dma;
        FrameBuffer = new FrameBuffer(ScreenWidth, ScreenHeight);
    }

    public FrameBuffer FrameBuffer { get; }

    private int _scanlineCycle;
    private bool IsInHBlank => _scanlineCycle >= HBlankStartCycle;

    public void Step(int cycles, GbaBus bus)
    {
        while (cycles > 0)
        {
            int nextBoundary = IsInHBlank ? CyclesPerScanline : HBlankStartCycle;
            int cyclesUntilBoundary = nextBoundary - _scanlineCycle;
            int consumed = Math.Min(cycles, cyclesUntilBoundary);

            _scanlineCycle += consumed;
            cycles -= consumed;

            if (_scanlineCycle != nextBoundary)
            {
                continue; //continue til hblank start,
            }

            if (nextBoundary == HBlankStartCycle)
            {
                EnterHBlank(bus); // enter hblank, trigger dma, interrupt, and renderScanLine
            }
            else
            {
                EndScanline(bus); // leave hblank, update vcount, enter vblank when appropriate
            }
        }
    }

    private void EnterHBlank(GbaBus bus)
    {
        _memory.Io.REG_DISPSTAT = (ushort)BitUtils.SetBit(_memory.Io.REG_DISPSTAT, 1, true); //set hblank

        if (_memory.Io.REG_VCOUNT < ScreenHeight)
        {
            _dma.RunDmas(DmaTimingType.Hblank, bus);
        }

        if (BitUtils.IsBitSet(_memory.Io.REG_VCOUNT, 4)) // hlbank irq enabled
        {
            _interrupts.Request(InterruptType.HBlank);
        }

        if (_memory.Io.REG_VCOUNT < ScreenHeight) //Only Render Visible scan lines
        {
            RenderScanLine(_memory.Io.REG_VCOUNT);
        }
    }

    private void EndScanline(GbaBus bus)
    {
        _memory.Io.REG_DISPSTAT = (ushort)BitUtils.SetBit(_memory.Io.REG_DISPSTAT, 1, false); // leave hblank unset bit

        _scanlineCycle = 0;

        int nextLine = _memory.Io.REG_VCOUNT + 1;

        if (nextLine == ScanLinesPerFrame)
        {
            nextLine = 0;
        }

        _memory.Io.REG_VCOUNT = (ushort)nextLine;

        UpdateVBlankState(bus); // on scanline 160 enter blank, and do dma and interrupt, on zero leave blank
        UpdateVCountMatch(); // check if vcount triggered and do interrupt
    }

    private void UpdateVBlankState(GbaBus bus)
    {
        ushort vcount = _memory.Io.REG_VCOUNT;

        switch (vcount)
        {
            case ScreenHeight:
                {
                    _memory.Io.REG_DISPSTAT = (ushort)BitUtils.SetBit(_memory.Io.REG_DISPSTAT, 0, true);

                    _dma.RunDmas(DmaTimingType.VBlank, bus);

                    if (BitUtils.IsBitSet(_memory.Io.REG_DISPSTAT, 3)) //vblank irq enabled
                    {
                        _interrupts.Request(InterruptType.VBlank);
                    }

                    break;
                }
            case 0:
                _memory.Io.REG_DISPSTAT = (ushort)BitUtils.SetBit(_memory.Io.REG_DISPSTAT, 0, false); // leave vblank
                break;
        }
    }

    private void UpdateVCountMatch()
    {
        ushort dispStat = _memory.Io.REG_DISPSTAT;
        int compareValue = (dispStat >> 8) & 0xFF;
        bool wasMatching = BitUtils.IsBitSet(dispStat, 2); //vcount triggered status
        bool isMatching = _memory.Io.REG_VCOUNT == compareValue; //trigger if line trigger from DISPSTAT equals vcount reg
        _memory.Io.REG_DISPSTAT = (ushort)BitUtils.SetBit(_memory.Io.REG_DISPSTAT, 2, isMatching); //set based on line trigger

        if (!wasMatching &&
            isMatching &&
            BitUtils.IsBitSet(_memory.Io.REG_DISPSTAT, 5)) //trigger interrupt if vcount enabled and if vcount == trigger vlaue and wasnt already set
        {
            _interrupts.Request(InterruptType.VCounter);
        }
    }

    private ushort ReadPalette16(int offset)
    {
        if (offset < 0 || offset + 1 >= _memory.PaletteRam.Length)
        {
            return 0;
        }

        return (ushort)(_memory.PaletteRam[offset] | (_memory.PaletteRam[offset + 1] << 8));
    }

    private byte ReadVram8(int offset)
    {
        if (offset < 0 || offset + 1 >= _memory.Vram.Length)
        {
            return 0;
        }

        return _memory.Vram[offset];
    }

    private ushort ReadVram16(int offset)
    {
        if (offset < 0 || offset + 1 >= _memory.Vram.Length)
        {
            return 0;
        }
        return (ushort)(_memory.Vram[offset] | (_memory.Vram[offset + 1] << 8));
    }

    private uint ReadBgPaletteColor(int paletteIndex)
    {
        var offset = paletteIndex * 2;
        var bgr555 = ReadPalette16(offset);
        return ConvertBgr555ToArgb(bgr555);
    }

    private void RenderScanLine(int scanLine)
    {
        var modeBits = _memory.Io.REG_DISPCNT & 0x7; //bits 0-2
        switch (modeBits)
        {
            case 0:
                RenderMode0(scanLine);
                break;
            case 1:
                //render mode 1
                break;
            case 2:
                //render mode 2
                break;
            case 3:
                //render mode 3
                RenderMode3(scanLine);
                break;
            case 4:
                //render mode 4
                RenderMode4(scanLine);
                break;
            case 5:
                //render mode 5
                break;
            default:
                //not valid mode just render jibber jabber
                RenderFallbackPattern(scanLine);
                break;
        }
    }

    private void RenderMode0(int y)
    {
        //tiles are arrrays of indices into palette memory
        // bg are arrays of indices into tilemaps
        //charblocks tileset
        //screenblocks tilemap
        HashSet<int> graphicsOffsets = [];
        int countofG = 0;
        var bg0Enabled = BitUtils.IsBitSet(_memory.Io.REG_DISPCNT, 8);
        var bg1Enabled = BitUtils.IsBitSet(_memory.Io.REG_DISPCNT, 9);
        var bg2Enabled = BitUtils.IsBitSet(_memory.Io.REG_DISPCNT, 10);
        var bg3Enabled = BitUtils.IsBitSet(_memory.Io.REG_DISPCNT, 11);
        if (bg0Enabled || bg1Enabled || bg2Enabled || bg3Enabled)
        {
            var x = 1;
            //var z = _vram.Count(q => q != 0);
        }
        var charBaseBlock = (_memory.Io.REG_BG1CNT >> 2) & 0b11;
        var startOffsetOfCharTileData = charBaseBlock * 0x4000; // + 0x0600000 for address
        var screenBaseBlock = (_memory.Io.REG_BG1CNT >> 8) & 0x1F;
        var startOffsetOfCharTileMap = screenBaseBlock * 0x800; // + 0x0600000 for address
        // 00 = 256x256 (32x32 tiles)
        // 01 = 512x256 (64x32 tiles)
        // 10 = 256x512 (32x64 tiles)
        // 11 = 512x512 (64x64 tiles)
        var tileMapSizeText = (_memory.Io.REG_BG1CNT >> 14) & 0b11;
        if (tileMapSizeText != 0)
        {
            var z = 1;
        }
        var is8bpp = BitUtils.IsBitSet(_memory.Io.REG_BG1CNT, 7);

        var backgroundY = (y + _memory.Io.REG_BG1VOFS) & 0xFF;
        var tileMapY = backgroundY >> 3;
        var pixelYInsideTile = backgroundY & 7;

        for (var x = 0; x < ScreenWidth; x++)
        {
            var backgroundX = (x + _memory.Io.REG_BG1HOFS) & 0xFF;
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

    public void RenderMode1(int y)
    {
        
    }

    private void RenderMode3(int y)
    {
        for (var x = 0; x < ScreenWidth; x++)
        {
            var offset = ((y * ScreenWidth) + x) * 2;
            if (offset + 1 >= _memory.Vram.Length)
            {
                FrameBuffer.SetPixel(x, y, 0xFF000000);
                continue;
            }

            var bgr555 = (ushort)(_memory.Vram[offset] | (_memory.Vram[offset + 1] << 8));
            FrameBuffer.SetPixel(x, y, ConvertBgr555ToArgb(bgr555));
        }
    }

    private void RenderMode4(int y)
    {
        var useFrame1 = BitUtils.IsBitSet(_memory.Io.REG_DISPCNT, 4);
        var dispCnt = _memory.Io.REG_DISPCNT;
        var bg2 = _memory.Io.REG_BG2CNT;
        var bg2hofs = _memory.Io.REG_BG2HOFS;
        var bg2vofs = _memory.Io.REG_BG2VOFS;
        var bg2x = _memory.Io.REG_BG2X;
        var bg2y = _memory.Io.REG_BG2Y;
        var bg2pa = _memory.Io.REG_BG2PA;
        var bg2pb = _memory.Io.REG_BG2PB;
        var bg2pc = _memory.Io.REG_BG2PC;
        var bg2pd = _memory.Io.REG_BG2PD;

        if (!BitUtils.IsBitSet(_memory.Io.REG_DISPCNT, 10))
        {
            return;
        }

        if (y == 76)
        {
            var x = 1;
        }

        for (var x = 0; x < ScreenWidth; x++)
        {
            var startOffset = !useFrame1 ? 0 : 0xA000;
            var vramPixelOffset = (y * ScreenWidth) + x + startOffset;
            var paletteIndex = ReadVram8(vramPixelOffset);
            if (paletteIndex != 0)
            {
                var z = 0;
            }
            var color = ReadBgPaletteColor(paletteIndex);

            FrameBuffer.SetPixel(x, y, color);
        }
    }

    private void RenderFallbackPattern(int y)
    {
        for (var x = 0; x < ScreenWidth; x++)
        {
            var red = (byte)(x * 255 / ScreenWidth);
            var green = (byte)(y * 255 / ScreenHeight);
            var blue = (byte)(((x / 8) ^ (y / 8)) * 18);
            FrameBuffer.SetPixel(x, y, 0xFF000000U | ((uint)red << 16) | ((uint)green << 8) | blue);
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