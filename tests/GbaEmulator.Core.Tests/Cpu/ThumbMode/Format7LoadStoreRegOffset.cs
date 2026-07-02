using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format7LoadStoreRegOffset
{
    /*
      2000000:       5088            str     r0, [r1, r2]
      2000002:       5888            ldr     r0, [r1, r2]
      2000004:       5488            strb    r0, [r1, r2]
      2000006:       5c88            ldrb    r0, [r1, r2] 
     */

    [Fact]
    public void LDR__Temp()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: str r0 [r1, r2]
        bus.Write16(0x02000000, 0x5088);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x02000000;
        cpu.Registers[1] = 0x02000000;
        cpu.Registers[2] = 0x02000000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x12345678u, cpu.Registers[0]);
    }
}