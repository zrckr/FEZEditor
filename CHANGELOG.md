# Changelog

## 2026.04 - 2026-04-11

The editor is major features complete!

#### Showcase

https://youtu.be/bksXuaovHVk

#### Features

- `EddyEditor`: Level editor
- `MuEditor`: NPC metadata
- `TexViewer`: Static and animated textures
- `LukeEditor`: Sky assets
- `RickViewer`: Sound effect preview with OGG and WAV playback
- GLTF diorama export for levels (available inside `EddyEditor`)
- Asset browser with thumbnail cache
- Persistent app state and recent files per provider
- Status bar with input hints
- Fog support for materials and Art Objects
- Hardware-instanced star field for `JadeEditor`
- View menu with File Browser toggle

#### Fixes

- Numerous EddyEditor fixes (camera, gizmo, trile deselection, context menus, scaling)
- File Browser improvements (context menu, path display, deselect on open)
- Ctrl key penalty for proper shortcut handling
- Rendering switched to DFS; pre-cached BasicEffect instances
- Fixed re-entrant DestroyActor crash, OGG stream leak, editor tab stability

## 2026.03 - 2026-03-05

First public release (**non-production ready!**).

#### Features

- Open PAK files and folders with extracted assets (XNB and FEZRepacker formats)
- Extract assets from PAK
- `ChrisEditor`: ArtObjects and TrileSets
- `DiezEditor`: Tracked Songs
- `JadeEditor`: World Map editor (live and interactive)
- `PoEditor`: Localization files
- `SallyEditor`: Save files (PC format only)
- `ZuEditor`: SpriteFonts

#### Supported platforms

- Windows x64
- Linux x64
- macOS ARM64
