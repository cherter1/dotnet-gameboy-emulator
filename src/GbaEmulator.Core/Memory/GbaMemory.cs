namespace GbaEmulator.Core.Memory;

public sealed class GbaMemory
{
    private byte[] _bios = new byte[0x4000]; //16KB
    private readonly byte[] _ewram = new byte[0x40000]; //256KB
    private readonly byte[] _iwram = new byte[0x8000]; //32KB
    private readonly byte[] _paletteRam = new byte[0x400]; //1KB
    private readonly byte[] _vram = new byte[0x18000]; //96KB
    private readonly byte[] _oam = new byte[0x400]; //1KB
    private byte[] _rom = [];
    private readonly byte[] _sram = new byte[0x10000]; //64KB
}