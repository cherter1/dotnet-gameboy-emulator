namespace GbaEmulator.App.SDL.Native.Enums;

internal static class Constants
{
    internal const uint SDL_INIT_VIDEO = 0x00000020u;
    internal const ulong SDL_WINDOW_RESIZABLE = 0x20;
    internal const ulong SDL_WINDOW_KEYBOARD_GRABBED = 0x100000;
    internal const uint SDL_PIXELFORMAT_ABGR1555 = 0x15731002u;
    internal const int SDL_TEXTUREACCESS_STREAMING = 2;
    internal const int SDL_SCALEMODE_PIXELART = 2;
    internal const int SDL_LOGICAL_PRESENTATION_LETTERBOX = 2;
    internal const uint SDL_EVENT_QUIT = 0x100;
    internal const uint SDL_EVENT_KEY_DOWN = 0x300;
    internal const uint SDL_EVENT_KEY_UP = 0x301;

    internal const uint SDLK_X = 0x00000078u;
    internal const uint SDLK_Z = 0x0000007au;
    internal const uint SDLK_A = 0x00000061u;
    internal const uint SDLK_S = 0x00000073u;
    internal const uint SDLK_RETURN = 0x0000000du;
    internal const uint SDLK_KP_ENTER = 0x40000058u;
    internal const uint SDLK_LSHIFT = 0x400000e1u;
    internal const uint SDLK_RSHIFT = 0x400000e5u;
    internal const uint SDLK_UP = 0x40000052u;
    internal const uint SDLK_DOWN = 0x40000051u;
    internal const uint SDLK_LEFT = 0x40000050u;
    internal const uint SDLK_RIGHT = 0x4000004fu;
}