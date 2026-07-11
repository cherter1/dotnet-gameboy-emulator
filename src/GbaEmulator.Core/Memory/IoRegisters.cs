namespace GbaEmulator.Core.Memory;

public sealed class IoRegisters
{
    public uint ReadIo32Aligned(uint address)
    {
        if ((address & 3u) != 0)
        {
            Console.WriteLine("ReadIo32Aligned called with non word aligned address");
            //throw new InvalidOperationException("ReadIo32Aligned called with non word aligned address");
        }

        var lo = GetMappedRegister(address);
        var hi = GetMappedRegister(address + 2);
        return (uint)((hi << 16) | lo);
    }

    public ushort ReadIo16Aligned(uint address)
    {
        if ((address & 1u) != 0)
        {
            Console.WriteLine("ReadIo16Aligned called with non halfword aligned address");
            //throw new InvalidOperationException("ReadIo16Aligned called with non halfword aligned address");
        }

        return GetMappedRegister(address);
    }

    public byte ReadIo8(uint address)
    {
        var aligned = address & ~1u;
        var registerValue = GetMappedRegister(aligned);
        //LSB if aligned, MSB if unaligned
        return (byte)((registerValue >> ((int)(address & 1) * 8)) & 0xFF);
    }

    public void WriteIo32Aligned(uint address, uint value)
    {
        if ((address & 3u) != 0)
        {
            Console.WriteLine("WriteIo32Aligned called with non word aligned address");
            //throw new InvalidOperationException("WriteIo32Aligned called with non word aligned address");
        }

        WriteIo16Aligned(address, (ushort)value);
        WriteIo16Aligned(address + 2, (ushort)(value >> 16));
    }

    public void WriteIo16Aligned(uint address, ushort value)
    {
        if ((address & 1u) != 0)
        {
            Console.WriteLine("WriteIo16Aligned called with non halfword aligned address");
            //throw new InvalidOperationException("WriteIo16Aligned called with non halfword aligned address");
        }

        switch (address)
        {
            //TODO: watch writeonly
            //Display
            case 0x04000000:
                REG_DISPCNT = value;
                break;
            case 0x04000004:
                REG_DISPSTAT = value;
                break;
            case 0x04000006:
                REG_VCOUNT = value;
                break;
            case 0x04000008:
                REG_BG0CNT = value;
                break;
            case 0x0400000A:
                REG_BG1CNT = value;
                break;
            case 0x0400000C:
                REG_BG2CNT = value;
                break;
            case 0x0400000E:
                REG_BG3CNT = value;
                break;
            case 0x04000010:
                REG_BG0HOFS = value;
                break;
            case 0x04000012:
                REG_BG0VOFS = value;
                break;
            case 0x04000014:
                REG_BG1HOFS = value;
                break;
            case 0x04000016:
                REG_BG1VOFS = value;
                break;
            case 0x04000018:
                REG_BG2HOFS = value;
                break;
            case 0x0400001A:
                REG_BG2VOFS = value;
                break;
            case 0x0400001C:
                REG_BG3HOFS = value;
                break;
            case 0x0400001E:
                REG_BG3VOFS = value;
                break;
            case 0x04000020:
                REG_BG2PA = value;
                break;
            case 0x04000022:
                REG_BG2PB = value;
                break;
            case 0x04000024:
                REG_BG2PC = value;
                break;
            case 0x04000026:
                REG_BG2PD = value;
                break;
            case 0x04000028:
                REG_BG2X = 0; //shiftlater
                break;
            case 0x0400002A:
                REG_BG2X = 0; //shiftlater
                break;
            case 0x0400002C:
                REG_BG2Y = 0; //shiftlater
                break;
            case 0x0400002E:
                REG_BG2Y = 0; //shiftlater
                break;
            case 0x04000030:
                REG_BG3PA = value;
                break;
            case 0x04000032:
                REG_BG3PB = value;
                break;
            case 0x04000034:
                REG_BG3PC = value;
                break;
            case 0x04000036:
                REG_BG3PD = value;
                break;
            case 0x04000038:
                REG_BG3X = 0; //shiftlater
                break;
            case 0x0400003A:
                REG_BG3X = 0; //shiftlater
                break;
            case 0x0400003C:
                REG_BG3Y = 0; //shiftlater
                break;
            case 0x0400003E:
                REG_BG3Y = 0; //shiftlater
                break;
            case 0x04000040:
                REG_WIN0H = value;
                break;
            case 0x04000042:
                REG_WIN1H = value;
                break;
            case 0x04000044:
                REG_WIN0V = value;
                break;
            case 0x04000046:
                REG_WIN1V = value;
                break;
            case 0x04000048:
                REG_WININ = value;
                break;
            case 0x0400004A:
                REG_WINOUT = value;
                break;
            case 0x0400004C:
                REG_MOSAIC = value;
                break;
            case 0x04000050:
                REG_BLDCNT = value;
                break;
            case 0x04000052:
                REG_BLDALPHA = 1;
                break;
            case 0x04000054:
                REG_BLDY = 1;
                break;
            default:
                Console.WriteLine("dum");
                break;
        }
    }

