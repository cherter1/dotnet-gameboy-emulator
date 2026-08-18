namespace GbaEmulator.Core.Timers;

public struct TimerState
{
    public ushort Reload;
    public ushort CounterAtBase;
    public ushort Control;

    public ulong BaseCycle;
    public ulong NextOverflowCycle;

    public byte PrescalerShift;

    public bool Enabled;
    public bool Cascade;
    public bool IrqEnabled;
}