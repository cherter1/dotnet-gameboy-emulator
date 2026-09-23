using System.Runtime.InteropServices;
using GbaEmulator.App.SDL;
using GbaEmulator.App.SDL.Native;

Console.WriteLine("Press any key to exit...");
var pixelPin = GCHandle.Alloc(Pix.PresentationPixels, GCHandleType.Pinned);

uint SDL_INIT_VIDEO = 0x00000020u;
Sdl.SDL_Init(SDL_INIT_VIDEO);

var window = Sdl.SDL_CreateWindow("Pixel Presentation Test", 240 * 3, 160 * 3, 0x20 | 0x100000); // resizeable, mouse_capture, keyboard_grabbed
var renderer = Sdl.SDL_CreateRenderer(window, null);
uint SDL_PIXELFORMAT_ARGB8888 = 0x16362004u;
uint SDL_PIXELFORMAT_RGBA8888 = 0x16462004u;
uint SDL_PIXELFORMAT_ARGB1555 = 0x15331002u;
var texture = Sdl.SDL_CreateTexture(renderer, SDL_PIXELFORMAT_ARGB8888, 2, 240, 160); //streaming access
Sdl.SDL_SetTextureScaleMode(texture, 2); //pixel art mode
Sdl.SDL_UpdateTexture(texture, nint.Zero, pixelPin.AddrOfPinnedObject(), 240 * 4);

Sdl.SDL_SetRenderLogicalPresentation(renderer, 240, 160, 2); //letterbox mode
Sdl.SDL_SetRenderDrawColor(renderer, 0, 128, 255, 255);
Sdl.SDL_RenderClear(renderer);

Sdl.SDL_RenderTexture(renderer, texture, nint.Zero, nint.Zero);
Sdl.SDL_RenderPresent(renderer);

bool running = true;
Console.WriteLine(AppContext.BaseDirectory + "roms");
var x = Path.Combine(AppContext.BaseDirectory, "roms");
Console.WriteLine(x);
ulong lastTime = 0;
uint frameCount = 0;
while (running)
{
    unsafe
    {
        SdlEvent sdlEvent;
        while (Sdl.SDL_PollEvent(&sdlEvent))
        {
            if (sdlEvent.Type == Sdl.EventQuit) //SDL_EventType
            {
                running = false;
            }
        }
    }
    //Console.WriteLine("presenting frame");
    Sdl.SDL_RenderClear(renderer);
    Sdl.SDL_RenderTexture(renderer, texture, nint.Zero, nint.Zero);
    Sdl.SDL_RenderPresent(renderer);
    frameCount++;

    ulong currentTime = Sdl.SDL_GetTicks();
    if (currentTime > lastTime + 1000)
    {
        Sdl.SDL_SetWindowTitle(window, $"FPS Test ({frameCount} fps)");
        //Console.WriteLine("set window title");
        frameCount = 0;
        lastTime = currentTime;
    }
}
Console.WriteLine("Terminating...");
//Sdl.SDL_Delay(2000);

Sdl.SDL_DestroyTexture(texture);
Sdl.SDL_DestroyRenderer(renderer);
Sdl.SDL_DestroyWindow(window);
Sdl.SDL_Quit();
return 0;
