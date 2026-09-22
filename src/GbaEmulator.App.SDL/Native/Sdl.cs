using System.Runtime.InteropServices;

namespace GbaEmulator.App.SDL.Native;

internal static unsafe partial class Sdl
{
    private const string LibraryName = "SDL3";

    internal const uint InitVideo = 0x00000020;
    internal const int TextureAccessStreaming = 1;
    internal const int ScaleModeNearest = 0;
    internal const int LogicalPresentationIntegerScale = 4;

    internal const uint PixelFormatArgb8888 = 0x16362004;
    internal const uint EventQuit = 0x100;

    #region SDL_init.h

    [LibraryImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal static partial bool SDL_Init(uint flags);

    [LibraryImport(LibraryName)]
    internal static partial void SDL_Quit();

    #endregion

    #region SDL_error.h

    [LibraryImport(LibraryName)]
    internal static partial nint SDL_GetError();

    #endregion

    #region SDL_video.h

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint SDL_CreateWindow(string title, int width, int height, ulong flags); //SDL_WindowFlags

    [LibraryImport(LibraryName)]
    internal static partial void SDL_DestroyWindow(nint window);

    #endregion

    #region SDL_render.h

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void SDL_CreateRenderer(nint window, string? name);

    [LibraryImport(LibraryName)]
    internal static partial void SDL_DestroyRenderer(nint renderer);

    [LibraryImport(LibraryName)]
    internal static partial nint SDL_CreateTexture(nint renderer, uint format, int access, int w, int h);

    #endregion

    #region SDL_timer.h

    [LibraryImport(LibraryName)]
    internal static partial void SDL_Delay(uint ms);

    [LibraryImport(LibraryName)]
    internal static partial void SDL_DelayNS(ulong ns);

    [LibraryImport(LibraryName)]
    internal static partial ulong SDL_GetTicks(); //ms elapsed

    [LibraryImport(LibraryName)]
    internal static partial ulong SDL_GetTicksNS(); //ns elapsed

    #endregion
}