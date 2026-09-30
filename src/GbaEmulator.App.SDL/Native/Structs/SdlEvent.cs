using System.Runtime.InteropServices;

namespace GbaEmulator.App.SDL.Native.Structs;

[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct SdlEvent
{
    [FieldOffset(0)]
    internal uint Type;
}