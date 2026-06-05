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
    public RegisterBank Registers { get; set; }
    private readonly InterruptController _interrupts;
    private readonly TimerController _timers;
    private readonly DmaController _dma;
    private readonly Ppu _ppu;
    private readonly KeypadState _keypad;

    private byte[] _bios = new byte[0x4000]; //16KB
    private readonly byte[] _ewram = new byte[0x40000]; //256KB
    private readonly byte[] _iwram = new byte[0x8000]; //32KB
    private readonly byte[] _paletteRam = new byte[0x400]; //1KB
    private readonly byte[] _vram = new byte[0x18000]; //96KB
    private readonly byte[] _oam = new byte[0x400]; //1KB
    private byte[] _rom = [];
    private readonly byte[] _sram = new byte[0x10000]; //64KB

    public GbaBus(
        InterruptController interrupts,
        TimerController timers,
        DmaController dma,
        Ppu ppu,
        KeypadState keypad)
    {
        _interrupts = interrupts;
        _timers = timers;
        _dma = dma;
        _ppu = ppu;
        _keypad = keypad;
        _ppu.ConnectMemory(_vram, _paletteRam);
    }

    public void LoadBios(BiosImage? bios)
    {
        _bios = new byte[0x4000];
        if (bios is not null)
        {
            Array.Copy(bios.Bytes, _bios, Math.Min(_bios.Length, bios.Bytes.Length));
        }
    }

    public void LoadCartridge(GbaCartridge? cartridge) => _rom = cartridge?.RomData ?? [];

    public byte Read8(uint address, bool called = false)
    {
        if (TryReadIo(address, out var ioValue))
        {
            return ioValue;
        }

        var region = ResolveRegion(address, out var buffer, out var offset);
        if (region is MemoryRegion.Unused)
        {
            Console.WriteLine("READ");
        }
        if (region == MemoryRegion.Rom && _rom.Length == 0)
        {
            return 0xFF;
        }

        return buffer[offset];
    }

    public ushort Read16(uint address)
    {
        var lo = Read8(address, true);
        var hi = Read8(address + 1, true);
        return (ushort)(lo | (hi << 8));
    }

    public uint Read32(uint address)
    {
        var b0 = Read8(address, true);
        var b1 = Read8(address + 1, true);
        var b2 = Read8(address + 2, true);
        var b3 = Read8(address + 3, true);
        return (uint)(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
    }

    public void Write8(uint address, byte value)
    {
        if (TryWriteIo(address, value))
        {
            return;
        }

        var region = ResolveRegion(address, out var buffer, out var offset);
        if (region is MemoryRegion.Unused)
        {
            Console.WriteLine("WRITE");
        }
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

        Write8(address, (byte)(value & 0xFF));
        Write8(address + 1, (byte)(value >> 8));
    }

    public void Write32(uint address, uint value)
    {
        if (address is >= 0x04000000 and <= 0x040003FC)
        {
            WriteIo32(address, value);
            return;
        }

        Write8(address, (byte)(value & 0xFF));
        Write8(address + 1, (byte)((value >> 8) & 0xFF));
        Write8(address + 2, (byte)((value >> 16) & 0xFF));
        Write8(address + 3, (byte)((value >> 24) & 0xFF));
    }

    private MemoryRegion ResolveRegion(uint address, out byte[] buffer, out int offset)
    {
        switch (address >> 24)
        {
            case 0x00:
                buffer = _bios;
                offset = (int)(address % (uint)_bios.Length);
                return MemoryRegion.Bios;
            case 0x02:
                buffer = _ewram;
                offset = (int)((address - 0x02000000) % (uint)_ewram.Length);
                return MemoryRegion.Ewram;
            case 0x03:
                buffer = _iwram;
                offset = (int)((address - 0x03000000) % (uint)_iwram.Length);
                return MemoryRegion.Iwram;
            case 0x05:
                buffer = _paletteRam;
                offset = (int)((address - 0x05000000) % (uint)_paletteRam.Length);
                return MemoryRegion.PaletteRam;
            case 0x06:
                buffer = _vram;
                offset = (int)((address - 0x06000000) % (uint)_vram.Length);
                return MemoryRegion.Vram;
            case 0x07:
                buffer = _oam;
                offset = (int)((address - 0x07000000) % (uint)_oam.Length);
                return MemoryRegion.Oam;
            case 0x08:
            case 0x09:
            case 0x0A:
            case 0x0B:
            case 0x0C:
            case 0x0D:
                buffer = _rom;
                offset = _rom.Length == 0 ? 0 : (int)((address - 0x08000000) % (uint)_rom.Length);
                return MemoryRegion.Rom;
            case 0x0E:
                buffer = _sram;
                offset = (int)((address - 0x0E000000) % (uint)_sram.Length);
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
        ushort registerValue = aligned switch
        {
            >= 0x04000000 and <= 0x0400001e => _ppu.Read16(aligned),
            >= 0x04000100 and <= 0x0400010E => _timers.Read16(aligned),
            0x04000130 => _keypad.ReadKeyInput(),
            0x04000200 or 0x04000202 or 0x04000208 => _interrupts.Read16(aligned),
            _ => 0
        };

        value = (byte)((registerValue >> ((int)(address & 1) * 8)) & 0xFF);
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
        if (address is >= 0x04000000 and <= 0x04000006)
        {
            Console.WriteLine("TRYING TO WRITE TO DISPSTAT OR DISPCNT or vcount");
        }
        switch (address)
        {
            case >= 0x04000000 and <= 0x04000054:
                _ppu.Write16(address, value);
                break;
            case >= 0x040000B0 and <= 0x040000DE:
                _dma.Write16(address, value, this);
                break;
            case >= 0x04000100 and <= 0x0400010E:
                _timers.Write16(address, value);
                break;
            case 0x04000200:
            case 0x04000202:
            case 0x04000208:
                _interrupts.Write16(address, value);
                break;
        }
    }

    private void WriteIo32(uint address, uint value)
    {
        if (address is >= 0x040000b0 and <= 0x040000de)
        {
            Console.WriteLine("writing to dma");
        }
        WriteIo16(address, (ushort)(value & 0xFFFF));
        WriteIo16(address + 2, (ushort)(value >> 16));
    }
}