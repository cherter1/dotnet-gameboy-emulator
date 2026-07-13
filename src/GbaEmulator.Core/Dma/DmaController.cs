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

    public void Write16(uint address, ushort value, GbaBus bus)
    {
        if (!TryResolve(address, out var channel, out var offset))
        {
            return;
        }

        switch (offset)
        {
            case 0:
                channel.SourceAddress = (channel.SourceAddress & 0xFFFF0000u) | value;
                break;
            case 2:
                channel.SourceAddress = (channel.SourceAddress & 0x0000FFFFu) | (uint)(value << 16);
                break;
            case 4:
                channel.DestinationAddress = (channel.DestinationAddress & 0xFFFF0000u) | value;
                break;
            case 6:
                channel.DestinationAddress = (channel.DestinationAddress & 0x0000FFFFu) | (uint)(value << 16);
                break;
            case 8:
                channel.Count = value == 0 ? (ushort)0x4000 : value;
                break;
            case 10:
                channel.Control = value;
                if ((value & 0x8000) != 0)
                {
                    RunDmas(DmaTimingType.Immediately, bus);
                }

                break;
        }
    }

    public void Write32(uint address, uint value)
    {
        if (!TryResolve(address, out var channel, out var offset))
        {
            return;
        }

        switch (offset)
        {
            case 0:
                channel.SourceAddress = value;
                break;
            case 4:
                channel.DestinationAddress = value;
                break;
        }
    }

    private void RunImmediateTransfer(DmaChannel channel, GbaBus bus)
    {
        var copyWords = (channel.Control & 0x0400) != 0;
        var unitSize = copyWords ? 4U : 2U;
        var source = channel.SourceAddress;
        var destination = channel.DestinationAddress;

        for (var i = 0; i < channel.Count; i++)
        {
            if (copyWords)
            {
                bus.Write32(destination, bus.Read32(source));
            }
            else
            {
                bus.Write16(destination, bus.Read16(source));
            }

            source += unitSize;
            destination += unitSize;
        }

        channel.SourceAddress = source;
        channel.DestinationAddress = destination;
        channel.Control = (ushort)(channel.Control & ~0x8000);

        if ((channel.Control & 0x4000) == 0) return;

        var channelIndex = Array.IndexOf(_channels, channel);
        _interrupts.Request((InterruptType)((ushort)InterruptType.Dma0 << channelIndex));
    }

    private bool TryResolve(uint address, out DmaChannel channel, out uint offset)
    {
        if (address is < 0x040000B0 or > 0x040000DE)
        {
            channel = null!;
            offset = 0;
            return false;
        }

        var channelIndex = (int)((address - 0x040000B0) / 12);
        if (channelIndex is < 0 or > 3)
        {
            channel = null!;
            offset = 0;
            return false;
        }

        channel = _channels[channelIndex];
        offset = (address - 0x040000B0) % 12;
        return true;
    }
}
