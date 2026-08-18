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

    private ulong _cycles;
    private ulong _nextTimerOverflow = ulong.MaxValue;
    private readonly TimerState[] _timers = new TimerState[4];

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

    public void Advance(int cycles)
    {
        _cycles += (uint)cycles;
        if (_cycles >= _nextTimerOverflow)
        {
            ProcessTimerOverflows();
        }
    }

    private void ProcessTimerOverflows()
    {
        for (int i = 0; i < 4; i++)
        {
            ref TimerState timer = ref _timers[i];

            if (!timer.Enabled || timer.Cascade || timer.NextOverflowCycle > _cycles)
            {
                continue;
            }
            ProcessClockTimerOverflow(i);
        }

        RecalculateNextTimerOverflow();
    }

    private void ProcessClockTimerOverflow(int index)
    {
        ref TimerState timer = ref _timers[index];

        ulong termCycles = (0x10000ul - timer.Reload) << timer.PrescalerShift; // timerTerm * Prescalar(cycles per increment) = totalCycles from start to overflow

        ulong overflowCount = 1 + ((_cycles - timer.NextOverflowCycle) / termCycles); //num overflows
        ulong lastOverflowCycle = timer.NextOverflowCycle + ((overflowCount - 1) * termCycles);

        timer.CounterAtBase = timer.Reload;
        timer.BaseCycle = lastOverflowCycle;
        timer.NextOverflowCycle += overflowCount * termCycles;

        if (timer.IrqEnabled)
        {
            _interrupts.Request((InterruptType)((ushort)InterruptType.Timer0 << index));
        }

        if (index < 3)
        {
            ApplyCascadeTicks(index + 1, overflowCount);
        }
    }

    private void ApplyCascadeTicks(int index, ulong ticks)
    {
        ref TimerState timer = ref _timers[index];
        if (!timer.Enabled || !timer.Cascade || ticks == 0)
        {
            return;
        }

        ulong counter = timer.CounterAtBase;
        ulong incrementsUntilOverflow = 0x10000ul - counter;

        if (ticks < incrementsUntilOverflow)
        {
            timer.CounterAtBase = (ushort)(counter + ticks);
            return;
        }

        ulong remainingTicks = ticks - incrementsUntilOverflow;
        ulong term = 0x10000ul - timer.Reload; //full term of timer from start to overflow

        ulong overflowCount = 1 + (remainingTicks / term); //num overflows

        timer.CounterAtBase = (ushort)(timer.Reload + (remainingTicks % term));
        if (timer.IrqEnabled)
        {
            _interrupts.Request((InterruptType)((ushort)InterruptType.Timer0 << index));
        }

        if (index < 3)
        {
            ApplyCascadeTicks(index + 1, overflowCount);
        }
    }

    private void RecalculateNextTimerOverflow()
    {
        ulong next = ulong.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            ref  TimerState timer = ref _timers[i];

            if (timer.Enabled && !timer.Cascade && timer.NextOverflowCycle < next)
            {
                next = timer.NextOverflowCycle;
            }
        }

        _nextTimerOverflow = next;
    }
}
