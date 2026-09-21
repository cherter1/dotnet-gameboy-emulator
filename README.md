# GBA Emulator

Clean, incremental Game Boy Advance emulator foundations in C#/.NET.

## Projects

- `src/GbaEmulator.Core`: emulator core, CPU/bus/video/input/timers/DMA/interrupts
- `src/GbaEmulator.App`: WPF desktop host for ROM discovery, keyboard input, and framebuffer presentation
- `tests/GbaEmulator.Core.Tests`: deterministic unit tests

## Usage

Place ROMs in `src/roms/`

```powershell
dotnet run --project src/GbaEmulator.App
```

To Run The Tests use

```powershell
dotnet test
```

## Current Status

This repo now includes:

- multi-project emulator solution structure
- ROM discovery and optional BIOS loading
- GBA bus and memory-map foundation
- ARM7TDMI CPU scaffold with a tested initial ARM/THUMB subset
- timer, DMA, interrupt, keypad, and framebuffer plumbing
- desktop framebuffer presentation at the native 240x160 resolution scaled in WPF

The architecture is set up for continued work toward broader GBA compatibility.
