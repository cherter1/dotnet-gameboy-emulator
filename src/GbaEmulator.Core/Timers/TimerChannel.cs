using System.Diagnostics;
using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Timers;

public sealed class TimerChannel(GbaMemory memory, int index)
{
    private static readonly int[] PrescalerValues = [1, 64, 256, 1024];
    private int _prescalerAccumulator;

    private ushort Reload
    {
        get => ResolveChannelReload();
    }

    private ushort Counter
    {
        get => ResolveChannelCounter();
        set => SetChannelCounter(value);
    }

    //bit 7: enable Timer
    //bit 6: Generate an interrupt Overflow
    //bit 2: Cascade (not Timer0)
    //bit 1-0: frequency
    private ushort Control
    {
        get => ResolveChannelControl();
    }

    public void Step(int cycles, InterruptController interrupts)
    {
        if ((Control & 0x0080) == 0)
        {
            return;
        }

        var prescaler = PrescalerValues[Control & 0x0003];
        _prescalerAccumulator += cycles;

        while (_prescalerAccumulator >= prescaler)
        {
            _prescalerAccumulator -= prescaler;
            Counter++;
            if (Counter != 0)
            {
                continue;
            }

            Counter = Reload;
            if ((Control & 0x0040) != 0)
            {
                interrupts.Request((InterruptType)((ushort)InterruptType.Timer0 << index));
            }
        }
    }

    private ushort ResolveChannelControl()
    {
        return index switch
        {
            0 => memory.Io.REG_TM0CNT,
            1 => memory.Io.REG_TM1CNT,
            2 => memory.Io.REG_TM2CNT,
            3 => memory.Io.REG_TM3CNT,
            _ => throw new UnreachableException("Invalid Timer channel index")
        };
    }

    private ushort ResolveChannelReload()
    {
        return index switch
        {
            0 => memory.Io.REG_TM0D_RELOAD,
            1 => memory.Io.REG_TM1D_RELOAD,
            2 => memory.Io.REG_TM2D_RELOAD,
            3 => memory.Io.REG_TM3D_RELOAD,
            _ => throw new UnreachableException("Invalid Timer channel index")
        };
    }

    private ushort ResolveChannelCounter()
    {
        return index switch
        {
            0 => memory.Io.REG_TM0D_COUNTER,
            1 => memory.Io.REG_TM1D_COUNTER,
            2 => memory.Io.REG_TM2D_COUNTER,
            3 => memory.Io.REG_TM3D_COUNTER,
            _ => throw new UnreachableException("Invalid Timer channel index")
        };
    }

    private void SetChannelCounter(ushort value)
    {
        switch (index)
        {
            case 0:
                memory.Io.REG_TM0D_COUNTER = value;
                break;
            case 1:
                memory.Io.REG_TM1D_COUNTER = value;
                break;
            case 2:
                memory.Io.REG_TM2D_COUNTER = value;
                break;
            case 3:
                memory.Io.REG_TM3D_COUNTER = value;
                break;
            default:
                throw new UnreachableException("Invalid Timer channel index");
        }
    }
}