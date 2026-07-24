using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Timers;

public sealed class TimerController
{
    private readonly TimerChannel[] _channels;
    private readonly InterruptController _interrupts;

    public TimerController(InterruptController interrupts, GbaMemory memory)
    {
        _interrupts = interrupts;
        _channels = Enumerable.Range(0, 4).Select((_, index) => new TimerChannel(memory, index)).ToArray();
    }

    public void Step(int cycles)
    {
        foreach (var channel in _channels)
        {
            channel.Step(cycles, _interrupts);
        }
    }
}
