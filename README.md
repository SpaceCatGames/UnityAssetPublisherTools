# UnityAssetPublisherTools

UnityAssetPublisherTools is a small set of Unity Editor utilities that helps you publish your asset as an embedded UPM package and keep the repository structure comfortable during development.

The toolkit focuses on a practical workflow:
- keep `Samples~` and `Documentation~` hidden or visible when you need it,
- build an embedded package under `Packages/<Package Root Folder>`,
- safely return everything back to the project layout,
- preserve stable GUIDs for sample assets so imported samples keep references,
- keep `package.json` fields and `PlayerSettings.bundleVersion` synchronized.

## Features

### Toggle Samples and Documentation folders
- Renames `Samples~` <-> `Samples` and `Documentation~` <-> `Documentation`.
- Uses a scripting define to keep state consistent (`SAMPLES_RENAMED`).
- Accessible via menu items under `SCG/`.

### Preserve stable GUIDs for Samples~
When sample assets are shipped via Package Manager import, missing `.meta` files can lead to GUID changes and broken references.

`SamplesMetaBaker`:
- forces Unity to generate `.meta` by temporarily importing assets into a temporary folder under `Assets/__SamplesMetaBake__`,
- copies generated `.meta` files back next to originals under `Samples~`,
- does not overwrite existing `.meta`.

### Build / Return embedded UPM package
`UpmPackageBuilder` automates switching between two layouts.

Build for UPM (non-UPM mode):
- ensures `Samples~` and `Documentation~` are hidden before build,
- moves the entire base folder (with the root `.meta`) into `Packages/<Package Root Folder>`,
- ensures the effective `package.json` exists in the embedded package root,
- bakes `.meta` for Samples~ assets,
- adds the `UPM_PACKAGE` define and triggers required refresh/reimport steps.

Return back to project (UPM mode):
- removes the dependency from `Packages/manifest.json`,
- moves the folder back to the original location (with `.meta`),
- reverts `Samples~` state if it was toggled by the tool,
- removes the `UPM_PACKAGE` define.

### package.json utilities
`PackageJsonUtility`:
- reads and writes `name`, `version`, `displayName`, and `description` in `package.json`,
- supports both TextAsset-based and file path based workflows.

### Settings asset
`AssetPublisherToolsSettings`:
- central editor configuration (expected to be located in a `Resources` folder),
- stores links to `Base Folder` and `Package Asset` (as `TextAsset`),
- can sync `PlayerSettings.bundleVersion` and selected `package.json` fields in non-UPM layout.

### Define symbol management
`DefineSymbolsManager`:
- add/remove/check scripting define symbols,
- supports Unity 2021.2+ via `NamedBuildTarget` and falls back to legacy APIs,
- best-effort applies defines for Android and iOS too.

## Installation (UPM)

Add via Git URL in Package Manager (or `manifest.json`):
https://github.com/SpaceCatGames/UnityAssetPublisherTools.git?path=Assets/SCG

## Setup

1. Create an `AssetPublisherToolsSettings` asset via:
   - `Assets -> Create -> SCG -> AssetPublisherToolsSettings`
2. Put it into a `Resources` folder (so `AssetPublisherToolsSettings.Instance` can load it).
3. Configure the key fields:
   - `Asset Root Folder`: folder name under `Assets/` used in project mode.
   - `Package Root Folder`: folder name under `Packages/` used in UPM mode.
   - `Base Folder`: folder asset representing the package root.
   - `Package Asset`: `package.json` as a `TextAsset`.
4. (Optional) Configure `Package Version`, `Package Id`, `Package Display Name`, `Package Description`.

(Optional) To avoid including the settings asset in player builds, place the `Resources` folder under an editor-only path, for example:
- `Assets/Editor/Resources/`

Notes on references:
- After switching between project and UPM layouts, `Base Folder` can temporarily show as `Missing (Object)`.
  This is expected because the underlying folder asset is physically moved.
  The settings asset will re-resolve references on the next editor refresh.

## Usage (Menu)

All menu items live under `SCG/`:

- Show Samples and Documentation folders / Hide Samples and Documentation folders
  Toggles `Samples~` and `Documentation~` visibility by renaming folders.

- Build for UPM Package
  Builds an embedded package and switches the project into UPM mode.

- Return from UPM Package (to project)
  Restores the original project layout from the embedded package.

- Set UNITY_ASTOOLS_EXPERIMENTAL Define
  Adds an optional experimental define for local workflows.

## Notes

- This toolkit is editor-only by design.
- Folder moves can fail if files are locked by external tools (File Explorer windows, IDEs/code editors, VCS clients, antivirus scanners, and any external processes touching the folder).
- `SamplesMetaBaker` uses a temporary folder under `Assets/` and cleans it up after work.

## Known warning during Return

When returning from UPM layout back to the project layout, Unity can print a warning like:

`Couldn't create '<Project>/Packages/<...>/~UnityDirMonSyncFile~...~'`

This comes from Unity's Directory Monitoring feature and usually does not affect the move result.
If you want to suppress it, you can disable Directory Monitoring in Unity Preferences (Edit -> Preferences -> Asset Pipeline -> Directory Monitoring) before running Return.

Unity can also print warnings when `Documentation~` and `Samples~` are being renamed or moved.
These warnings are expected and indicate Unity reimport/refresh work for those special folders.
They do not affect the result, and there is no reliable way to fully suppress them other than avoiding the toggle/move itself.

## License

MIT License

Copyright © 2025 Space Cat Games

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
