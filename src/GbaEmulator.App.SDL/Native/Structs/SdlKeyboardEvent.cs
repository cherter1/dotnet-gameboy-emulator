using System.Runtime.InteropServices;

namespace GbaEmulator.App.SDL.Native.Structs;

[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct SdlKeyboardEvent
{
    [FieldOffset(28)]
    internal uint KeyCode;
}