    private ushort GetMappedRegister(uint address) =>
        address switch
        {
            //TODO: watch writeonly
            //Display
            0x04000000 => REG_DISPCNT,
            0x04000004 => REG_DISPSTAT,
            0x04000006 => REG_VCOUNT,
            0x04000008 => REG_BG0CNT,
            0x0400000A => REG_BG1CNT,
            0x0400000C => REG_BG2CNT,
            0x0400000E => REG_BG3CNT,
            0x04000010 => REG_BG0HOFS,
            0x04000012 => REG_BG0VOFS,
            0x04000014 => REG_BG1HOFS,
            0x04000016 => REG_BG1VOFS,
            0x04000018 => REG_BG2HOFS,
            0x0400001A => REG_BG2VOFS,
            0x0400001C => REG_BG3HOFS,
            0x0400001E => REG_BG3VOFS,
            0x04000020 => REG_BG2PA,
            0x04000022 => REG_BG2PB,
            0x04000024 => REG_BG2PC,
            0x04000026 => REG_BG2PD,
            0x04000028 => (ushort)REG_BG2X, //shiftlater
            0x0400002A => (ushort)REG_BG2X, //shiftlater
            0x0400002C => (ushort)REG_BG2Y, //shiftlater
            0x0400002E => (ushort)REG_BG2Y, //shiftlater
            0x04000030 => REG_BG3PA,
            0x04000032 => REG_BG3PB,
            0x04000034 => REG_BG3PC,
            0x04000036 => REG_BG3PD,
            0x04000038 => (ushort)REG_BG3X, //shiftlater
            0x0400003A => (ushort)REG_BG3X, //shiftlater
            0x0400003C => (ushort)REG_BG3Y, //shiftlater
            0x0400003E => (ushort)REG_BG3Y, //shiftlater
            0x04000040 => REG_WIN0H,
            0x04000042 => REG_WIN1H,
            0x04000044 => REG_WIN0V,
            0x04000046 => REG_WIN1V,
            0x04000048 => REG_WININ,
            0x0400004A => REG_WINOUT,
            0x0400004C => REG_MOSAIC,
            0x04000050 => REG_BLDCNT,
            0x04000052 => REG_BLDALPHA,
            0x04000054 => REG_BLDY,
            //Sound
            //Display
            0x040000B0 => (ushort)REG_DMA0SAD, //shiftlater
            0x040000B4 => (ushort)REG_DMA0DAD, //shiftlater
            0x040000B8 => REG_DMA0CNT_L,
            0x040000BA => REG_DMA0CNT_H,
            0x040000BC => (ushort)REG_DMA1SAD, //shiftlater
            0x040000C0 => (ushort)REG_DMA1DAD, //shiftlater
            0x040000C4 => REG_DMA1CNT_L,
            0x040000C6 => REG_DMA1CNT_H,
            0x040000C8 => (ushort)REG_DMA2SAD, //shiftlater
            0x040000CC => (ushort)REG_DMA2DAD, //shiftlater
            0x040000D0 => REG_DMA2CNT_L,
            0x040000D2 => REG_DMA2CNT_H,
            0x040000D4 => (ushort)REG_DMA3SAD, //shiftlater
            0x040000D8 => (ushort)REG_DMA3DAD, //shiftlater
            0x040000DC => REG_DMA3CNT_L,
            0x040000DE => REG_DMA3CNT_H,
            _ => 0 //TODO: add openBus behavior
        };

    #region Display

