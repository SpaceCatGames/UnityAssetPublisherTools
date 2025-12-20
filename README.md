# UnityAssetPublisherTools

UnityAssetPublisherTools is a small set of Unity Editor utilities that helps you publish your asset as a UPM package and keep the repository structure comfortable during development.

The toolkit focuses on a practical workflow:
- keep `Samples~` and `Documentation~` hidden or visible when you need it,
- build an embedded package under `Packages/`,
- safely return everything back to the project layout,
- preserve stable GUIDs for sample assets so imported samples keep references,
- keep `package.json` version in sync when working in the project layout.

## Features

### Toggle Samples and Documentation folders
- Renames `Samples~` <-> `Samples` and `Documentation~` <-> `Documentation`.
- Uses a scripting define to keep state consistent (`SAMPLES_RENAMED`).
- Accessible via menu items under `SCG/`.

### Preserve stable GUIDs for Samples~
When sample assets are shipped via Package Manager import, missing `.meta` files can lead to GUID changes and broken references.  
SamplesMetaBaker:
- forces Unity to generate `.meta` by temporarily importing assets into `Assets/__SamplesMetaBake__`,
- copies generated `.meta` files back next to originals under `Samples~`,
- does not overwrite existing `.meta`.

### Build / Return embedded UPM package
UpmPackageBuilder automates switching between two layouts:

**Build for UPM (non-UPM mode):**
- ensures `Samples~` are hidden before build,
- moves the entire base folder (with root `.meta`) to `Packages/SCG`,
- ensures the effective `package.json` exists in the embedded package root,
- bakes `.meta` for Samples~ assets,
- adds the `UPM_PACKAGE` define and triggers required refresh/reimport steps.

**Return back to project (UPM mode):**
- removes the local package (or skips PM remove for embedded packages),
- restores swapped `package.json` if it was backed up,
- moves the folder back to the original location (with `.meta`),
- reverts `Samples~` state if it was toggled by the tool,
- removes the `UPM_PACKAGE` define.

### package.json helpers
PackageVersionParser:
- reads `version` and `name` from `package.json`,
- updates `version` in a writable `package.json` (embedded/local package scenario).

EditorResourceObject:
- central editor configuration (expected to be located in a `Resources` folder),
- stores links to BaseFolder and package.json (as TextAsset),
- can sync `PlayerSettings.bundleVersion` and the json version in non-UPM layout.

### Define symbol management
DefineSymbolsManager:
- add/remove/check scripting define symbols,
- supports Unity 2021.2+ via NamedBuildTarget and falls back to legacy APIs,
- best-effort applies defines for Android and iOS too.

## Installation (UPM)

Add via Git URL in Package Manager (or `manifest.json`):
https://github.com/SpaceCatGames/UnityAssetPublisherTools.git?path=Assets/SCG

## Setup

1. Create an `EditorResourceObject` asset via:
   - `Assets -> Create -> SCG -> EditorResourceObject`
2. Put it into a `Resources` folder (so `EditorResourceObject.Instance` can load it).
3. Assign:
   - **BaseFolder**: the folder asset that represents your package root in the project layout,
   - **PackageAsset**: a TextAsset reference to your effective `package.json`.
4. (Optional) Configure:
   - **AssetFolderName** and **PackageName** depending on your layout and workflow needs.

## Usage (Menu)

All menu items live under `SCG/`:

- **Show Samples and Documentation folders** / **Hide Samples and Documentation folders**  
  Toggles `Samples~` and `Documentation~` visibility by renaming folders.

- **Build for UPM Package**  
  Builds an embedded package and switches the project into UPM mode.

- **Return from UPM Package (to project)**  
  Restores the original project layout from the embedded package.

- **Set UNITY_ASTOOLS_EXPERIMENTAL Define**  
  Adds an optional experimental define for local workflows.

## Notes

- This toolkit is editor-only by design.
- Folder moves can fail if files are locked by external tools (IDE, antivirus, etc.).
- SamplesMetaBaker uses a temporary folder under `Assets/` and cleans it up after work.

## License

MIT License

Copyright (c) 2025 SpaceCatGames

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
