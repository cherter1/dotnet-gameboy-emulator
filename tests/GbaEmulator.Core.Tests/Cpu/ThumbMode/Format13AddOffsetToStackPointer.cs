using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format13AddOffsetToStackPointer
{
    [Fact]
    public void ADD_NegativeImmediate_UpdatesStackPointer()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sub sp, #104
        bus.Write16(0x02000000, 0xB09A);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        //0x03007F00
        Assert.Equal(0x03007e98u, cpu.Registers.StackPointer);
    }

    [Fact]
    public void ADD_PositiveImmediate_UpdatesStackPointer()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add sp, #268
        bus.Write16(0x02000000, 0xB043);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0300800Cu, cpu.Registers.StackPointer);
    }
}