    /// <summary>
    /// 0x04000000
    /// </summary>
    public ushort REG_DISPCNT { get; set; }
    /// <summary>
    /// 0x04000004
    /// </summary>
    public ushort REG_DISPSTAT { get; set; }
    /// <summary>
    /// 0x04000006
    /// </summary>
    public ushort REG_VCOUNT { get; set; }
    /// <summary>
    /// 0x04000008
    /// </summary>
    public ushort REG_BG0CNT { get; set; }
    /// <summary>
    /// 0x0400000A
    /// </summary>
    public ushort REG_BG1CNT { get; set; }
    /// <summary>
    /// 0x0400000C
    /// </summary>
    public ushort REG_BG2CNT { get; set; }
    /// <summary>
    /// 0x0400000E
    /// </summary>
    public ushort REG_BG3CNT { get; set; }
    /// <summary>
    /// 0x04000010
    /// </summary>
    public ushort REG_BG0HOFS { get; set; }
    /// <summary>
    /// 0x04000012
    /// </summary>
    public ushort REG_BG0VOFS { get; set; }
    /// <summary>
    /// 0x04000014
    /// </summary>
    public ushort REG_BG1HOFS { get; set; }
    /// <summary>
    /// 0x04000016
    /// </summary>
    public ushort REG_BG1VOFS { get; set; }
    /// <summary>
    /// 0x04000018
    /// </summary>
    public ushort REG_BG2HOFS { get; set; }
    /// <summary>
    /// 0x0400001A
    /// </summary>
    public ushort REG_BG2VOFS { get; set; }
    /// <summary>
    /// 0x0400001C
    /// </summary>
    public ushort REG_BG3HOFS { get; set; }
    /// <summary>
    /// 0x0400001E
    /// </summary>
    public ushort REG_BG3VOFS { get; set; }
    /// <summary>
    /// 0x04000020
    /// </summary>
    public ushort REG_BG2PA { get; set; }
    /// <summary>
    /// 0x04000022
    /// </summary>
    public ushort REG_BG2PB { get; set; }
    /// <summary>
    /// 0x04000024
    /// </summary>
    public ushort REG_BG2PC { get; set; }
    /// <summary>
    /// 0x04000026
    /// </summary>
    public ushort REG_BG2PD { get; set; }
    /// <summary>
    /// 0x04000028
    /// </summary>
    public uint REG_BG2X { get; set; }
    /// <summary>
    /// 0x0400002C
    /// </summary>
    public uint REG_BG2Y { get; set; }
    /// <summary>
    /// 0x04000030
    /// </summary>
    public ushort REG_BG3PA { get; set; }
    /// <summary>
    /// 0x04000032
    /// </summary>
    public ushort REG_BG3PB { get; set; }
    /// <summary>
    /// 0x04000034
    /// </summary>
    public ushort REG_BG3PC { get; set; }
    /// <summary>
    /// 0x04000036
    /// </summary>
    public ushort REG_BG3PD { get; set; }
    /// <summary>
    /// 0x04000038
    /// </summary>
    public uint REG_BG3X { get; set; }
    /// <summary>
    /// 0x0400003C
    /// </summary>
    public uint REG_BG3Y { get; set; }
    /// <summary>
    /// 0x04000040
    /// </summary>
    public ushort REG_WIN0H { get; set; }
    /// <summary>
    /// 0x04000042
    /// </summary>
    public ushort REG_WIN1H { get; set; }
    /// <summary>
    /// 0x04000044
    /// </summary>
    public ushort REG_WIN0V { get; set; }
    /// <summary>
    /// 0x04000046
    /// </summary>
    public ushort REG_WIN1V { get; set; }
    /// <summary>
    /// 0x04000048
    /// </summary>
    public ushort REG_WININ { get; set; }
    /// <summary>
    /// 0x0400004A
    /// </summary>
    public ushort REG_WINOUT { get; set; }
    /// <summary>
    /// 0x0400004C
    /// </summary>
    public ushort REG_MOSAIC { get; set; }
    /// <summary>
    /// 0x04000050
    /// </summary>
    public ushort REG_BLDCNT { get; set; }
    /// <summary>
    /// 0x04000052
    /// </summary>
    public ushort REG_BLDALPHA { get; set; }
    /// <summary>
    /// 0x04000054
    /// </summary>
    public ushort REG_BLDY { get; set; }

    #endregion

    #region Sound
    #endregion

    #region Dma

    /// <summary>
    /// 0x040000B0
    /// </summary>
    public uint REG_DMA0SAD { get; set; }
    /// <summary>
    /// 0x040000B4
    /// </summary>
    public uint REG_DMA0DAD { get; set; }
    /// <summary>
    /// 0x040000B8
    /// </summary>
    public ushort REG_DMA0CNT_L { get; set; }
    /// <summary>
    /// 0x040000BA
    /// </summary>
    public ushort REG_DMA0CNT_H { get; set; }
    /// <summary>
    /// 0x040000BC
    /// </summary>
    public uint REG_DMA1SAD { get; set; }
    /// <summary>
    /// 0x040000C0
    /// </summary>
    public uint REG_DMA1DAD { get; set; }
    /// <summary>
    /// 0x040000C4
    /// </summary>
    public ushort REG_DMA1CNT_L { get; set; }
    /// <summary>
    /// 0x040000C6
    /// </summary>
    public ushort REG_DMA1CNT_H { get; set; }
    /// <summary>
    /// 0x040000C8
    /// </summary>
    public uint REG_DMA2SAD { get; set; }
    /// <summary>
    /// 0x040000CC
    /// </summary>
    public uint REG_DMA2DAD { get; set; }
    /// <summary>
    /// 0x040000D0
    /// </summary>
    public ushort REG_DMA2CNT_L { get; set; }
    /// <summary>
    /// 0x040000D2
    /// </summary>
    public ushort REG_DMA2CNT_H { get; set; }
    /// <summary>
    /// 0x040000D4
    /// </summary>
    public uint REG_DMA3SAD { get; set; }
    /// <summary>
    /// 0x040000D8
    /// </summary>
    public uint REG_DMA3DAD { get; set; }
    /// <summary>
    /// 0x040000DC
    /// </summary>
    public ushort REG_DMA3CNT_L { get; set; }
    /// <summary>
    /// 0x040000DE
    /// </summary>
    public ushort REG_DMA3CNT_H { get; set; }
    #endregion

    #region Timers
    #endregion
}