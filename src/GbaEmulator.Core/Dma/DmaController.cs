using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Dma;

public sealed class DmaController
{
    private readonly DmaChannel[] _channels;
    private readonly InterruptController _interrupts;

    internal DmaController(InterruptController interrupts, GbaMemory memory)
    {
        _interrupts = interrupts;
        _channels = Enumerable.Range(0, 4).Select((_, index) => new DmaChannel(memory, index)).ToArray();
    }

    public void RunDmas(DmaTimingType timingType, GbaBus bus)
    {
        foreach (var channel in _channels)
        {
            if ((channel.Control & 0x8000) != 0x8000 ||
                (DmaTimingType)((channel.Control >> 12) & 0b11) != timingType)
            {
                continue;
            }

            var destIncType = (channel.Control >> 5) & 0b11;
            var sourceIncType = (channel.Control >> 7) & 0b11;
            var repeat = ((channel.Control >> 9) & 1) == 1;
            var is32BitCopy = ((channel.Control >> 10) & 1) == 1;

            var dest = channel.DestinationAddress;
            var source = channel.SourceAddress;
            var unitSize = is32BitCopy ? 4u : 2u;

            for (int i = 0; i < channel.Count; i++)
            {
                if (is32BitCopy)
                {
                    bus.Write32(dest, bus.Read32(source));
                }
                else
                {
                    bus.Write16(dest, bus.Read16(source));
                }

                switch (sourceIncType)
                {
                    case 0:
                        source += unitSize;
                        break;
                    case 1:
                        source -= unitSize;
                        break;
                }
                switch (destIncType)
                {
                    case 0:
                        dest += unitSize;
                        break;
                    case 1:
                        dest -= unitSize;
                        break;
                }
            }

            channel.SourceAddress = source;
            channel.DestinationAddress = dest;
            channel.Control = (ushort)(channel.Control & ~0x8000);

            if ((channel.Control >> 14 & 1) != 1)
            {
                continue;
            }

            var channelIndex = Array.IndexOf(_channels, channel);
            _interrupts.Request((InterruptType)((ushort)InterruptType.Dma0 << channelIndex));
        }
    }
}
