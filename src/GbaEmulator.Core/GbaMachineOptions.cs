namespace GbaEmulator.Core;

public sealed class GbaMachineOptions
{
    public string? RomPath { get; init; }

    public string? BiosPath { get; init; }

    public string? SaveDirectory { get; init; }

    public bool SkipBios { get; init; } = true;
}
