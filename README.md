# Backtrack: Back and Forward buttons for the Unity Project window

[![Unity 6+](https://img.shields.io/badge/Unity-6000.0%2B-black?logo=unity)](https://unity.com/releases/editor/archive)
[![UPM](https://img.shields.io/badge/UPM-git%20package-blue)](#installation)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE.md)

**Back** and **Forward** navigation for the Unity Editor **Project window**, just like in a web browser. Open a folder, dive three levels deep, and click Back to return, the way a file explorer or web browser works. The buttons sit in the Project window's own toolbar, next to the Create (+) button, so they look like part of Unity.

![Back and Forward buttons in the Unity Project window toolbar](Documentation~/images/project-window.png)

## Features

- **Back / Forward buttons in the toolbar.** Native toolbar style, right next to the Create (+) dropdown. Each greys out when there is nowhere to go.
- **History dropdown.** Right click either button to see every remembered folder, newest first, and jump straight to one.
- **One history per Project window.** With two or more Project windows open, each keeps its own trail.
- **Survives domain reloads.** Recompiling scripts or entering Play Mode does not wipe the history.
- **Safe with renames.** Folders are remembered by GUID, so renamed or moved folders stay in the history. Deleted folders are skipped.
- **Configurable length.** Remembers 10 folders by default, adjustable from 1 to 100.
- **Zero footprint.** Lives in an editor assembly only. Nothing goes into your builds, scenes or assets, and there's nothing to set up.

![Toolbar closeup: Create, Back and Forward](Documentation~/images/toolbar-closeup.png)

## Installation

### Package Manager (git URL)

1. Open **Window > Package Manager**.
2. Click **+** > **Install package from git URL...**
3. Paste:

```
https://github.com/razzorblade/unity-project-window-navigation.git
```

To pin a specific version, add a release tag:

```
https://github.com/razzorblade/unity-project-window-navigation.git#v1.0.0
```

### manifest.json

Alternatively, add the dependency to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.nyvion.backtrack": "https://github.com/razzorblade/unity-project-window-navigation.git#v1.0.0"
  }
}
```

Git packages require [Git](https://git-scm.com/) on your PATH.

## Usage

| Action | Result |
| --- | --- |
| Open folders as usual (folder tree, double click, breadcrumbs, ping) | Each folder you open is added to the history |
| Click **Back** | Return to the previous folder |
| Click **Forward** | Go forward again after going back |
| Right click **Back** or **Forward** | Pick any folder from the history |
| Open a new folder after going back | Forward history is dropped, like in a browser |

Search results are not recorded, only folders you actually open.

### Multiple Project windows

Every Project window has its own independent history.

![Two Project windows, each with its own folder history](Documentation~/images/multiple-windows.png)

## Settings

**Edit > Preferences > Nyvion > Backtrack** (**Unity > Settings** on macOS)

| Setting | Default | Description |
| --- | --- | --- |
| Folders Remembered | 10 | How many folders Back can return to (1 to 100). |
| Clear History | | Forgets the history of every Project window. |

Settings are stored per user in EditorPrefs, so they never touch your project files or version control.

## Compatibility

- **Unity 6 (6000.0) or newer.** Developed and tested on Unity 6.3 LTS.
- Tested on the Windows editor. Nothing in the code is tied to a platform, so macOS and Linux should work too.
- Works with the Project window's **Two Column Layout**. In One Column Layout there is no "open folder", so the buttons hide.
- The buttons also hide while the Project window is very narrow (under about 340 px), where they would overlap the search field.

## How it works

Unity has no public API for the Project window toolbar, so Backtrack adds a small UI Toolkit element to each Project window's `rootVisualElement`. That layer draws above the window's own IMGUI toolbar. A lightweight poll, ten times a second, reads each window's open folder through cached delegates and records changes. History is kept in a `ScriptableSingleton`, which Unity carries across domain reloads.

Backtrack has to read a few internal members of Unity's `ProjectBrowser`. All of that access lives in a single class, `ProjectBrowserBridge`. If a future Unity version renames those members, Backtrack logs one warning and switches itself off instead of throwing errors.

## FAQ

**Doesn't Unity already have selection history?**
Unity's Previous / Next Selection (Ctrl+Shift+[ and ]) steps through every *selection* you made: assets, GameObjects and folders mixed together. Backtrack only tracks the folders you *open* in each Project window, which is what you want when browsing a large project.

**Does it persist after restarting Unity?**
No. History lasts for the editor session and survives domain reloads, but starts fresh when Unity restarts. The "Folders Remembered" setting is permanent.

**Does it change my project?**
No. It creates no assets and writes nothing to your project. Remove the package and nothing is left behind.

## Uninstall

Remove the package in **Window > Package Manager**, or delete the `com.nyvion.backtrack` line from `Packages/manifest.json`.

## License

[MIT](LICENSE.md) (c) Nyvion
