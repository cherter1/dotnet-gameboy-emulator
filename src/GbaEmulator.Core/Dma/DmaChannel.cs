using System.Diagnostics;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Dma;

internal sealed class DmaChannel
{
    private readonly GbaMemory _memory;
    private readonly int _index;
    public DmaChannel(GbaMemory memory, int index)
    {
        _memory = memory;
        _index = index;
    }
    /// <summary>
    /// REG_DMA0SAD: 0x040000B0 27bits
    /// REG_DMA1SAD: 0x040000BC 28bits
    /// REG_DMA2SAD: 0x040000C8 28bits
    /// REG_DMA3SAD: 0x040000D4 28bits
    /// </summary>
    public uint SourceAddress
    {
        get => ResolveChannelSource();
        set => SetChannelSource(value);
    }
    /// <summary>
    /// REG_DMA0DAD: 0x040000B4 27bits
    /// REG_DMA1DAD: 0x040000C0 27bits
    /// REG_DMA2DAD: 0x040000CC 27bits
    /// REG_DMA3DAD: 0x040000D8 28bits
    /// </summary>
    public uint DestinationAddress
    {
        get => ResolveChannelDestination();
        set => SetChannelDestination(value);
    }
    /// <summary>
    /// REG_DMA0CNT_L: 0x040000B8
    /// REG_DMA1CNT_L: 0x040000C4
    /// REG_DMA2CNT_L: 0x040000D0
    /// REG_DMA3CNT_L: 0x040000DC
    /// </summary>
    public ushort Count
    {
        get => ResolveChannelCount();
        set => SetChannelCount(value);
    }
    /// <summary>
    /// REG_DMA0CNT_H: 0x040000BA
    /// REG_DMA1CNT_H: 0x040000C6
    /// REG_DMA2CNT_H: 0x040000D2
    /// REG_DMA3CNT_H: 0x040000DE
    /// </summary>
    public ushort Control
    {
        get => ResolveChannelControl();
        set => SetChannelControl(value);
    }

    private uint ResolveChannelSource()
    {
        return _index switch
        {
            0 => _memory.Io.REG_DMA0SAD,
            1 => _memory.Io.REG_DMA1SAD,
            2 => _memory.Io.REG_DMA2SAD,
            3 => _memory.Io.REG_DMA3SAD,
            _ => throw new UnreachableException("Invalid DMA channel index")
        };
    }

    private void SetChannelSource(uint value)
    {
        switch (_index)
        {
            case 0:
                _memory.Io.REG_DMA0SAD = value;
                break;
            case 1:
                _memory.Io.REG_DMA1SAD = value;
                break;
            case 2:
                _memory.Io.REG_DMA2SAD = value;
                break;
            case 3:
                _memory.Io.REG_DMA3SAD = value;
                break;
            default:
                throw new UnreachableException("Invalid DMA channel index");
        }
    }

    private uint ResolveChannelDestination()
    {
        return _index switch
        {
            0 => _memory.Io.REG_DMA0DAD,
            1 => _memory.Io.REG_DMA1DAD,
            2 => _memory.Io.REG_DMA2DAD,
            3 => _memory.Io.REG_DMA3DAD,
            _ => throw new UnreachableException("Invalid DMA channel index")
        };
    }

    private void SetChannelDestination(uint value)
    {
        switch (_index)
        {
            case 0:
                _memory.Io.REG_DMA0DAD = value;
                break;
            case 1:
                _memory.Io.REG_DMA1DAD = value;
                break;
            case 2:
                _memory.Io.REG_DMA2DAD = value;
                break;
            case 3:
                _memory.Io.REG_DMA3DAD = value;
                break;
            default:
                throw new UnreachableException("Invalid DMA channel index");
        }
    }

    private ushort ResolveChannelCount()
    {
        return _index switch
        {
            0 => _memory.Io.REG_DMA0CNT_L,
            1 => _memory.Io.REG_DMA1CNT_L,
            2 => _memory.Io.REG_DMA2CNT_L,
            3 => _memory.Io.REG_DMA3CNT_L,
            _ => throw new UnreachableException("Invalid DMA channel index")
        };
    }

    private void SetChannelCount(ushort value)
    {
        switch (_index)
        {
            case 0:
                _memory.Io.REG_DMA0CNT_L = value;
                break;
            case 1:
                _memory.Io.REG_DMA1CNT_L = value;
                break;
            case 2:
                _memory.Io.REG_DMA2CNT_L = value;
                break;
            case 3:
                _memory.Io.REG_DMA3CNT_L = value;
                break;
            default:
                throw new UnreachableException("Invalid DMA channel index");
        }
    }

    private ushort ResolveChannelControl()
    {
        return _index switch
        {
            0 => _memory.Io.REG_DMA0CNT_H,
            1 => _memory.Io.REG_DMA1CNT_H,
            2 => _memory.Io.REG_DMA2CNT_H,
            3 => _memory.Io.REG_DMA3CNT_H,
            _ => throw new UnreachableException("Invalid DMA channel index")
        };
    }

    private void SetChannelControl(ushort value)
    {
        switch (_index)
        {
            case 0:
                _memory.Io.REG_DMA0CNT_H = value;
                break;
            case 1:
                _memory.Io.REG_DMA1CNT_H = value;
                break;
            case 2:
                _memory.Io.REG_DMA2CNT_H = value;
                break;
            case 3:
                _memory.Io.REG_DMA3CNT_H = value;
                break;
            default:
                throw new UnreachableException("Invalid DMA channel index");
        }
    }
}