using System.Runtime.CompilerServices;
using GbaEmulator.Core.Common;
using GbaEmulator.Core.Dma;
using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Video.Backgrounds;
using GbaEmulator.Core.Video.SpecialEffects;
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
                Console.WriteLine("RENDER MODE 5");
                break;
            default:
                //Console.WriteLine("BACKDROP RENDER");
                //should only be used when forceBlank bit is set in DisplayControl register
                var backDropColor = ReadBgPaletteColor(0); //backdrop color is set by zero index of palette
                FrameBuffer.FillScanline(scanLine, backDropColor); // 0xffffff00); //tmep yellow color for testing
                break;
        }
    }

    private void RenderMode0(int y)
    {
        ReadOnlySpan<byte> vram = _memory.Vram.AsSpan();
        var displayControl = _memory.Io.REG_DISPCNT;

        var win0Enabled = BitUtils.IsBitSet(displayControl, 13);
        var win1Enabled = BitUtils.IsBitSet(displayControl, 14);
        var objWinEnabled = BitUtils.IsBitSet(displayControl, 15);
        if (win0Enabled)
        {
            var x2 = (_memory.Io.REG_WIN0H & 0xff) + 1; //bits 0-7 plus 1 x2 rightMost
            var x1 = _memory.Io.REG_WIN0H >> 8; //bits 8-15 x1 leftMost
            if (x2 > 240 || x1 > x2)
            {
                x2 = 240;
            }

            var y2 = (_memory.Io.REG_WIN0V & 0xff) + 1; //bits 0-7 plus 1 y2 bottomMost
            var y1 = _memory.Io.REG_WIN0V >> 8; //bits 8-15 y1 topMost
            if (y2 > 160 || y1 > y2)
            {
                y2 = 160;
            }
        }

        if (win1Enabled)
        {
            var x2 = (_memory.Io.REG_WIN0H & 0xff) + 1; //bits 0-7 plus 1 x2 rightMost
            var x1 = _memory.Io.REG_WIN0H >> 8; //bits 8-15 x1 leftMost
            if (x2 > 240 || x1 > x2)
            {
                x2 = 240;
            }

            var y2 = (_memory.Io.REG_WIN0V & 0xff) + 1; //bits 0-7 plus 1 y2 bottomMost
            var y1 = _memory.Io.REG_WIN0V >> 8; //bits 8-15 y1 topMost
            if (y2 > 160 || y1 > y2)
            {
                y2 = 160;
            }
        }

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

        Span<int> bgScanlineInfo = stackalloc int[7 * (activeBgs.Length + 1)];
        for (int i = 0; i < activeBgs.Length; i++)
        {
            var bgIdx = activeBgs[i];
            var tileDataStartOffset = ((bgControls[bgIdx] >> 2) & 0b11) * 0x4000; // + 0x0600000 for address
            var tileMapStartOffset = ((bgControls[bgIdx] >> 8) & 0x1f) * 0x800; // + 0x0600000 for address
            var tileMapSize = (bgControls[bgIdx] >> 14) & 0b11;
            BackgroundHelpers.GetTextBackgroundSizeTiles(tileMapSize, out var xTiles, out var yTiles);

            var bgYStartOffset = y + vofsTable[bgIdx];
            if (yTiles > 32 && ((bgYStartOffset >> 8) & 1) != 0) // startOffset greater than pixels per map or SE length across AND yTiles > 32, if its 32 mirror the single Y SE
            {
                tileMapStartOffset += xTiles > 32 ? 0x1000 : 0x800; //move startOffset to next SE(map) start offset, if xTiles long then add 2 maps if not then only add 1 map length
            }
            bgYStartOffset &= 0xff;

            var tileY = bgYStartOffset >> 3; // div 8 to count tiles from offset
            var pixelYInTile = bgYStartOffset & 7; // modulo 8 for pixel 0-7 on x axis

            bgScanlineInfo[i * 7] = tileDataStartOffset;
            bgScanlineInfo[(i * 7) + 1] = tileMapStartOffset;
            bgScanlineInfo[(i * 7) + 2] = xTiles;
            bgScanlineInfo[(i * 7) + 3] = yTiles; //remove later
            bgScanlineInfo[(i * 7) + 4] = tileY;
            bgScanlineInfo[(i * 7) + 5] = pixelYInTile;
            bgScanlineInfo[(i * 7) + 6] = (bgControls[bgIdx] >> 7) & 0b1; // set is 8bpp mode else 4bpp
        }

        Span<ScanlineSpriteInfo> sprites = stackalloc ScanlineSpriteInfo[128];
        var spriteCount = 0;
        if ((displayControl & 0x1000) == 0x1000) //bit 12 set means sprites enabled
        {
            spriteCount = SpriteStuff_TempName(y, sprites);
        }

        Span<ScanlineSpriteInfo> enabledSprites = stackalloc ScanlineSpriteInfo[spriteCount];
        SortSpriteIndicesByPriority(sprites[..spriteCount], enabledSprites);

        for (var x = 0; x < ScreenWidth; x++)
        {
            ushort topPixelBgrColor = 0x8000;
            BlendTargetOneType topColorSource = BlendTargetOneType.Backdrop;
            ushort nextTopPixelBgrColor = 0x8000;
            BlendTargetTwoType nextTopColorSource = BlendTargetTwoType.Backdrop;

            int topPriorityLine = 4; //top pixel priority line
            int priorityLine = 4; //priority Of the next TopPixel default 4 so by default anything has higher priority
            for (int i = 0; i < activeBgs.Length; i++)
            {
                var bgIdx = activeBgs[i];
                var paletteIndex = RenderTiledTextBackground(ref vram, x, hofsTable[bgIdx],
                    tileMapStartOffset: bgScanlineInfo[(i * 7) + 1],
                    xTiles: bgScanlineInfo[(i * 7) + 2],
                    tileY: bgScanlineInfo[(i * 7) + 4],
                    pixelYInTile: bgScanlineInfo[(i * 7) + 5],
                    isSinglePalette: bgScanlineInfo[(i * 7) + 6] == 1,
                    tileDataStartOffset: bgScanlineInfo[i * 7]);

                if ((paletteIndex & 0xf) == 0) //mod 16 cuz if zero index of any palette color is transparent
                {
                    continue;
                }

                var bgrColor = ReadPalette16(paletteIndex * 2); // paletteInd * 2 bc each paletteEntry is 2bytes

                if (win0Enabled)
                {
                    //AND in window region
                    var win0BgDisplay = (_memory.Io.REG_WININ & (1 << bgIdx)) == (1 << bgIdx);
                }
                if (topPixelBgrColor == 0x8000)
                {
                    topPixelBgrColor = bgrColor;
                    topColorSource = (BlendTargetOneType)(1 << bgIdx);
                    topPriorityLine = bgControls[bgIdx] & 0b11;
                    continue;
                }

                nextTopPixelBgrColor = bgrColor;
                nextTopColorSource = (BlendTargetTwoType)(1 << (bgIdx + 8));
                priorityLine = bgControls[bgIdx] & 0b11;
                break;
            }

            int spriteMode = 0;
            foreach (var sprite in enabledSprites)
            {
                int objPaletteIndex = sprite.IsRotational
                    ? RenderAffineSprite(ref vram, x, priorityLine, displayControl, sprite)
                    : RenderRegularSprite(ref vram, x, priorityLine, sprite);

                if ((objPaletteIndex & 0xf) == 0) //mod 16 cuz if zero index of any palette color is transparent
                {
                    continue;
                }

                var objPixelColor = ReadObjPaletteColor(objPaletteIndex + (16 * (sprite.IsSinglePalette ? 0 : sprite.PaletteNumber)));
                if (sprite.Priority <= topPriorityLine) //if sprite has higher priority than current top pixel
                {
                    //make sprite pixel top and next top gets set to previous top
                    var tempCol = topPixelBgrColor;
                    var tempSource = (uint)topColorSource;

                    topPixelBgrColor = objPixelColor;
                    topColorSource = BlendTargetOneType.Obj;
                    spriteMode = sprite.Mode;

                    nextTopPixelBgrColor = tempCol;
                    nextTopColorSource = (BlendTargetTwoType)(tempSource << 8);
                    break;
                }

                nextTopPixelBgrColor = objPixelColor;
                nextTopColorSource = BlendTargetTwoType.Obj;
                break;
            }

            if (nextTopPixelBgrColor == 0x8000)
            {
                nextTopPixelBgrColor = ReadPalette16(0);
                if (topPixelBgrColor == 0x8000)
                {
                    topPixelBgrColor = ReadPalette16(0);
                }
            }

            if (topColorSource == BlendTargetOneType.Obj && spriteMode == 1)
            {
                //will always use alpha blending with this as source regardless of BLDCNT
                var t2BlendingEnabled = (_memory.Io.REG_BLDCNT & (ushort)nextTopColorSource) == (ushort)nextTopColorSource;
                if (t2BlendingEnabled)
                {
                    topPixelBgrColor = SpecialEffectsHelper.AlphaBlendPixels(topPixelBgrColor, nextTopPixelBgrColor, _memory.Io.REG_BLDALPHA);
                }
                else
                {
                    if (((_memory.Io.REG_BLDCNT >> 6) & 0b11) != 0b00) //blend control bits 6-7 not zero then apply blending
                    {
                        topPixelBgrColor = ApplyBlendingEffects(topPixelBgrColor, topColorSource, nextTopPixelBgrColor, nextTopColorSource);
                    }
                }
            }
            else if (((_memory.Io.REG_BLDCNT >> 6) & 0b11) != 0b00) //blend control bits 6-7 not zero then apply blending
            {
                topPixelBgrColor = ApplyBlendingEffects(topPixelBgrColor, topColorSource, nextTopPixelBgrColor, nextTopColorSource);
            }

            var finalColor = ConvertBgr555ToArgb(topPixelBgrColor);
            FrameBuffer.SetPixel(x, y, finalColor);
        }
    }

    private static int RenderAffineSprite(ref ReadOnlySpan<byte> vram, int x, int priorityLine, ushort displayControl, ScanlineSpriteInfo sprite)
    {
        int xCoord = sprite.XCoord;
        if (xCoord >= 256)
        {
            xCoord -= 512; //X wrapping, Sign extend the 9-bit of x pos
        }
        var spriteXPos = x - xCoord;

        var canvasWidth = sprite.HFlip ? (sprite.NumXTiles * 8) * 2 : sprite.NumXTiles * 8; //for now hFlip is DoubleSize flag for rotationalSprites
        if ((uint)spriteXPos >= (uint)canvasWidth || sprite.Priority > priorityLine)
        {
            return 0; //sprite not in x range or lower priority than nextTopPixel
        }

        int relativeX = spriteXPos - canvasWidth / 2;
        int relativeY = sprite.YPixelOffset; //for now YPixelOffset is relativeY for rotational sprites

        int sourceX = ((sprite.Pa * relativeX + sprite.Pb * relativeY) >> 8) + (sprite.NumXTiles * 8) / 2;
        int sourceY = ((sprite.Pc * relativeX + sprite.Pd * relativeY) >> 8) + (sprite.YTiles * 8) / 2;

        if ((uint)sourceX >= (uint)(sprite.NumXTiles * 8) || (uint)sourceY >= (uint)(sprite.YTiles * 8))
        {
            return 0; //transformed coordinate is outsize the sprite graphics
        }

        var sourceXTileNumber = sourceX >> 3; //div 8
        var sourceXPixelNumber = sourceX & 7; //mod 8
        var sourceYTileNumber = sourceY >> 3; //div 8
        var sourceYPixelNumber = sourceY & 7; //mod 8

        var yPixelOffset = sourceYPixelNumber * 4; //mul 4(4bppMode) for pixel inside tile offset
        var startTileNumber = sprite.ScanlineStartMapTileNumber; //for now ScanlineStartMapTileNumber is attr2 tileNumber for rotationalSprites
        var twoDMatrixSize = 32;

        if (sprite.IsSinglePalette)
        {
            startTileNumber /= 2;
            yPixelOffset *= 2; //mul 2 (8 total bc already mul 4 above) in 8bpp mode because each byte is a pixel
            twoDMatrixSize = 16;
        }

        int scanlineStartMapTileNumber;
        if ((displayControl & 0x40) == 0x40) //bit 6 set then 1d char mapping
        {
            scanlineStartMapTileNumber = startTileNumber + (sourceYTileNumber * sprite.NumXTiles);
        }
        else //bit 6 clear 2d character mapping
        {
            scanlineStartMapTileNumber = startTileNumber + (sourceYTileNumber * twoDMatrixSize);
        }

        var sourceMapTileNumber = scanlineStartMapTileNumber + sourceXTileNumber;
        var sourceTileOffset = 0x10000 + (sourceMapTileNumber * (sprite.IsSinglePalette ? 0x40 : 0x20));
        var sourcePixOffset = sourceTileOffset + yPixelOffset + (sourceXPixelNumber >> (sprite.IsSinglePalette ? 0 : 1)); //divide x by 2 if 4bpp
        var sourceObjPaletteIndex = sprite.IsSinglePalette
            ? vram[sourcePixOffset]
            : (sourceXPixelNumber & 1) == 0
                ? vram[sourcePixOffset] & 0xf
                : vram[sourcePixOffset] >> 4;
        return sourceObjPaletteIndex;
    }

    private static int RenderRegularSprite(ref ReadOnlySpan<byte> vram, int x, int priorityLine, ScanlineSpriteInfo sprite)
    {
        int xCoord = sprite.XCoord;
        if (xCoord >= 256)
        {
            xCoord -= 512; //X wrapping, Sign extend the 9-bit of x pos
        }
        var spriteXPos = x - xCoord;

        bool objXRange = (uint)spriteXPos < (uint)(sprite.NumXTiles * 8);
        if (!objXRange || sprite.Priority > priorityLine)
        {
            return 0;
        }

        var currentXTileNumber = spriteXPos >> 3; //divide 8
        var currentXPixelNumber = spriteXPos & 7; // mod 8
        if (sprite.HFlip)
        {
            currentXTileNumber = (sprite.NumXTiles - 1) - currentXTileNumber;
            currentXPixelNumber = 7 - currentXPixelNumber;
        }
        var currentMapTileNumber = sprite.ScanlineStartMapTileNumber + currentXTileNumber;
        var currentTileOffset = 0x10000 + (currentMapTileNumber * (sprite.IsSinglePalette ? 0x40 : 0x20));
        var currentPixOffset = currentTileOffset + sprite.YPixelOffset + (currentXPixelNumber >> (sprite.IsSinglePalette ? 0 : 1)); //divide x by 2 if 4bpp
        var objPaletteIndex = sprite.IsSinglePalette
            ? vram[currentPixOffset]
            : (currentXPixelNumber & 1) == 0
                ? vram[currentPixOffset] & 0xf
                : vram[currentPixOffset] >> 4;

        return objPaletteIndex;
    }

    private static int RenderTiledTextBackground(ref ReadOnlySpan<byte> vram, int x, ushort hofs, int tileMapStartOffset,
        int xTiles, int tileY, int pixelYInTile, bool isSinglePalette, int tileDataStartOffset)
    {
        var bgXStartOffset = x + hofs;
        if (xTiles > 32 && ((bgXStartOffset >> 8) & 1) != 0) // startOffset greater than pixels per map or SE length across AND xTiles > 32, if its 32 mirror the single X SE
        {
            tileMapStartOffset += 0x800; //move startOffset to next SE(map) start offset
        }
        bgXStartOffset &= 0xff;

        var tileX = bgXStartOffset >> 3; // div 8 to count tiles from offset
        var pixelXInTile = bgXStartOffset & 7; // modulo 8 for pixel 0-7 on x axis

        var tileMapIndex = tileY * 32 + tileX; //tileY * 32 + tileX
        var tileMapEntryOffset = tileMapStartOffset + tileMapIndex * 2; //tileMapStartOffset + mapIndex * mapEntrySize
        var tileMapEntry = ReadVram16(vram, tileMapEntryOffset);

        var hFlip = (tileMapEntry & 0x0400) != 0;
        var vFlip = (tileMapEntry & 0x0800) != 0;
        if (hFlip) pixelXInTile = 7 - pixelXInTile;
        if (vFlip) pixelYInTile = 7 - pixelYInTile;

        int paletteIndex;
        if (isSinglePalette) //8bpp mode
        {
            var tileIndex = tileMapEntry & 0x03ff;
            var currentTileOffset = tileDataStartOffset + tileIndex * 0x40; //tileDataStartOffset + (tileIndex * sizeof Tile(bytes))
            var currentPixelOffset = currentTileOffset + (pixelYInTile * 8) + pixelXInTile;
            paletteIndex = vram[currentPixelOffset];
        }
        else //4bpp mode
        {
            var tileIndex = tileMapEntry & 0x03ff;
            var currentTileOffset = tileDataStartOffset + tileIndex * 0x20; //tileDataStartOffset + (tileIndex * sizeof Tile(bytes))
            var currentPixelOffset = currentTileOffset + (pixelYInTile * 4) + (pixelXInTile >> 1); //YPixel * 4 bc 2 pixels per byte in 8pixel row XPixel /2 for same reason
            paletteIndex = (pixelXInTile & 1) == 0 // mod by 2 if even Index take bits 0-3 else take bits 4-7
                ? vram[currentPixelOffset] & 0xf
                : vram[currentPixelOffset] >> 4;
            paletteIndex += (tileMapEntry >> 12) * 16; // add palette bank start offset
        }

        return paletteIndex;
    }

    private ushort ApplyBlendingEffects(ushort t1PixelBgr555Color, BlendTargetOneType t1ControlBit, ushort t2PixelBgr555Color, BlendTargetTwoType t2ControlBit)
    {
        var blendControl = _memory.Io.REG_BLDCNT;
        var blendMode = (blendControl >> 6) & 0b11; //bits 6-7 blend mode

        var t1BlendingEnabled = (blendControl & (ushort)t1ControlBit) == (ushort)t1ControlBit;
        if (!t1BlendingEnabled)
        {
            //top is not enabled as target1 don't blend
            return t1PixelBgr555Color;
        }

        ushort finalColor = 1;
        switch (blendMode)
        {
            case 0b01: //alpha blending
                var t2BlendingEnabled = (blendControl & (ushort)t2ControlBit) == (ushort)t2ControlBit;
                if (!t2BlendingEnabled)
                {
                    //nextTop not enabled as target2 don't blend
                    return t1PixelBgr555Color;
                }
                finalColor = SpecialEffectsHelper.AlphaBlendPixels(t1PixelBgr555Color, t2PixelBgr555Color, _memory.Io.REG_BLDALPHA);
                break;
            case 0b10: //brightness increase
                finalColor = SpecialEffectsHelper.LightenBlend(t1PixelBgr555Color, _memory.Io.REG_BLDY);
                break;
            case 0b11: //brightness decrease
                finalColor = SpecialEffectsHelper.DarkenBlend(t1PixelBgr555Color, _memory.Io.REG_BLDY);
                break;
        }

        return finalColor;
    }

    public void RenderMode1(int y)
    {
        Console.WriteLine("RENDER MODE 1");
    }

    private int _internalBg2X;
    private int _internalBg2Y;
    private int _internalBg3X;
    private int _internalBg3Y;

    private void RenderMode2(int y)
    {
        if (y == 0)
        {
            _internalBg2X = BitUtils.SignExtend((int)_memory.Io.REG_BG2X, 28);
            _internalBg2Y = BitUtils.SignExtend((int)_memory.Io.REG_BG2Y, 28);
            _internalBg3X = BitUtils.SignExtend((int)_memory.Io.REG_BG3X, 28);
            _internalBg3Y = BitUtils.SignExtend((int)_memory.Io.REG_BG3Y, 28);
        }

        ReadOnlySpan<byte> vram = _memory.Vram.AsSpan();

        var displayControl = _memory.Io.REG_DISPCNT;

        var bg2Enabled = BitUtils.IsBitSet(displayControl, 10);
        var bg3Enabled = BitUtils.IsBitSet(displayControl, 11);

        var bg2Control = _memory.Io.REG_BG2CNT;
        var bg3Control = _memory.Io.REG_BG3CNT;

        ReadOnlySpan<ushort> bgControls = [bg2Control, bg3Control];
        Span<int> fixedSourceXTable = [_internalBg2X, _internalBg3X];
        Span<int> fixedSourceYTable = [_internalBg2Y, _internalBg3Y];
        ReadOnlySpan<ushort> bgPaTable = [_memory.Io.REG_BG2PA, _memory.Io.REG_BG3PA];
        ReadOnlySpan<ushort> bgPcTable = [_memory.Io.REG_BG2PC, _memory.Io.REG_BG3PC];
        ReadOnlySpan<int> bgSizeTable =
        [
            BackgroundHelpers.GetRotationalBackgroundSizePixels((bg2Control >> 14) & 0b11),
            BackgroundHelpers.GetRotationalBackgroundSizePixels((bg3Control >> 14) & 0b11)
        ];

        Span<int> usedBackgrounds = stackalloc int[2];
        int activeBgCount;
        if (bg2Enabled && bg3Enabled)
        {
            activeBgCount = 2;
            var bg3Priority = bg3Control & 0b11;
            var bg2Priority = bg2Control & 0b11;
            if (bg3Priority < bg2Priority)
            {
                usedBackgrounds[0] = 3;
                usedBackgrounds[1] = 2;
            }
            else
            {
                usedBackgrounds[1] = 3;
                usedBackgrounds[0] = 2;
            }
        }
        else if (bg3Enabled)
        {
            activeBgCount = 1;
            usedBackgrounds[0] = 3;
        }
        else if (bg2Enabled)
        {
            activeBgCount = 1;
            usedBackgrounds[0] = 2;
        }
        else
        {
            activeBgCount = 0;
        }
        var activeBgs = usedBackgrounds[..activeBgCount];

        Span<ScanlineSpriteInfo> sprites = stackalloc ScanlineSpriteInfo[128];
        var spriteCount = 0;
        if ((displayControl & 0x1000) == 0x1000) //bit 12 set means sprites enabled
        {
            spriteCount = SpriteStuff_TempName(y, sprites);
        }

        Span<ScanlineSpriteInfo> enabledSprites = stackalloc ScanlineSpriteInfo[spriteCount];
        SortSpriteIndicesByPriority(sprites[..spriteCount], enabledSprites);

        for (int x = 0; x < ScreenWidth; x++)
        {
            ushort topPixelBgrColor = 0x8000;
            BlendTargetOneType topColorSource = BlendTargetOneType.Backdrop;
            ushort nextTopPixelBgrColor = 0x8000;
            BlendTargetTwoType nextTopColorSource = BlendTargetTwoType.Backdrop;

            int topPriorityLine = 4; //top pixel priority line
            int priorityLine = 4; //priority Of the next TopPixel default 4 so by default anything has higher priority
            foreach (var bgIdx in activeBgs)
            {
                //Mode 2 bgs are always 8bpp aka singlePaletteMode
                var bgControl = bgControls[bgIdx - 2];
                var paletteIndex = RenderAffineTiledBackground(ref vram, ref fixedSourceXTable[bgIdx - 2],
                    ref fixedSourceYTable[bgIdx - 2], bgControl, (short)bgPaTable[bgIdx - 2],
                    (short)bgPcTable[bgIdx - 2], bgSizeTable[bgIdx - 2]);

                if (paletteIndex == 0) //transparent
                {
                    continue;
                }

                var bgrColor = ReadPalette16(paletteIndex * 2); // paletteInd * 2 bc each paletteEntry is 2bytes
                if (topPixelBgrColor == 0x8000)
                {
                    topPixelBgrColor = bgrColor;
                    topColorSource = (BlendTargetOneType)(1 << bgIdx);
                    topPriorityLine = bgControls[bgIdx - 2] & 0b11;
                    continue;
                }

                nextTopPixelBgrColor = bgrColor;
                nextTopColorSource = (BlendTargetTwoType)(1 << (bgIdx + 8));
                priorityLine = bgControls[bgIdx - 2] & 0b11;
                break;
            }

            int spriteMode = 0;
            foreach (var sprite in enabledSprites)
            {
                int objPaletteIndex = sprite.IsRotational
                    ? RenderAffineSprite(ref vram, x, priorityLine, displayControl, sprite)
                    : RenderRegularSprite(ref vram, x, priorityLine, sprite);

                if ((objPaletteIndex & 0xf) == 0) //mod 16 cuz if zero index of any palette color is transparent
                {
                    continue;
                }

                var objPixelColor = ReadObjPaletteColor(objPaletteIndex + (16 * (sprite.IsSinglePalette ? 0 : sprite.PaletteNumber)));
                if (sprite.Priority <= topPriorityLine) //if sprite has higher priority than current top pixel
                {
                    //make sprite pixel top and next top gets set to previous top
                    var tempCol = topPixelBgrColor;
                    var tempSource = (uint)topColorSource;

                    topPixelBgrColor = objPixelColor;
                    topColorSource = BlendTargetOneType.Obj;
                    spriteMode = sprite.Mode;

                    nextTopPixelBgrColor = tempCol;
                    nextTopColorSource = (BlendTargetTwoType)(tempSource << 8);
                    break;
                }

                nextTopPixelBgrColor = objPixelColor;
                nextTopColorSource = BlendTargetTwoType.Obj;
                break;
            }

            if (nextTopPixelBgrColor == 0x8000)
            {
                nextTopPixelBgrColor = ReadPalette16(0);
                if (topPixelBgrColor == 0x8000)
                {
                    topPixelBgrColor = ReadPalette16(0);
                }
            }

            if (topColorSource == BlendTargetOneType.Obj && spriteMode == 1)
            {
                //will always use alpha blending with this as source regardless of BLDCNT
                var t2BlendingEnabled = (_memory.Io.REG_BLDCNT & (ushort)nextTopColorSource) == (ushort)nextTopColorSource;
                if (t2BlendingEnabled)
                {
                    topPixelBgrColor = SpecialEffectsHelper.AlphaBlendPixels(topPixelBgrColor, nextTopPixelBgrColor, _memory.Io.REG_BLDALPHA);
                }
                else
                {
                    if (((_memory.Io.REG_BLDCNT >> 6) & 0b11) != 0b00) //blend control bits 6-7 not zero then apply blending
                    {
                        topPixelBgrColor = ApplyBlendingEffects(topPixelBgrColor, topColorSource, nextTopPixelBgrColor, nextTopColorSource);
                    }
                }
            }
            else if (((_memory.Io.REG_BLDCNT >> 6) & 0b11) != 0b00) //blend control bits 6-7 not zero then apply blending
            {
                topPixelBgrColor = ApplyBlendingEffects(topPixelBgrColor, topColorSource, nextTopPixelBgrColor, nextTopColorSource);
            }

            var finalColor = ConvertBgr555ToArgb(topPixelBgrColor);
            FrameBuffer.SetPixel(x, y, finalColor);
        }

        if (bg2Enabled)
        {
            _internalBg2X += (short)_memory.Io.REG_BG2PB;
            _internalBg2Y += (short)_memory.Io.REG_BG2PD;
        }

        if (!bg3Enabled)
        {
            return;
        }

        _internalBg3X += (short)_memory.Io.REG_BG3PB;
        _internalBg3Y += (short)_memory.Io.REG_BG3PD;

    }

    private static int RenderAffineTiledBackground(ref ReadOnlySpan<byte> vram, ref int sourceXFixed, ref int sourceYFixed, ushort bgControl, short pa, short pc, int backgroundSize)
    {
        //ref readonly var bgControl = ref bgControls[bgIdx - 2];
        //ref var sourceXFixed = ref fixedSourceXTable[bgIdx - 2];
        //ref var sourceYFixed = ref fixedSourceYTable[bgIdx - 2];

        int sourceX = sourceXFixed >> 8; // div 256
        int sourceY = sourceYFixed >> 8; // div 256

        //sourceXFixed += (short)bgPaTable[bgIdx - 2];
        sourceXFixed += pa;
        //sourceYFixed += (short)bgPcTable[bgIdx - 2];
        sourceYFixed += pc;

        var wrapAround = BitUtils.IsBitSet(bgControl, 13);
        //var backgroundSize = bgSizeTable[bgIdx - 2];
        if (wrapAround)
        {
            sourceX &= backgroundSize - 1; //mod by bgSize
            sourceY &= backgroundSize - 1; //mod by bgSize
        }
        else if ((uint)sourceX >= (uint)backgroundSize ||
                 (uint)sourceY >= (uint)backgroundSize)
        {
            //continue;
            return 0;
        }
        // calc pixel color with sourceX and sourceY
        var charBaseBlock = (bgControl >> 2) & 0b11; //bgXcnt bits 2-3
        var charDataStartOffset = charBaseBlock * 0x4000;
        var screenBaseBlock = (bgControl >> 8) & 0x1F; //bgXcnt bits 8-12
        var tileMapStartOffset = screenBaseBlock * 0x800;

        //tileMapIndex = tileY * tilesPerRow + xTile
        var tileMapIndex = ((sourceY >> 3) * (backgroundSize >> 3)) + (sourceX >> 3);
        var tileNumber = vram[tileMapStartOffset + tileMapIndex];

        var currentTileStartOffset = charDataStartOffset + tileNumber * 0x40; //tile size 0x40 bc mode2 is forced 8bpp for bgs
        var yPixel = sourceY & 7; //mod 8
        var xPixel = sourceX & 7; //mod 8

        var paletteIndex = vram[currentTileStartOffset + yPixel * 8 + xPixel];
        return paletteIndex;
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
                var paStartOffset = 0x6 + (rotateParamGroup * 0x20);
                var pA = (short)ReadOam16(oam, paStartOffset);
                var pB = (short)ReadOam16(oam, paStartOffset + 8);
                var pC = (short)ReadOam16(oam, paStartOffset + 16);
                var pD = (short)ReadOam16(oam, paStartOffset + 24);
                var doubleSized = attr0.IsDisabled;
                var affattr1 = new ObjAttribute1(attr1Value);

                var affshapeSize = (attr0.ObjShape << 2) | affattr1.ObjSize; //low two bits attr1 size
                SpriteHelpers.GetSpriteSizeTiles(affshapeSize, out int affxTiles, out int affyTiles);
                //int spriteCanvasWidth = affxTiles * 8;
                int spriteCanvasHeight = affyTiles * 8;
                if (doubleSized)
                {
                    //spriteCanvasWidth *= 2;
                    spriteCanvasHeight *= 2;
                }

                int sY = attr0.YCoord;
                if (sY + spriteCanvasHeight > 256)
                {
                    sY -= 256; //Y wrapping
                }

                var canvasY = y - sY;
                if ((uint)canvasY >= (uint)spriteCanvasHeight) //not in Y range
                {
                    continue;
                }

                int relativeY = canvasY - spriteCanvasHeight / 2;
                var affattr2Value = ReadOam16(oam, oamAttrOffset + 4);
                var affattr2 = new ObjAttribute2(affattr2Value);
                var rotationalSprite = new ScanlineSpriteInfo(affattr2.TileNumber, isSinglePalette, affattr2.PaletteNumber, relativeY,
                    affattr2.Priority, affxTiles, affattr1.XCoord, attr0.ObjMode, doubleSized, true, pA, pB, pC, pD, affyTiles);
                sprites[count++] = rotationalSprite;
                continue;
            }

            var attr1 = new ObjAttribute1(attr1Value);

            var shapeSize = (attr0.ObjShape << 2) | attr1.ObjSize; //low two bits attr1 size
            SpriteHelpers.GetSpriteSizeTiles(shapeSize, out int xTiles, out int yTiles);

            //only * 8 for 8bpp mode in 4bpp mode its * 4 since each byte represents 2 pix of a tile
            var spriteY = attr0.YCoord;
            var canvasHeight = yTiles * 8;
            if (spriteY + canvasHeight > 256)
            {
                spriteY -= 256; //Y wrap around screen
            }

            int spriteYPos = y - spriteY;
            bool objYRange = (uint)spriteYPos < (uint)canvasHeight;

            if (!objYRange)
            {
                continue;
            }

            var attr2Value = ReadOam16(oam, oamAttrOffset + 4);
            var attr2 = new ObjAttribute2(attr2Value);

            var currentYTile = spriteYPos >> 3; //div 8
            var currentYPixel = spriteYPos & 7; //mod 8
            if (attr1.VerticalMirrored)
            {
                currentYTile = yTiles - currentYTile;
                currentYPixel = 7 - currentYPixel;
            }

            var yPixelOffset = currentYPixel * 4; //mod 8 and mul 4(4bppMode) for pixel inside tile offset
            var startTileNumber = attr2.TileNumber;
            var twoDMatrixSize = 32;

            if (isSinglePalette)
            {
                startTileNumber /= 2;
                yPixelOffset *= 2; //mul 2 (8 total bc already mul 4 above) in 8bpp mode because each byte is a pixel
                twoDMatrixSize = 16;
            }

            int scanlineStartMapTileNumber;
            if ((_memory.Io.REG_DISPCNT & 0x40) == 0x40) //bit 6 set then 1d char mapping
            {

                scanlineStartMapTileNumber = startTileNumber + (currentYTile * xTiles);
            }
            else //bit 6 clear 2d character mapping
            {
                scanlineStartMapTileNumber = startTileNumber + (currentYTile * twoDMatrixSize);
            }

            var regSpriteInfo = new ScanlineSpriteInfo(scanlineStartMapTileNumber, isSinglePalette, attr2.PaletteNumber, yPixelOffset,
                attr2.Priority, xTiles, attr1.XCoord, attr0.ObjMode, attr1.HorizontalMirrored, false, 0, 0, 0, 0, 0);
            //add to list for display reg sprites
            sprites[count++] = regSpriteInfo;
        }

        return count + 1;
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
        Console.WriteLine("RENDER MODE 3");
        for (var x = 0; x < ScreenWidth; x++)
        {
            var offset = ((y * ScreenWidth) + x) * 2;

            var bgr555 = ReadVram16(offset);
            FrameBuffer.SetPixel(x, y, ConvertBgr555ToArgb(bgr555));
        }
    }

    private void RenderMode4(int y)
    {
        Console.WriteLine("RENDER MODE 4");
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SortSpriteIndicesByPriority(ReadOnlySpan<ScanlineSpriteInfo> sprites,
        Span<ScanlineSpriteInfo> output)
    {
        Span<int> offsets = stackalloc int[4];

        for (int i = 0; i < sprites.Length; i++)
        {
            offsets[sprites[i].Priority]++;
        }

        int p0Count = offsets[0];
        int p1Count = offsets[1];
        int p2Count = offsets[2];

        offsets[0] = 0;
        offsets[1] = p0Count;
        offsets[2] = p0Count + p1Count;
        offsets[3] = p0Count + p1Count + p2Count;

        foreach (var sprite in sprites)
        {
            ref readonly ScanlineSpriteInfo spriteInfo = ref sprite;
            output[offsets[spriteInfo.Priority]++] = spriteInfo;
        }
    }
}