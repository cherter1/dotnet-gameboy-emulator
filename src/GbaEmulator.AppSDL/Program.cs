using SDL3;

if (!SDL.InitSubSystem(SDL.InitFlags.Video))
{
    Console.Error.WriteLine($"SDL init failed: {SDL.GetError()}");
}
//240, 160
SDL.CreateWindow("SDL Testing Window", 480, 320, SDL.WindowFlags.Resizable);

SDL.Delay(4000);

SDL.Quit();
Environment.Exit(0);