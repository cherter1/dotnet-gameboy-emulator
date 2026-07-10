namespace GbaEmulator.Core.Memory;

public sealed class IoRegisters
{
    public uint ReadIo32Aligned(uint address)
    {
        return 0;
    }

    public uint ReadIo16Aligned(uint address)
    {
        return 0;
    }

    public ushort GetMappedRegister(uint address) =>
        address switch
        {
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

    #endregion
}