using System.Runtime.InteropServices;

namespace GbaEmulator.App.SDL.Native;

[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct SdlKeyboardEvent
{
    [FieldOffset(28)]
    internal uint KeyCode;
}