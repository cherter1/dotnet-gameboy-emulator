using System.Runtime.CompilerServices;
using GbaEmulator.Core.Common;
using GbaEmulator.Core.Dma;
using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Video.Backgrounds;
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
    //private bool IsInHBlank => _scanlineCycle >= HBlankStartCycle;

    public void Step(int cycles, GbaBus bus)
    {
        while (cycles > 0)
        {
            //if is in HBlank then cycle boundary is start of next scanline otherwise its in the start of HBlank
            int nextBoundary = _scanlineCycle >= HBlankStartCycle ? CyclesPerScanline : HBlankStartCycle;
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
                FrameBuffer.FillScanline(scanLine, 0xffffff00); //tmep yellow color for testing
                break;
        }
    }

    private void RenderMode0(int y)
    {
        Span<byte> vram = _memory.Vram.AsSpan();
        var displayControl = _memory.Io.REG_DISPCNT;

        ReadOnlySpan<ushort> bgControls =
        [
            _memory.Io.REG_BG0CNT, _memory.Io.REG_BG1CNT, _memory.Io.REG_BG2CNT, _memory.Io.REG_BG3CNT
        ];
        ReadOnlySpan<ushort> hofsTable =
        [
            _memory.Io.REG_BG0HOFS, _memory.Io.REG_BG1HOFS,
            _memory.Io.REG_BG2HOFS, _memory.Io.REG_BG3HOFS
        ];
        ReadOnlySpan<ushort> vofsTable =
        [
            _memory.Io.REG_BG0VOFS, _memory.Io.REG_BG1VOFS,
            _memory.Io.REG_BG2VOFS, _memory.Io.REG_BG3VOFS
        ];
        Span<int> bgOutputBuffer = stackalloc int[4];
        int enabledBgCount = FastSortBackgroundsByPriority(displayControl, bgControls, bgOutputBuffer);
        Span<int> activeBgs = bgOutputBuffer[..enabledBgCount];

        Span<int> bgScanlineInfo = stackalloc int[7 * activeBgs.Length];
        foreach (var bgIdx in activeBgs)
        {
            var tileDataStartOffset = ((bgControls[bgIdx] >> 2) & 0b11) * 0x4000; // + 0x0600000 for address
            var tileMapStartOffset = ((bgControls[bgIdx] >> 8) & 0x1f) * 0x800; // + 0x0600000 for address
            var tileMapSize = (bgControls[bgIdx] >> 14) & 0b11;
            BackgroundHelpers.GetTextBackgroundSizeTiles(tileMapSize, out var xTiles, out var yTiles);
            var bgYStartOffset = y + (vofsTable[bgIdx] & 0xff);
            var tileY = bgYStartOffset >> 3; // div 8 to count tiles from offset
            var pixelYInTile = bgYStartOffset & 7; // modulo 8 for pixel 0-7 on x axis

            bgScanlineInfo[bgIdx] = tileDataStartOffset;
            bgScanlineInfo[(bgIdx * 7) + 1] = tileMapStartOffset;
            bgScanlineInfo[(bgIdx * 7) + 2] = xTiles;
            bgScanlineInfo[(bgIdx * 7) + 3] = yTiles;
            bgScanlineInfo[(bgIdx * 7) + 4] = tileY;
            bgScanlineInfo[(bgIdx * 7) + 5] = pixelYInTile;
            bgScanlineInfo[(bgIdx * 7) + 6] = (bgControls[bgIdx] >> 7) & 0b1; // set is 8bpp mode else 4bpp
        }

        var backgroundY = (y + _memory.Io.REG_BG1VOFS) & 0xFF;
        var tileMapY = backgroundY >> 3;
        var pixelYInsideTile = backgroundY & 7;

        for (var x = 0; x < ScreenWidth; x++)
        {
            foreach (var bgIdx in activeBgs)
            {
                var bgXStartOffset = x + (hofsTable[bgIdx] & 0xff);
                var tileX = bgXStartOffset >> 3; // div 8 to count tiles from offset
                var pixelXInTile = bgXStartOffset & 7; // modulo 8 for pixel 0-7 on x axis

                var tileMapIndex = (bgScanlineInfo[(bgIdx * 7) + 4] * bgScanlineInfo[(bgIdx * 7) + 3]) + tileX; //tileY * numXTiles + tileX
                var tileMapEntryOffset = bgScanlineInfo[(bgIdx * 7) + 1] + tileMapIndex * 2; //tileMapStartOffset + mapIndex * mapEntrySize
                var tileMapEntry = ReadVram16(vram, tileMapEntryOffset);

                //TODO: flipping

                var tileIndex = tileMapEntry & 0x03ff;
            }

            //var hFlip = (tileMapEntry & 0x0400) != 0;
            //var vFlip = (tileMapEntry & 0x0800) != 0;

            //var tileIndex = tileMapEntry & 0x03FF;
            //var paletteBank = (tileMapEntry >> 12) & 0xF;

            //var tileGraphicsOffset = startOffsetOfCharTileData + tileIndex * 32;

            //var tileRowOffset = tileGraphicsOffset + pixelYInsideTile * 4;
            //var tilePixelPairOffset = tileRowOffset + pixelXInsideTile / 2;

            //var twoPackedPixelIndexes = ReadVram8(tilePixelPairOffset);

            //var colorIndex = (pixelXInsideTile % 2) == 0
                //? twoPackedPixelIndexes & 0x0F
                //: twoPackedPixelIndexes >> 4;

            //if (colorIndex == 0)
            {
                //var backDrop = ReadBgPaletteColor(0);
                //FrameBuffer.SetPixel(x, y, backDrop);
                //continue;
            }

            //var paletteIndex = paletteBank * 16 + colorIndex;
            //var color = ReadBgPaletteColor(paletteIndex);

            //FrameBuffer.SetPixel(x, y, color);
        }
    }

    public void RenderMode1(int y)
    {
        
    }

    private void RenderMode2(int y)
    {
        var blendControl = _memory.Io.REG_BLDCNT;
        var blendAlpha = _memory.Io.REG_BLDALPHA;

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
                if (sprite.Mode == 1) //semi-transparent
                {
                    if ((blendControl & 0xc0) == 0x40) //additive
                    {
                        //temp - get last pixel non-transparent
                        ushort pixel = 0b0000000_11111_11111;
                        var redB = pixel & 0x1f;
                        var greenB = (pixel >> 5) & 0x1f;
                        var blueB = (pixel >> 10) & 0x1f;

                        float coefA = (float)(blendAlpha & 0x1f) / 16;
                        float coefB = (float)((blendAlpha >> 8) & 0x1f) / 16;

                        //blend
                        var redA = objPixelColor & 0x1f;
                        var greenA = (objPixelColor >> 5) & 0x1f;
                        var blueA = (objPixelColor >> 10) & 0x1f;

                        var red = Math.Min(31, (redA * coefA) + (redB * coefB));
                        var green = Math.Min(31, (greenA * coefA) + (greenB * coefB));
                        var blue = Math.Min(31, (blueA * coefA) + (blueB * coefB));
                        objPixelColor = (ushort)((uint)red | ((uint)green << 5) | ((uint)blue << 10));
                    }
                }
                var finalSpritePixelColor = ConvertBgr555ToArgb(objPixelColor);
                FrameBuffer.SetPixel(x, y, finalSpritePixelColor);
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

            var attr1Value = ReadOam16(oam, oamAttrOffset + 2);
            if (attr0.IsRotationScaling)
            {
                var rotateParamGroup = (attr1Value >> 9) & 0x1f;
                var doubleSized = attr0.IsDisabled;
                //continue; //temp
                //do affine
            }

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
                attr2.Priority, xTiles, attr1.XCoord, attr0.ObjMode);
            //add to list for display reg sprites
            sprites[count++] = regSpriteInfo;

            //affine only affine
            //ushort attr3 = ReadOam16(oam, oamAttrOffset + 6);
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ReadOam16(ReadOnlySpan<byte> oam, int offset)
    {
        return (ushort)((oam[offset + 1] << 8) | oam[offset]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ReadVram16(ReadOnlySpan<byte> vram, int offset)
    {
        return (ushort)((vram[offset + 1] << 8) | vram[offset]);
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

    private ushort ReadObjPaletteColor(int paletteIndex)
    {
        var offset = paletteIndex * 2;
        var bgr555 = ReadPalette16(offset + 0x200);
        return bgr555;
        //return ConvertBgr555ToArgb(bgr555);
    }

    private static uint ConvertBgr555ToArgb(ushort value)
    {
        var red = (byte)((value & 0x1F) * 255 / 31);
        var green = (byte)(((value >> 5) & 0x1F) * 255 / 31);
        var blue = (byte)(((value >> 10) & 0x1F) * 255 / 31);
        return 0xFF000000U | ((uint)red << 16) | ((uint)green << 8) | blue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FastSortBackgroundsByPriority(uint displayControl, ReadOnlySpan<ushort> bgControlRegs,
        Span<int> bgIndexOutput)
    {
        int count = 0;
        int p0 = 0xff, p1 = 0xff, p2 = 0xff, p3 = 0xff;

        uint enabledMask = (displayControl >> 8) & 0xf;
        if ((enabledMask & 1) != 0) p0 = ((bgControlRegs[0] & 0b11) << 2);
        if ((enabledMask & 2) != 0) p1 = ((bgControlRegs[1] & 0b11) << 2) | 1;
        if ((enabledMask & 4) != 0) p2 = ((bgControlRegs[2] & 0b11) << 2) | 2;
        if ((enabledMask & 8) != 0) p3 = ((bgControlRegs[3] & 0b11) << 2) | 3;

        if (p0 > p1) { (p0, p1) = (p1, p0); }
        if (p2 > p3) { (p2, p3) = (p3, p2); }
        if (p0 > p2) { (p0, p2) = (p2, p0); }
        if (p1 > p3) { (p1, p3) = (p3, p1); }
        if (p1 > p2) { (p1, p2) = (p2, p1); }

        int idx = 0;
        if (p0 != 0xff) { bgIndexOutput[idx++] = p0 & 0b11; count++; }
        if (p1 != 0xff) { bgIndexOutput[idx++] = p1 & 0b11; count++; }
        if (p2 != 0xff) { bgIndexOutput[idx++] = p2 & 0b11; count++; }

        if (p3 == 0xff)
        {
            return count;
        }

        bgIndexOutput[idx] = p3 & 0b11; count++;
        return count;
    }
}