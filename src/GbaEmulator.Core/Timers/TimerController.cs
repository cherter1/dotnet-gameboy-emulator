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

    public ushort Read16(uint address)
    {
        var channelIndex = (int)((address - 0x04000100) / 4);
        if (channelIndex is < 0 or > 3)
        {
            return 0;
        }

        var channel = _channels[channelIndex];
        return ((address - 0x04000100) % 4) switch
        {
          //  0 => channel.Counter,
           // 2 => channel.Control,
            _ => 0
        };
    }

    public void Write16(uint address, ushort value)
    {
        var channelIndex = (int)((address - 0x04000100) / 4);
        if (channelIndex is < 0 or > 3)
        {
            return;
        }

        var channel = _channels[channelIndex];
        switch ((address - 0x04000100) % 4)
        {
            case 0:
                //channel.Reload = value;
                break;
            case 2:
                //channel.Control = value;
                if ((value & 0x0080) != 0)
                {
                    //channel.Counter = channel.Reload;
                }

                break;
        }
    }
}
