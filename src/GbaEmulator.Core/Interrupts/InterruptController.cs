namespace GbaEmulator.Core.Interrupts;

public sealed class InterruptController
{
    /// <summary>
    /// REG_IE: 0x04000200
    /// </summary>
    private ushort InterruptEnable { get; set; }
    /// <summary>
    /// REG_IF: 0x04000202
    /// </summary>
    private ushort InterruptFlags { get; set; }
    /// <summary>
    /// REG_IME: 0x04000208
    /// </summary>
    private bool InterruptMasterEnable { get; set; }

    public bool ShouldServiceIrq(bool irqDisabled) => InterruptMasterEnable && !irqDisabled && (InterruptEnable & InterruptFlags) != 0;

    public void Request(InterruptType interrupt) => InterruptFlags |= (ushort)interrupt;

    public void AcknowledgePendingIrq() => InterruptFlags = 0;

    public ushort Read16(uint address) =>
        address switch
        {
            0x04000200 => InterruptEnable,
            0x04000202 => InterruptFlags,
            0x04000208 => (ushort)(InterruptMasterEnable ? 1 : 0),
            _ => 0
        };

    public void Write16(uint address, ushort value)
    {
        switch (address)
        {
            case 0x04000200:
                InterruptEnable = value;
                break;
            case 0x04000202:
                InterruptFlags = (ushort)(InterruptFlags & ~value);
                break;
            case 0x04000208:
                InterruptMasterEnable = (value & 1) != 0;
                break;
        }
    }
}
