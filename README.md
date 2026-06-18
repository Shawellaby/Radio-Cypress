# Radio Cypress

**Radio Cypress** is a Windows desktop internet radio player built with **WPF** and **C#**. It provides a compact keyboard-driven radio interface with editable station presets, audio recording, mute control, and real-time audio visualizations.

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![Framework](https://img.shields.io/badge/.NET-net10.0--windows-purple)
![UI](https://img.shields.io/badge/UI-WPF-5C2D91)
![License](https://img.shields.io/badge/license-GPLv3-green)

---

<p align="center">
  <img src="RepoAssets\RadioCypress_readmeimage_01.png" alt="Radio Cypress screenshot" width="650">
</p>

---

## Features

- Stream internet radio stations.
- Store station presets locally per user/machine.
- Edit station presets from within the app.
- Supports up to **9 station presets**, numbered **1 through 9**.
- Keyboard-driven controls.
- Real-time audio visualizations.
- MP3 recording support.
- Mute/unmute support.
- Station selection dialog.
- Built-in help window.

---

## Screens and Dialogs

Radio Cypress includes several focused WPF windows:

| Window | Purpose |
|---|---|
| Main Window | Plays the current stream and displays visualizations/status indicators |
| Station Selection | Allows selecting one of the configured station presets |
| Station Editor | Allows adding, editing, deleting, and saving local station presets |
| Help Window | Displays keyboard shortcuts and station listings |

---

## Station Presets

Station presets are stored locally for each Windows user.

The station list is saved as JSON under:
```text
%LOCALAPPDATA%\RadioCypress\stations.json
```

Example location:
```text
C:\Users<username>\AppData\Local\RadioCypress\stations.json
```
The file is created automatically on first run using the application’s default station list.

### Station Rules

- Station numbers must be between `1` and `9`.
- Station numbers must be unique.
- Each station must have a name.
- Each station must have a valid `http` or `https` stream URL.
- Invalid station numbers are ignored when loading from disk.

Example `stations.json`:
```json
json [{"Number":1,"Name":"Cypress Radio","Url":"https://CypressRadio.org:8000/stream"},{"Number":2,"Name":"Big 80's","Url":"https://ssl.nexuscast.com:9044/;"}]
```

---
 

## Keyboard Shortcuts

| Key | Action |
|---|---|
| `H` | Show help window |
| `Enter` | Open station selection window |
| `Ctrl + E` | Edit station presets |
| `1` - `9` | Switch to station preset |
| `Numpad 1` - `Numpad 9` | Switch to station preset |
| `M` | Mute / unmute |
| `R` | Start / stop recording |
| `Q` | Quit application |


---
## Station Preset Editing (CTRL-E key)
<img src="RepoAssets\RadioCypress_readmeimage_0c.png" alt="Radio Cypress screenshot" width="600">

---

## Station Selection (ENTER key)
<img src="RepoAssets\RadioCypress_readmeimage_0a.png" alt="Radio Cypress screenshot" width="400">

---

## HELP (H key)
<img src="RepoAssets\RadioCypress_readmeimage_0b.png" alt="Radio Cypress screenshot" width="400">

---

## Visualization Shortcuts

| Key | Visualization |
|---|---|
| `E` | Equalizer |
| `P` | Psychedelic |
| `W` | Wave |
| `L` | LED Matrix / WOPR |
| `T` | Ethereal |
| `O` | Oscilloscope |

---

## Recording

Radio Cypress can record the currently playing stream to an MP3 file.

To record:

1. Press `R`.
2. Choose the output location.
3. Recording begins.
4. Press `R` again to stop recording.

Recorded filenames are generated from the station name and timestamp, for example:
```text
Cypress Radio_20260612_153000.mp3
```

---

## Technologies Used

Radio Cypress is built with:

- **C#**
- **WPF**
- **.NET for Windows**
- **CSCore**
- **NAudio**
- **NAudio.Lame**

Primary audio-related responsibilities include:

- Stream decoding.
- Audio playback.
- Audio sample processing.
- FFT-based visualization.
- MP3 recording.

---

## Requirements

### Runtime

- Windows
- .NET Desktop Runtime compatible with the target framework

### Development

- Visual Studio or JetBrains Rider
- .NET SDK compatible with the project target framework
- Windows desktop development workload

---

## Build

From the project root: 
```powershell
 dotnet build
```

For release builds: 
```powershell
dotnet build -c Release
```

---

## Run

From the project root: 
```powershell
dotnet run
```

---

## Publish

Example publish command: 
```powershell
dotnet publish -c Release
```

If using a publish profile, publish from your IDE or with: 
```powershell
 dotnet publish -c Release
```

The project is intended to be published as a Windows desktop application.

---

## Local Data

Radio Cypress does not require registry storage for station configuration.

User-editable data is stored under:
```text
 %LOCALAPPDATA%\RadioCypress
```

Currently stored local data:

| File | Purpose |
|---|---|
| `stations.json` | User-editable station presets |

To reset stations back to defaults, close the app and delete:\
```text
%LOCALAPPDATA%\RadioCypress\stations.json
```

The file will be recreated on the next launch.

---

## Default Stations

Radio Cypress ships with default station presets so the app works immediately on first launch.

Users can replace or edit these presets using the built-in station editor.

Only preset slots `1` through `9` are supported.

---

## Creating a New Visualization

Radio Cypress supports multiple audio visualizations through a small visualizer abstraction. Each visualization is responsible for drawing its own graphics onto the main visualization canvas while using the shared audio spectrum data provided by the application.

At a high level, adding a new visualization involves creating a new visualizer class, registering it with the main window, and assigning it to a keyboard shortcut.

### Visualization Architecture

Visualizations are located in the `Visualizations` area of the project.

Each visualizer implements the shared visualizer contract and receives two important objects:

| Object | Purpose |
|---|---|
| `Canvas` | The WPF drawing surface used to render the visualization |
| `SpectrumVisualizationContext` | Shared audio/FFT data and helper values used by visualizers |

The visualizer is called repeatedly by a timer while audio is playing. On each update, the active visualizer redraws the canvas using the current audio data.

### General Process

To add a new visualization:

1. Create a new visualizer class in the `Visualizations` folder.
2. Implement the project’s visualizer interface.
3. Use the provided canvas to draw shapes, text, colors, animations, or other WPF elements.
4. Use the shared visualization context if the effect should respond to audio.
5. Add a new entry to the visualization mode list.
6. Register the new visualizer during visualization initialization.
7. Add a keyboard shortcut so users can activate it.
8. Update the Help window and README shortcut list.

### Drawing Model

Visualizers generally redraw the full canvas each frame.

A typical visualization will:

- Clear the canvas.
- Read the current canvas width and height.
- Exit early if the canvas has no usable size.
- Read recent audio samples or FFT data from the visualization context.
- Convert the audio data into a visual intensity, position, color, or animation value.
- Add WPF elements such as rectangles, ellipses, lines, polygons, or text.

Because the canvas is redrawn frequently, visualizers should avoid unnecessary expensive work where possible.

### Audio-Reactive Visuals

A visualization can be purely decorative, or it can react to the current audio stream.

Audio-reactive visualizers can use:

- Raw sample data.
- FFT data.
- Frequency buckets.
- Smoothed gain values.
- Canvas dimensions.
- Previous visualization state, if the visualizer stores its own state.

Frequency-based visualizations are useful for equalizers, spectrum bars, waveform effects, pulsing lights, and other music-responsive animations.

### Registration

After a visualizer class is created, it must be registered with the application so it can be selected at runtime.

Radio Cypress keeps a collection of available visualizers and chooses the active one based on the current visualization mode. A new visualization should be added to that collection during visualization initialization.

The visualization mode should also be added to the mode list used by the main window.

### Keyboard Shortcut

Each visualization is selected with a keyboard shortcut. When adding a new visualization, choose a key that does not conflict with existing commands such as station selection, mute, recording, help, or station editing.

After adding the shortcut, update the Help window so users can discover it.

### Design Guidelines

When creating a new visualization, try to keep it:

- Responsive to window resizing.
- Safe when no audio data is available.
- Efficient enough to redraw many times per second.
- Visually distinct from the existing modes.
- Consistent with the dark, neon-style Radio Cypress interface.

### Recommended Visualizer Ideas

Possible visualization styles include:

<table>
  <thead>
    <tr>
      <th style="text-align: left; padding: 8px; border-bottom: 2px solid #ddd;">Suggested Key</th>
      <th style="text-align: left; padding: 8px; border-bottom: 2px solid #ddd;">Title</th>
      <th style="text-align: left; padding: 8px; border-bottom: 2px solid #ddd;">Features</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">C</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">Circular Spectrum Ring</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">
        A radial audio spectrum where frequency buckets form bars around a circle.<br>
       <b>How it reacts to music:</b><br>
        - Bass expands the whole ring outward.<br>
        - Mid frequencies control bar length.<br>
        - Treble adds sparkles or small orbiting dots.<br>
        - Color hue rotates slowly with the beat.<br>
      </td>
    </tr>
    <tr>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">G</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">Neon Pulse Grid</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">
        A retro synthwave-style perspective grid that pulses with the audio.<br>
        <b>How it reacts to music:</b><br>
        - Bass makes the grid “jump” or bend.<br>
        - Energy controls grid brightness.<br>
        - Treble creates small lightning flickers along grid lines.<br>
        - The horizon glow grows with louder sections.<br>
      </td>
    </tr>
    <tr>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">F</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">Fire / Flame Spectrum</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">
        A flame-like visualization where the bottom of the canvas burns in response to frequency energy.<br>
       <b>How it reacts to music:</b><br>
        - Bass controls flame height.<br>
        - Mids create flame waves.<br>
        - Treble creates sparks rising upward.<br>
        - Louder audio shifts color from red/orange to yellow/white.<br>
      </td>
    </tr>
    <tr>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">Y or Z</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">Waterfall Spectrogram</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">
        A scrolling spectrogram where each frame of frequency data becomes a row or column of color.<br>
       <b>How it reacts to music:</b><br>
        - Frequency buckets are drawn as colored pixels/rectangles.<br>
        - New audio enters at one edge.<br>
        - Older frames scroll away.<br>
        - Bass/mid/treble appear as different color bands.<br>
        - More “scientific” and visually distinct from the existing equalizer.<br>
      </td>
    </tr>
    <tr>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">D or A</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">Radar Sweep</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">
        A green phosphor radar display with blips that react to music.<br>
        <b>How it reacts to music:</b><br>
        - A rotating sweep arm moves around the center.<br>
        - Bass creates large blips.<br>
        - Treble creates small bright blips.<br>
        - Strong beats create expanding rings.<br>
        - Older blips fade out slowly.<br>
      </td>
    </tr>
    <tr>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">B or J</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">Vinyl Record / Turntable</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">
        A spinning record with grooves that pulse with the music.<br>
       <b>How it reacts to music:</b><br>
        - Rotation speed subtly changes with energy.<br>
        - Bass makes the record pulse outward.<br>
        - Frequency levels draw glowing groove rings.<br>
        - Treble creates small needle sparks.<br>
      </td>
    </tr>
    <tr>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">K</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">City Skyline</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">
        A neon city skyline where building heights react to frequency buckets.<br>
       <b>How it reacts to music:</b><br>
        - Each building maps to a frequency range.<br>
        - Windows light up based on energy.<br>
        - Bass makes the whole skyline glow.<br>
        - Treble creates stars or aircraft lights.<br>
        - Fun variation on a spectrum analyzer without looking like a standard equalizer.<br>
      </td>
    </tr>
    <tr>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">A</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">Aurora Borealis</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">
        Flowing colored ribbons across the screen.<br>
       <b>How it reacts to music:</b><br>
        - Bass controls ribbon height.<br>
        - Mid frequencies control wave deformation.<br>
        - Treble adds shimmering particles.<br>
        - Overall volume controls brightness.<br>
        - Complements Ethereal, but with a more sky-like northern-lights identity.<br>
      </td>
    </tr>
    <tr>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">B or J</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">Plasma Ball</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">
        A central glowing orb with lightning bolts that extend outward based on audio.<br>
       <b>How it reacts to music:</b><br>
        - Bass expands the orb.<br>
        - Mids control bolt length.<br>
        - Treble controls branching bolts.<br>
        - Loud hits flash the center.<br>
      </td>
    </tr>
    <tr>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">C</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">Comet Trails</td>
      <td style="padding: 8px; border-bottom: 1px solid #ddd;">
        Frequency buckets become comets orbiting or flying across the display.<br>
       <b>How it reacts to music:</b><br>
        - Each comet maps to a band of frequencies.<br>
        - Bass comets are large and slow.<br>
        - Treble comets are small and fast.<br>
        - Loud hits leave longer trails.<br>
      </td>
    </tr>
  </tbody>
</table>
<br><br>
Currently Included Visualizers:

<table border=0>
  <tr>
    <td><img src="RepoAssets\RadioCypress_readmeimage_08.png" alt="Equalizer Spectrum" width="500"></td>
    <td><img src="RepoAssets\RadioCypress_readmeimage_02.png" alt="Ethereal Spectrum" width="500"></td>
  </tr>
  <tr>
    <td><img src="RepoAssets\RadioCypress_readmeimage_03.png" alt="Psychedelic Spectrum" width="500"></td>
    <td><img src="RepoAssets\RadioCypress_readmeimage_04.png" alt="Oscilloscope Waveform" width="500"></td>
  </tr>
  <tr>
    <td><img src="RepoAssets\RadioCypress_readmeimage_05.png" alt="Led Matrix Spectrum" width="500"></td>
    <td><img src="RepoAssets\RadioCypress_readmeimage_06.png" alt="Wave Spectrum" width="500"></td>
  </tr>
  <tr>
    <td><img src="RepoAssets\RadioCypress_readmeimage_07.png" alt="Vu Meter" width="500"></td>
    <td><img src="RepoAssets\RadioCypress_readmeimage_09.png" alt="Starfield Spectrum" width="500"></td>
  </tr>
  <tr>
    <td><img src="RepoAssets\RadioCypress_readmeimage_10.png" alt="Matrix Rain Spectrum" width="500"></td>
    <td><img src="RepoAssets\RadioCypress_readmeimage_11.png" alt="Lissajous Scope" width="500"></td>

  </tr>
</table>

---

## Project Structure

Typical source areas include: 

```text
RadioCypress/
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── HelpWindow.xaml
├── HelpWindow.xaml.cs
├── StationSelectionWindow.xaml
├── StationSelectionWindow.xaml.cs
├── StationEditorWindow.xaml
├── StationEditorWindow.xaml.cs
├── StationStore.cs
└── Visualizations/
    ├── IVisualizer.cs
    ├── SpectrumVisualizationContext.cs
    └── ...
```


---

## License

Radio Cypress is licensed under the **GNU General Public License v3.0 or later**.

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or, at your option, any later version.

This program is distributed in the hope that it will be useful, but **without any warranty**, including the implied warranties of merchantability or fitness for a particular purpose.

See: LICENSE
or visit: https://www.gnu.org/licenses/

---

## Copyright

© 2022 Shawellaby Software LLC. All Rights Reserved.

Project repository: https://github.com/Shawellaby

---

## Notes

Radio stream availability depends on the third-party stations and streaming endpoints configured by the user. If a station does not play, verify that the URL is still active and accessible.
