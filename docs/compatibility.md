# Compatibility notes

DynamicResolutionSwitcher switches an existing mode of the primary display. Mode availability is decided by Windows, the display driver, monitor, cable path, and any driver-level custom-resolution setup.

## Verified locally

| Item | Result |
| --- | --- |
| Resolution / refresh-rate round trip | 1920×1080 @ 165 Hz ↔ 1720×1080 @ 165 Hz |
| GPU | NVIDIA GeForce GTX 1050 Ti |

## Boundaries

- Windows 10/11 only; primary display only.
- The app does not create custom resolutions.
- AMD and Intel have not yet been independently validated.
- It does not set GPU scaling policy, game options, FPS, or anti-cheat settings.

## Report a setup

    Windows version:
    GPU and driver version:
    Monitor and connection:
    Modes shown by the app:
    Switch result:
