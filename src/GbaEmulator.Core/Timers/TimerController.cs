using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Timers;

public sealed class TimerController
{
    private readonly TimerChannel _channel0;
    private readonly TimerChannel _channel1;
    private readonly TimerChannel _channel2;
    private readonly TimerChannel _channel3;
    private readonly InterruptController _interrupts;
    private readonly GbaMemory _memory;

    public TimerController(InterruptController interrupts, GbaMemory memory)
    {
        _interrupts = interrupts;
        _memory = memory;
        _channel0 = new TimerChannel(0);
        _channel1 = new TimerChannel(1);
        _channel2 = new TimerChannel(2);
        _channel3 = new TimerChannel(3);
        //_channels = [new TimerChannel(memory, 0), new TimerChannel(memory, 1), new TimerChannel(memory, 2), new TimerChannel(memory, 3)];
        //_channels = Enumerable.Range(0, 4).Select((_, index) => new TimerChannel(memory, index)).ToArray();
    }

    public void Step(int cycles)
    {
        var tm0Counter = _memory.Io.REG_TM0D_COUNTER;
        _channel0.Step(cycles, _memory.Io.REG_TM0CNT, ref tm0Counter, _memory.Io.REG_TM0D_RELOAD, _interrupts);
        _memory.Io.REG_TM0D_COUNTER = tm0Counter;
        var tm1Counter = _memory.Io.REG_TM1D_COUNTER;
        _channel1.Step(cycles, _memory.Io.REG_TM1CNT, ref tm1Counter, _memory.Io.REG_TM1D_RELOAD, _interrupts);
        _memory.Io.REG_TM1D_COUNTER = tm1Counter;
        var tm2Counter = _memory.Io.REG_TM2D_COUNTER;
        _channel2.Step(cycles, _memory.Io.REG_TM2CNT, ref tm2Counter, _memory.Io.REG_TM2D_RELOAD, _interrupts);
        _memory.Io.REG_TM2D_COUNTER = tm2Counter;
        var tm3Counter = _memory.Io.REG_TM3D_COUNTER;
        _channel3.Step(cycles, _memory.Io.REG_TM3CNT, ref tm3Counter, _memory.Io.REG_TM3D_RELOAD, _interrupts);
        _memory.Io.REG_TM3D_COUNTER = tm3Counter;
    }
}
