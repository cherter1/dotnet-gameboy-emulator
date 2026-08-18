using GbaEmulator.Core.Interrupts;

namespace GbaEmulator.Core.Timers;

public sealed class TimerChannel(int index)
{
    private static readonly int[] PrescalerValues = [1, 64, 256, 1024];
    private int _prescalerAccumulator;

    public void Step(int cycles, ushort control, ref ushort counter, ushort reload, InterruptController interrupts)
    {
        if ((control & 0x80) == 0) //bit 7 set high enabled
        {
            return;
        }

        var prescaler = PrescalerValues[control & 0b11];
        _prescalerAccumulator += cycles;

        if ((control & 0b100) != 0 && index != 0) //bit 2 set cascade, except timer 0
        {
            //cascade overflow
            return;
        }
        while (_prescalerAccumulator >= prescaler)
        {
            _prescalerAccumulator -= prescaler;
            counter++;
            if (counter != 0)
            {
                continue;
            }

            counter = reload;
            if ((control & 0x40) != 0)
            {
                interrupts.Request((InterruptType)((ushort)InterruptType.Timer0 << index));
            }
        }
    }
}