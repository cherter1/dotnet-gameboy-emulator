using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Interrupts;

public sealed class InterruptController(GbaMemory memory)
{
    public bool ShouldServiceIrq(bool irqDisabled) =>
        !irqDisabled && memory.Io.REG_IME && (memory.Io.REG_IE & memory.Io.REG_IF) != 0;

    public void Request(InterruptType interrupt) => memory.Io.REG_IF |= (ushort)interrupt;
}
