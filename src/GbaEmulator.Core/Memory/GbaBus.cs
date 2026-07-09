using GbaEmulator.Core.Bios;
using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Dma;
using GbaEmulator.Core.Input;
using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Timers;
using GbaEmulator.Core.Video;
using GbaCartridge = GbaEmulator.Core.Cartridge.Cartridge;

namespace GbaEmulator.Core.Memory;

public sealed class GbaBus
{
    private readonly GbaMemory _memory;
    //public RegisterBank Registers { get; set; }
    
    //private readonly InterruptController _interrupts;
    //private readonly TimerController _timers;
    //private readonly DmaController _dma;
    //private readonly Ppu _ppu;
    //private readonly KeypadState _keypad;

    //private byte[] _bios = new byte[0x4000]; //16KB
    //private readonly byte[] _ewram = new byte[0x40000]; //256KB
    //private readonly byte[] _iwram = new byte[0x8000]; //32KB
    //private readonly byte[] _paletteRam = new byte[0x400]; //1KB
    //private readonly byte[] _vram = new byte[0x18000]; //96KB
    //private readonly byte[] _oam = new byte[0x400]; //1KB
    //private byte[] _rom = [];
    //private readonly byte[] _sram = new byte[0x10000]; //64KB

    public GbaBus(
        InterruptController interrupts,
        TimerController timers,
        DmaController dma,
        Ppu ppu,
        KeypadState keypad,
        GbaMemory memory)
    {
        //_interrupts = interrupts;
        //_timers = timers;
        //_dma = dma;
        //_ppu = ppu;
        //_keypad = keypad;
        //_ppu.ConnectMemory(_vram, _paletteRam);
        _memory = memory;
    }

    public void LoadBios(BiosImage? bios)
    {
        if (bios is not null)
        {
            Array.Copy(bios.Bytes, _memory.Bios, Math.Min(_memory.Bios.Length, bios.Bytes.Length));
        }
    }

    public void LoadCartridge(GbaCartridge? cartridge) => _memory.Rom = cartridge?.RomData ?? [];

    public uint Read32New(uint address)
    {
        var aligned = address & ~3u;
        return 0;
    }

    public byte Read8(uint address)
    {
        //if (TryReadIo(address, out var ioValue))
        //{
        //    return ioValue;
        //}

        var region = ResolveRegion(address, out var buffer, out var offset);
        if (region is MemoryRegion.Unused)
        {
            Console.WriteLine("READ UNUSED");
            return 0x0;
        }
        if (region == MemoryRegion.Rom && _memory.Rom.Length == 0)
        {
            return 0xFF;
        }

        return buffer[offset];
    }

    public ushort Read16(uint address)
    {
        var lo = Read8(address);
        var hi = Read8(address + 1);
        return (ushort)((hi << 8) | lo);
    }

    public uint Read32(uint address)
    {
        var b0 = Read8(address);
        var b1 = Read8(address + 1);
        var b2 = Read8(address + 2);
        var b3 = Read8(address + 3);
        return (uint)((b3 << 24) | (b2 << 16) | (b1 << 8) | b0);
    }

    public void Write8(uint address, byte value)
    {
        if (TryWriteIo(address, value))
        {
            return;
        }

        var region = ResolveRegion(address, out var buffer, out var offset);

        if (region is MemoryRegion.Bios or MemoryRegion.Rom or MemoryRegion.Unused)
        {
            //throw new Exception("Cannot Write to bios or rom or unused memory");
            return;
        }

        buffer[offset] = value;
    }

    public void Write16(uint address, ushort value)
    {
        if (address is >= 0x04000000 and <= 0x040003FE)
        {
            WriteIo16(address, value);
            return;
        }

        Write8(address, (byte)value);
        Write8(address + 1, (byte)(value >> 8));
    }

    public void Write32(uint address, uint value)
    {
        if (address is >= 0x04000000 and <= 0x040003FC)
        {
            WriteIo32(address, value);
            return;
        }

        Write8(address, (byte)value);
        Write8(address + 1, (byte)(value >> 8));
        Write8(address + 2, (byte)(value >> 16));
        Write8(address + 3, (byte)(value >> 24));
    }

