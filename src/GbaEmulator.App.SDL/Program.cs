using GbaEmulator.App.SDL.Hosting;
using GbaEmulator.App.SDL.Native;
using GbaEmulator.Core.Input;

const ulong NativeFrameTicks = 16_744_118; //NS
var machine = GbaInit.CreateMachine();

uint SDL_INIT_VIDEO = 0x00000020u;
Sdl.SDL_Init(SDL_INIT_VIDEO);

var window = Sdl.SDL_CreateWindow("Pixel Presentation Test", 240 * 3, 160 * 3, 0x20 | 0x100000); // resizeable, keyboard_grabbed
var renderer = Sdl.SDL_CreateRenderer(window, null);

uint SDL_PIXELFORMAT_ABGR1555 = 0x15731002u;
var texture = Sdl.SDL_CreateTexture(renderer, SDL_PIXELFORMAT_ABGR1555, 2, 240, 160); //streaming access
Sdl.SDL_SetTextureScaleMode(texture, 2); //pixel art mode

Sdl.SDL_SetRenderLogicalPresentation(renderer, 240, 160, 2); //letterbox mode
Sdl.SDL_SetRenderDrawColor(renderer, 0, 128, 255, 255);
Sdl.SDL_RenderClear(renderer);
Sdl.SDL_RenderPresent(renderer);

bool running = true;
ulong lastTime = 0;
ulong lastTicksNs = 0;
uint fpsCount = 0;
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
            else if (sdlEvent.Type == Sdl.EventKeyDown) //key down
            {
                var keySetDown = *(SdlKeyboardEvent*)&sdlEvent;
                if (TryMapKey(keySetDown.KeyCode, out GbaButton button))
                {
                    Console.WriteLine($"key down: {button}");
                    machine.Keypad.SetPressed(button, true);
                }
            }
            else if (sdlEvent.Type == Sdl.EventKeyUp) //key up
            {
                var keySetUp = *(SdlKeyboardEvent*)&sdlEvent;
                if (TryMapKey(keySetUp.KeyCode, out GbaButton button))
                {
                    Console.WriteLine($"key up: {button}");
                    machine.Keypad.SetPressed(button, false);
                }
            }
        }

        machine.RunFrame();

        fixed (ushort* pixels = machine.FrameBuffer.Pixels)
        {
            Sdl.SDL_UpdateTexture(texture, null, pixels, 240 * 2);
        }

        Sdl.SDL_RenderClear(renderer);
        Sdl.SDL_RenderTexture(renderer, texture, null, null);
        Sdl.SDL_RenderPresent(renderer);
    }

    fpsCount++;

    ulong currentTime = Sdl.SDL_GetTicks();
    if (currentTime > lastTime + 1000)
    {
        Sdl.SDL_SetWindowTitle(window, $"FPS Test ({fpsCount} fps)");
        fpsCount = 0;
        lastTime = currentTime;
    }

    ulong currentTicksNs = Sdl.SDL_GetTicksNS();
    ulong nextIntervalTicksNs = lastTicksNs + NativeFrameTicks;
    if (currentTicksNs < nextIntervalTicksNs)
    {
        Sdl.SDL_DelayNS(nextIntervalTicksNs - currentTicksNs);
    }
    lastTicksNs = Sdl.SDL_GetTicksNS();
}

Sdl.SDL_DestroyTexture(texture);
Sdl.SDL_DestroyRenderer(renderer);
Sdl.SDL_DestroyWindow(window);
Sdl.SDL_Quit();
return 0;

static bool TryMapKey(uint k, out GbaButton button)
{
    switch (k)
    {
        case 0x00000078u: //SDLK_X
            button = GbaButton.A;
            return true;
        case 0x0000007au: //SDLK_Z
            button = GbaButton.B;
            return true;
        case 0x00000061u: //SDLK_A
            button = GbaButton.L;
            return true;
        case 0x00000073u: //SDLK_S
            button = GbaButton.R;
            return true;
        case 0x0000000du: //SDLK_RETURN
        case 0x40000058u: //SDLK_KP_ENTER
            button = GbaButton.Start;
            return true;
        case 0x400000e1u: //SDLK_LSHIFT
        case 0x400000e5u: //SDLK_RSHIFT
            button = GbaButton.Select;
            return true;
        case 0x40000052u: //SDLK_UP
            button = GbaButton.Up;
            return true;
        case 0x40000051u: //SDLK_DOWN
            button = GbaButton.Down;
            return true;
        case 0x40000050u: //SDLK_LEFT
            button = GbaButton.Left;
            return true;
        case 0x4000004fu: //SDLK_RIGHT
            button = GbaButton.Right;
            return true;
        default:
            button = default;
            return false;
    }
}