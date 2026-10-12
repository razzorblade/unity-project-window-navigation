# Changelog

All notable changes to this package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-10-13

### Added

- Back and Forward buttons in the Project window toolbar, next to the Create (+) dropdown.
- Separate folder history for every open Project window.
- Right click either button to see the full history and jump to any folder in it.
- History survives domain reloads (script recompiles, entering Play Mode).
- Folders are tracked by GUID, so renamed or moved folders stay in the history; deleted folders are skipped.
- Preferences > Nyvion > Backtrack: number of folders remembered (1 to 100, default 10) and Clear History.

[1.0.0]: https://github.com/razzorblade/unity-project-window-navigation/releases/tag/v1.0.0