    private MemoryRegion ResolveRegion(uint address, out byte[] buffer, out int offset)
    {
        switch (address >> 24)
        {
            case 0x00:
                buffer = _memory.Bios;
                offset = (int)(address % (uint)_memory.Bios.Length);
                return MemoryRegion.Bios;
            case 0x02:
                buffer = _memory.Ewram;
                offset = (int)((address - 0x02000000) % (uint)_memory.Ewram.Length);
                return MemoryRegion.Ewram;
            case 0x03:
                buffer = _memory.Iwram;
                offset = (int)((address - 0x03000000) % (uint)_memory.Iwram.Length);
                return MemoryRegion.Iwram;
            case 0x04:
                buffer = [];
                offset = 0;
                return MemoryRegion.Io;
            case 0x05:
                buffer = _memory.PaletteRam;
                offset = (int)((address - 0x05000000) % (uint)_memory.PaletteRam.Length);
                return MemoryRegion.PaletteRam;
            case 0x06:
                buffer = _memory.Vram;
                offset = (int)((address - 0x06000000) % (uint)_memory.Vram.Length);
                return MemoryRegion.Vram;
            case 0x07:
                buffer = _memory.Oam;
                offset = (int)((address - 0x07000000) % (uint)_memory.Oam.Length);
                return MemoryRegion.Oam;
            case 0x08:
            case 0x09:
            case 0x0A:
            case 0x0B:
            case 0x0C:
            case 0x0D:
                buffer = _memory.Rom;
                offset = _memory.Rom.Length == 0 ? 0 : (int)((address - 0x08000000) % (uint)_memory.Rom.Length);
                return MemoryRegion.Rom;
            case 0x0E:
                buffer = _memory.Sram;
                offset = (int)((address - 0x0E000000) % (uint)_memory.Sram.Length);
                return MemoryRegion.Sram;
            default:
                buffer = [];
                offset = 0;
                Console.WriteLine($"Address Accessed: 0x{address:x8}");
                return MemoryRegion.Unused;
        }
    }

    private bool TryReadIo(uint address, out byte value)
    {
        if (address is < 0x04000000 or > 0x04FFFFFF)
        {
            value = 0;
            return false;
        }

        var aligned = address & ~1U;
        // ushort registerValue = aligned switch
        // {
        //     >= 0x04000000 and <= 0x0400001e => _ppu.Read16(aligned),
        //     >= 0x04000100 and <= 0x0400010E => _timers.Read16(aligned),
        //     0x04000130 => _keypad.ReadKeyInput(),
        //     0x04000200 or 0x04000202 or 0x04000208 => _interrupts.Read16(aligned),
        //     _ => 0
        // };

        //value = (byte)((registerValue >> ((int)(address & 1) * 8)) & 0xFF);
        value = 0;
        return true;
    }

    private bool TryWriteIo(uint address, byte value)
    {
        if (address is < 0x04000000 or > 0x040003FF)
        {
            return false;
        }

        if (address is >= 0x04000000 and <= 0x04000006)
        {
            Console.WriteLine("TRYING TO WRITE TO DISPSTAT THINGS");
        }

        var aligned = address & ~1U;
        var existing = Read16(aligned);
        var shift = (int)(address & 1) * 8;
        var merged = (ushort)((existing & ~(0xFF << shift)) | (value << shift));
        WriteIo16(aligned, merged);
        return true;
    }

    private void WriteIo16(uint address, ushort value)
    {
        switch (address)
        {
            case >= 0x04000000 and <= 0x04000054:
                //_ppu.Write16(address, value);
                break;
            case >= 0x040000B0 and <= 0x040000DE:
                //_dma.Write16(address, value, this);
                break;
            case >= 0x04000100 and <= 0x0400010E:
                //_timers.Write16(address, value);
                break;
            case 0x04000200:
            case 0x04000202:
            case 0x04000208:
                //_interrupts.Write16(address, value);
                break;
        }
    }

    private void WriteIo32(uint address, uint value)
    {
        WriteIo16(address, (ushort)value);
        WriteIo16(address + 2, (ushort)(value >> 16));
    }
}