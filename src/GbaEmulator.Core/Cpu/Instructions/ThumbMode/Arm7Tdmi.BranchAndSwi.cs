namespace GbaEmulator.Core.Cpu;

public sealed partial class Arm7Tdmi
{
    private void Bl(ushort instruction)
    {
        
    }

    private void B(ushort instruction)
    {
        
    }

    private void BCond(ushort instruction)
    {
        
    }

    private void Bx(ushort instruction)
    {
        
    }

    private void Swi(ushort instruction)
    {
        
    }

    private void Illegal(ushort instruction)
    {
        throw new InvalidOperationException("Illegal instruction");
    }
}