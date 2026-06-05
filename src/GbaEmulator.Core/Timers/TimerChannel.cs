using GbaEmulator.Core.Interrupts;

namespace GbaEmulator.Core.Timers;

sealed class TimerChannel
{
    private static readonly int[] PrescalerValues = [1, 64, 256, 1024];
    private int _prescalerAccumulator;

    public ushort Reload { get; set; }

    public ushort Counter { get; set; }

    //bit 7: enable Timer
    //bit 6: Generate an interrupt Overflow
    //bit 2: Cascade (not Timer0)
    //bit 1-0: frequency
    public ushort Control { get; set; }

    public void Step(int cycles, int channelIndex, InterruptController interrupts)
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
                interrupts.Request((InterruptType)((ushort)InterruptType.Timer0 << channelIndex));
            }
        }
    }
}