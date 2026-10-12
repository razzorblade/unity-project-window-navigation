# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [1.1.0] - 2026-10-13

### Added

- Up button next to Forward. Opens the parent folder with the folder you came from selected and scrolled into view.
- Mouse side buttons (4 and 5) go Back and Forward while the pointer is over a Project window.
- Keyboard shortcuts Alt+Left, Alt+Right and Alt+Up, rebindable under Edit > Shortcuts > Backtrack. They act on the focused Project window and stay out of the way while a text field is being edited.
- Back and Forward restore the scroll position and the selection you had in that folder.
- Preferences: Mouse Side Buttons and Restore Selection toggles, plus a button that opens the Shortcut Manager.
- Button tooltips show the current shortcut binding.

### Fixed

- History restored after a domain reload is validated, so data saved by an older version can no longer cause errors.

## [1.0.0] - 2026-10-13

### Added

- Back and Forward buttons in the Project window toolbar, next to the Create (+) dropdown.
- Separate folder history for every open Project window.
- Right click either button to see the full history and jump to any folder in it.
- History survives domain reloads (script recompiles, entering Play Mode).
- Folders are tracked by GUID, so renamed or moved folders stay in the history; deleted folders are skipped.
- Preferences > Nyvion > Backtrack: number of folders remembered (1 to 100, default 10) and Clear History.

[1.1.0]: https://github.com/razzorblade/unity-project-window-navigation/releases/tag/v1.1.0
[1.0.0]: https://github.com/razzorblade/unity-project-window-navigation/releases/tag/v1.0.0
