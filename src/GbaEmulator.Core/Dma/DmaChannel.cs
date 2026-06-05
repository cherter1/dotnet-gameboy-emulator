namespace GbaEmulator.Core.Dma;

internal sealed class DmaChannel
{
    /// <summary>
    /// REG_DMA0SAD: 0x040000B0 27bits
    /// REG_DMA1SAD: 0x040000BC 28bits
    /// REG_DMA2SAD: 0x040000C8 28bits
    /// REG_DMA3SAD: 0x040000D4 28bits
    /// </summary>
    public uint SourceAddress { get; set; }
    /// <summary>
    /// REG_DMA0DAD: 0x040000B4 27bits
    /// REG_DMA1DAD: 0x040000C0 27bits
    /// REG_DMA2DAD: 0x040000CC 27bits
    /// REG_DMA3DAD: 0x040000D8 28bits
    /// </summary>
    public uint DestinationAddress { get; set; }
    /// <summary>
    /// REG_DMA0CNT_L: 0x040000B8
    /// REG_DMA1CNT_L: 0x040000C4
    /// REG_DMA2CNT_L: 0x040000D0
    /// REG_DMA3CNT_L: 0x040000DC
    /// </summary>
    public ushort Count { get; set; }
    /// <summary>
    /// REG_DMA0CNT_H: 0x040000BA
    /// REG_DMA1CNT_H: 0x040000C6
    /// REG_DMA2CNT_H: 0x040000D2
    /// REG_DMA3CNT_H: 0x040000DE
    /// </summary>
    public ushort Control { get; set; }
}