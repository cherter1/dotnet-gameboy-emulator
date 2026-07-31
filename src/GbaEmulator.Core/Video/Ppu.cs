using System.Runtime.CompilerServices;
using GbaEmulator.Core.Common;
using GbaEmulator.Core.Dma;
using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Video.Sprites;

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
        if (BitUtils.IsBitSet(_memory.Io.REG_DISPCNT, 7)) //if bit 7 is set force blank
        {
            modeBits = 0xff;
        }

        switch (modeBits)
        {
            case 0: //tile/map based text mode
                RenderMode0(scanLine);
                break;
            case 1: //tile/map based text mode
                RenderMode1(scanLine);
                break;
            case 2: //tile/map based rotation/scaling mode
                RenderMode2(scanLine);
                break;
            case 3: //bitmap based mode for still images
                RenderMode3(scanLine);
                break;
            case 4: //bitmap based mode
                RenderMode4(scanLine);
                break;
            case 5: //bitmap based mode
                //render mode 5
                break;
            default:
                //should only be used when forceBlank bit is set in DisplayControl register
                FrameBuffer.FillScanline(scanLine, 0xffffff1d); //tmep yellow color for testing
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

    private void RenderMode2(int y)
    {
        var displayControl = _memory.Io.REG_DISPCNT;

        var bg2Enabled = BitUtils.IsBitSet(displayControl, 10);
        var bg3Enabled = BitUtils.IsBitSet(displayControl, 11);
        var objEnable = BitUtils.IsBitSet(displayControl, 12);
        if (!bg2Enabled && !bg3Enabled && !objEnable)
        {
            return;
        }

        var bg2Control = _memory.Io.REG_BG2CNT;
        var bg3Control = _memory.Io.REG_BG3CNT;

        var bg3x = _memory.Io.REG_BG3X;
        var bg3y = _memory.Io.REG_BG3Y;

        //8bpp mode only for r/s bg
        Span<ScanlineSpriteInfo> sprites = stackalloc ScanlineSpriteInfo[128];
        var spriteCount = SpriteStuff_TempName(y, sprites);

        for (int x = 0; x < ScreenWidth; x++)
        {
            //bg3
            var wrapAround = BitUtils.IsBitSet(bg3Control, 13);
            // r/s backgrounds
            // 00 = 128x128 (16x16 tiles)
            // 01 = 256x256 (32x32 tiles)
            // 10 = 512x512 (64x64 tiles)
            // 11 = 1024x1024 (128x128 tiles)
            var tileMapSizeMode = (bg3Control >> 14) & 0b11; //maybe prvt getsize method?

            var charBaseBlock = (bg3Control >> 2) & 0b11; //bgXcnt bits 2-3
            var charDataStartOffset = charBaseBlock * 0x4000;
            var screenBaseBlock = (bg3Control >> 8) & 0x1F; //bgXcnt bits 8-12
            var tileMapStartOffset = screenBaseBlock * 0x800;

            var tileMapIndex = ((y / 8) * 32) // change 32 to the number of tiles in a row
                               + (x / 8);
            var mapAddress = tileMapStartOffset + tileMapIndex;
            var tileNumber = ReadVram8(mapAddress);
            var currentTileStartOffset = charDataStartOffset + tileNumber * 64; //size of tile in bytes
            var yTileOffset = y % 8;
            var xTileOffset = x % 8;
            var tilePixelOffset = (yTileOffset * 8) + xTileOffset;
            var currentPixelDataOffset = currentTileStartOffset + tilePixelOffset;
            var paletteIndex = ReadVram8(currentPixelDataOffset);
            var color = ReadBgPaletteColor(paletteIndex);

            if (bg3Enabled) //temp
            {
                FrameBuffer.SetPixel(x, y, color);
            }

            //sprites
            //sprite map data starts at 0x06010000
            //sprite palette starts at 0x05000200
            //0
            //20 26  |  ad c2  |  40 0a  |  00 01
            //6
            //20 26 |  22  ce  |  00 08  |  00 00
            //7
            //66 64  |  37 c0  |  40 5b  |  04 00
            //6
            //66 64  |  77 c0  |  50 5b  |  04 00
            if (y == 112 && x == 73)
            {
                var z = 1;
            }

            for (int objIndex = 0; objIndex < spriteCount; objIndex++)
            {
                ref readonly var sprite = ref sprites[objIndex];

                bool objXRange = (x >= sprite.XCoord) && (x < sprite.XCoord + (sprite.NumXTiles * (sprite.IsSinglePalette ? 8 : 4)));
                if (!objXRange)
                {
                    continue;
                }

                var currentXTileNumber = (x - sprite.XCoord) / 8;
                var currentXPixelNumber = (x - sprite.XCoord) % 8;
                var currentMapTileNumber = sprite.ScanlineStartMapTileNumber + currentXTileNumber;
                var currentTileOffset = 0x10000 + (currentMapTileNumber * (sprite.IsSinglePalette ? 0x40 : 0x20));
                var currentPixOffset = currentTileOffset + sprite.YPixelOffset + currentXPixelNumber;
                var objPaletteIndex = ReadVram8(currentPixOffset);
                if (objPaletteIndex == 0)
                {
                    continue; // dont draw if zero index, pixel should be transparent
                }
                var objPixelColor = ReadObjPaletteColor(objPaletteIndex);
                FrameBuffer.SetPixel(x, y, objPixelColor);
            }
        }
    }

    private int SpriteStuff_TempName(int y, Span<ScanlineSpriteInfo> sprites)
    {
        int count = 0;
        var oam = _memory.Oam.AsSpan();

        //int maxCyclesPerLine = 1210; //1210 only for dispcnt bit5 == 0, if bit5 set max cycles becomes 954
        //int objRenderingCyclesUsed = 0;
        //int priorityLine = 0xff;
        for (int oamAttrOffset = 0; oamAttrOffset < 1016; oamAttrOffset += 8) //loop runs for sprites 0-127
        {
            var attr0Value = ReadOam16(oam, oamAttrOffset);
            var attr0 = new ObjAttribute0(attr0Value);
            var isSinglePalette = attr0.IsSinglePalette; //just here for later so i remember
            if (attr0 is { IsRotationScaling: false, IsDisabled: true })
            {
                continue; //disabled bit only if not r/s obj otherwise its IsDoubleSize
            }

            if (attr0.IsRotationScaling)
            {
                //continue; //temp
                //do affine
            }

            var attr1Value = ReadOam16(oam, oamAttrOffset + 2);
            var attr1 = new ObjAttribute1(attr1Value);

            var shapeSize = (attr0.ObjShape << 2) | attr1.ObjSize; //low two bits attr1 size
            SpriteHelpers.GetSpriteSizeTiles(shapeSize, out int xTiles, out int yTiles);

            //only * 8 for 8bpp mode in 4bpp mode its * 4 since each byte represents 2 pix of a tile
            bool objYRange = (y >= attr0.YCoord) && (y < attr0.YCoord + (yTiles * (isSinglePalette ? 8 : 4)));

            if (!objYRange)
            {
                continue;
            }

            var attr2Value = ReadOam16(oam, oamAttrOffset + 4);
            var attr2 = new ObjAttribute2(attr2Value);

            var yAbsolute = y - attr0.YCoord;

            var yPixelOffset = (yAbsolute % 8) * 4;
            var startTileNumber = attr2.TileNumber;
            var twoDMatrixSize = 32;

            if (isSinglePalette)
            {
                startTileNumber /= 2;
                yPixelOffset *= 2;
                twoDMatrixSize = 16;
            }

            var currentYTileNumber = yAbsolute / 8;
            int scanlineStartMapTileNumber;
            if ((_memory.Io.REG_DISPCNT & 0x40) == 0x40) //bit 6 set then 1d char mapping
            {

                scanlineStartMapTileNumber = startTileNumber + (currentYTileNumber * xTiles);
            }
            else //bit 6 clear 2d character mapping
            {
                scanlineStartMapTileNumber = startTileNumber + (currentYTileNumber * twoDMatrixSize);
            }

            var regSpriteInfo = new ScanlineSpriteInfo(scanlineStartMapTileNumber, isSinglePalette, attr2.PaletteNumber, yPixelOffset,
                attr2.Priority, xTiles, attr1.XCoord);
            //add to list for display reg sprites
            sprites[count++] = regSpriteInfo;

            //affine only affine
            //ushort attr3 = ReadOam16(oam, oamAttrOffset + 6);
        }

        return count;
    }

    private void oldtemp(int y, int x)
    {
        for (int s = 0; s < 128; s++)
        {
            var startOffset = s * 8;

            var attr0Value = ReadOam16(startOffset);
            var attr0 = new ObjAttribute0(attr0Value);
            if (!attr0.IsRotationScaling && attr0.IsDisabled)
            {
                continue; //disabled bit only if not r/s obj otherwise its IsDoubleSize
            }

            var attr1Value = ReadOam16(startOffset + 2);
            var attr1 = new ObjAttribute1(attr1Value);
            var shapeSize = (attr0.ObjShape << 2) | attr1.ObjSize;
            SpriteHelpers.GetSpriteSizeTiles(shapeSize, out int xTiles, out int yTiles);

            var attr2Value = ReadOam16(startOffset + 4);
            var attr2 = new ObjAttribute2(attr2Value);
            //var spriteStartTileOffset = attr2.TileNumber * 32; // + 0x06010000 may need to divide by 2 based on palette mode if not divisible correctly
            //only for non r/s sprites
            bool objYRange = (y >= attr0.YCoord) && (y < attr0.YCoord + (yTiles * 8));
            bool objXRange = (x >= attr1.XCoord) && (x < attr1.XCoord + (xTiles * 8));
            if (objYRange && objXRange)
            {
                //check priority
                var currentXTileNumber = (x - attr1.XCoord) / 8;
                var currentXPixelNumber = (x - attr1.XCoord) % 8;
                var currentYTileNumber = (y - attr0.YCoord) / 8;
                var currentYPixelNumber = (y - attr0.YCoord) % 8;
                // DISPCNT bit 6 cleared (2d character mapping) && 8bpp 16x32 tiles
                //div 2 on tilenumber only in 8bpp
                var currentMapTileNumber = (attr2.TileNumber / 2) + (currentYTileNumber * 16) + currentXTileNumber;
                var currentTileOffset = (currentMapTileNumber * 0x40) + 0x10000; //0x40 is tile size in 8bppMode
                //only for single palette mode else * 4 bc 4bpp
                var currentPixOffset = currentTileOffset + (currentYPixelNumber * 8) + currentXPixelNumber;
                var objPaletteIndex = ReadVram8(currentPixOffset);
                if (objPaletteIndex == 0)
                {
                    //continue; // dont draw if zero index, pixel should be transparent
                }
                var objPixelColor = ReadObjPaletteColor(objPaletteIndex);
                FrameBuffer.SetPixel(x, y, objPixelColor);
            }

            ushort attr3 = ReadOam16(startOffset + 6);
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ReadOam16(ReadOnlySpan<byte> oam, int offset)
    {
        return (ushort)((oam[offset + 1] << 8) | oam[offset]);
    }

    private ushort ReadOam16(int offset)
    {
        if (offset < 0 || offset + 1 >= _memory.Oam.Length)
        {
            return 0;
        }
        return (ushort)(_memory.Oam[offset] | (_memory.Oam[offset + 1] << 8));
    }

    private void RenderMode3(int y)
    {
        for (var x = 0; x < ScreenWidth; x++)
        {
            var offset = ((y * ScreenWidth) + x) * 2;

            var bgr555 = ReadVram16(offset);
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

        for (var x = 0; x < ScreenWidth; x++)
        {
            var startOffset = !useFrame1 ? 0 : 0xA000;
            var vramPixelOffset = (y * ScreenWidth) + x + startOffset;
            var paletteIndex = ReadVram8(vramPixelOffset);

            var color = ReadBgPaletteColor(paletteIndex);

            FrameBuffer.SetPixel(x, y, color);
        }
    }

    private uint ReadObjPaletteColor(int paletteIndex)
    {
        var offset = paletteIndex * 2;
        var bgr555 = ReadPalette16(offset + 0x200);
        return ConvertBgr555ToArgb(bgr555);
    }

    private static uint ConvertBgr555ToArgb(ushort value)
    {
        var red = (byte)((value & 0x1F) * 255 / 31);
        var green = (byte)(((value >> 5) & 0x1F) * 255 / 31);
        var blue = (byte)(((value >> 10) & 0x1F) * 255 / 31);
        return 0xFF000000U | ((uint)red << 16) | ((uint)green << 8) | blue;
    }
}