using GbaEmulator.Core;
using GbaEmulator.Core.Cpu;

namespace GbaEmulator.App.Hosting;

public sealed class EmulatorStartup
{
    public required GbaMachine Machine { get; init; }
    public required string StatusMessage { get; init; }
    public required string WindowTitle { get; init; }

    public static EmulatorStartup Create(string[] args, string appBaseDirectory)
    {
        var startup = RomDiscovery.Resolve(args, appBaseDirectory);
        var machine = GbaMachine.Create(new GbaMachineOptions
        {
            RomPath = startup.RomPath,
            BiosPath = startup.BiosPath,
            SaveDirectory = startup.SaveDirectory,
            SkipBios = startup.BiosPath is null
        });

        var title = machine.Cartridge?.Title is { Length: > 0 } romTitle
            ? $"GBA Emulator - {romTitle}"
            : "GBA Emulator";

        return new EmulatorStartup
        {
            Machine = machine,
            StatusMessage = startup.Message,
            WindowTitle = title
        };
    }
}
/*
           |...._.1.._...._...0|
           |5432_1098_7654_3210|
f2 |0001_1..._...._....|
add |0001_1.0._...._....| //r or 3bit imm
sub |0001_1.1._...._....| //r or 3bit imm
        
f1 |000E_E..._...._....|
lsl |0000_0..._...._....| //5bit imm shifter
lsr |0000_1..._...._....| //5bit imm shifter
asr |0001_0..._...._....| //5bit imm shifter
        
f3 |001._...._...._....|
mov |0010_0..._...._....| //8bit imm offset
cmp |0010_1..._...._....| //8bit imm offset
add |0011_0..._...._....| //8bit imm offset
sub |0011_1..._...._....| //8bit imm offset
        
f4 |0100_00.._...._....| alu
and |0100_0000_00.._....| //lo r pair
eor |0100_0000_01.._....| //lo r pair
lsl |0100_0000_10.._....| //lo r pair
lsr |0100_0000_11.._....| //lo r pair
asr |0100_0001_00.._....| //lo r pair
adc |0100_0001_01.._....| //lo r pair
sbc |0100_0001_10.._....| //lo r pair
ror |0100_0001_11.._....| //lo r pair
tst |0100_0010_00.._....| //lo r pair
neg |0100_0010_01.._....| //lo r pair
cmp |0100_0010_10.._....| //lo r pair
cmn |0100_0010_11.._....| //lo r pair
orr |0100_0011_00.._....| //lo r pair
mul |0100_0011_01.._....| //lo r pair
bic |0100_0011_10.._....| //lo r pair
mvn |0100_0011_11.._....| //lo r pair
        
f5 |0100_01.._...._....|
add |0100_0100_...._....| //lo/hi r or hi r pair
cmp |0100_0101_...._....| //lo/hi r or hi r pair
mov |0100_0110_...._....| //lo/hi r or hi r pair
bx |0100_0111_...._....|
        
f6 |0100_1..._...._....|
ldr |0100_1..._...._....| //with pc
        
f7 |0101_..0._...._....|
str |0101_000._...._....| //reg offset
strb |0101_010._...._....| //reg offset
ldr |0101_100._...._....| //reg offset
ldrb |0101_110._...._....| //reg offset
        
f8 |0101_..1._...._....|
strh |0101_001._...._....| //reg offset
ldsb |0101_011._...._....| //reg offset
ldrh |0101_101._...._....| //reg offset
ldsh |0101_111._...._....| //reg offset
        
f9 |011._...._...._....|
str |0110_0..._...._....| //imm offset
ldr |0110_1..._...._....| //imm offset
strb |0111_0..._...._....| //imm offset
ldrb |0111_1..._...._....| //imm offset
        
f10 |1000_...._...._....|
strh |1000_0..._...._....| //imm offset
ldrh |1000_1..._...._....| //imm offset
       
f11 |1001_...._...._....|
str |1001_0..._...._....| //with sp
ldr |1001_1..._...._....| //with sp
       
f12 |1010_...._...._....|
add |1010_0..._...._....| //with pc
add |1010_1..._...._....| //with sp

f13 |1011_0000_...._....|
add |1011_0000_0..._....| //sp += imm
sub |1011_0000_1..._....| //sp -= imm
       
f14 |1011_.10._...._....|
push |1011_010._...._....| //write
pop |1011_110._...._....| //read
       
f15 |1100_...._...._....|
stm |1100_0..._...._....| //ia
ldm |1100_1..._...._....| //ia
       
f16 |1101_...._...._....|
b{cond} |1101_cccc_...._....| //not 1111 thats for swi
       
f17 |1101_1111_...._....|
swi |1101_1111_...._....|
       
f18 |1110_0..._...._....|
b |1110_0..._...._....| //unconditional
       
f19 |1111_...._...._....|
bl |1111_...._...._....| //long branch

*/