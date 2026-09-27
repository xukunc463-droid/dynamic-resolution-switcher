# DynamicResolutionSwitcher

A portable Windows tool for switching the primary display's existing resolution and refresh-rate modes. [简体中文](README.md) · [Download](https://github.com/xukunc463-droid/dynamic-resolution-switcher/releases/latest)

![DynamicResolutionSwitcher UI](docs/screenshot.png)

It lists modes already exposed by Windows and your display driver, then lets you switch them from a small desktop app. Single EXE, no install, administrator permission, or network access.

## Features

- Enumerates resolution and refresh-rate modes on the Windows primary display
- Highlights the current mode; switch with double-click, Enter, or the button
- Refresh with F5 and tests a mode with the Windows display API before applying it
- Does not create or alter custom resolutions

## Valorant and stretched output

In the author's personal Valorant environment, real stretched output was observed after combining an existing custom resolution with NVIDIA scaling and in-game display settings. This app only switches an existing Windows primary-display mode; it does not change GPU scaling policy, game settings, FPS, or anti-cheat configuration. Results vary by setup.

## Build

    powershell -ExecutionPolicy Bypass -File .\build.ps1

[MIT](LICENSE)
