# Jellyfin Plugin: NextUp Rewatch

[![Build and Test](https://github.com/Soviann/jellyfin-plugin-nextup-rewatch/actions/workflows/build.yaml/badge.svg)](https://github.com/Soviann/jellyfin-plugin-nextup-rewatch/actions/workflows/build.yaml)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Target Jellyfin ABI](https://img.shields.io/badge/Jellyfin-10.10.x-purple.svg)](https://github.com/jellyfin/jellyfin)

A server-side Jellyfin plugin that fixes the "Next Up" carousel behavior for rewatched series.

---

## 📌 The Problem

In Jellyfin, users can turn on the client-side setting **"Enable Rewatching in Next Up"** (`enableRewatching=true`) to keep track of series they are rewatching without needing to reset their overall watched history.

However, this feature contains a critical flaw ([Jellyfin Issue #10470](https://github.com/jellyfin/jellyfin-web/issues/10470)):
1. **Completed Series Stalling:** When you finish all episodes of a series (or finish a rewatch), Jellyfin has no subsequent episode to suggest. Instead of removing the series from "Next Up", it keeps the **final watched episode permanently stuck in "Next Up"** marked as "Watched" (green checkmark).
2. **Specials & Dead-ends (`S00`):** Watching special episodes / OVAs after finishing regular seasons causes the special to remain stuck indefinitely in "Next Up".
3. **Client-side setting mismatch:** Because this setting is client-specific (stored in browser/TV `localStorage`), TV clients like LG webOS or Samsung Tizen get cluttered with completed shows while web browsers without the toggle do not.

---

## 💡 How It Works

`NextUp Rewatch` runs seamlessly on your Jellyfin server by intercepting `/Shows/NextUp` API responses before they reach the client:

* **Unplayed Episodes (`Played == false`):** Always preserved intact (standard ongoing series behavior).
* **Played Episodes (`Played == true`):**
  * The plugin compares the episode's `LastPlayedDate` (`EpLastPlayed`) against the maximum `LastPlayedDate` across all episodes in that series (`SeriesMaxLastPlayed`).
  * **Active Rewatches (`EpLastPlayed < SeriesMaxLastPlayed`):** An episode was watched *more recently* in the series, proving that this item is legitimately the **next episode to watch** in an ongoing rewatch sequence $\rightarrow$ **Kept**.
  * **Stalled Completed Series (`EpLastPlayed >= SeriesMaxLastPlayed`):** This item is the episode that was *just finished*, and no further episodes remain to follow $\rightarrow$ **Filtered out**.

### Concrete Example
| Series | State | Returned Episode | `EpLastPlayed` | `SeriesMaxLastPlayed` | Decision |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Sherlock Holmes** | Active rewatch (just finished S01E07) | S02E01 | July 2026 | Yesterday | **KEPT** (Next to rewatch) |
| **Overlord** | Completed (watched movie S00E61) | S00E61 | Sept 2026 | Sept 2026 | **FILTERED OUT** (Finished) |
| **Columbo** | Completed (watched pilot S00E08) | S00E08 | Yesterday | Yesterday | **FILTERED OUT** (Finished) |

---

## 🚀 Installation

### Option 1: Via Jellyfin Plugin Repository (Recommended)

1. Open **Jellyfin Dashboard** $\rightarrow$ **Plugins** $\rightarrow$ **Repositories** tab.
2. Click the **➕ Add Repository** button.
3. Enter the repository details:
   - **Repository Name:** `NextUp Rewatch Repository` (or any custom name)
   - **Repository URL:**
     ```
     https://raw.githubusercontent.com/Soviann/jellyfin-plugin-nextup-rewatch/main/manifest.json
     ```
4. Click **Save**.
5. Switch to the **Catalog** tab.
6. Find **NextUp Rewatch** under the **General** category and click **Install**.
7. Restart your Jellyfin server / container when prompted or at your convenience.

---

### Option 2: Manual Installation (Docker / Host)

1. Download the latest release `NextUpRewatch_1.0.0.0.zip` (or build from source).
2. Create a folder in your Jellyfin `plugins` directory:
   ```bash
   mkdir -p /path/to/jellyfin/config/plugins/NextUpRewatch_1.0.0.0
   ```
3. Extract `Jellyfin.Plugin.NextUpRewatch.dll` and `meta.json` into this folder:
   ```bash
   cp Jellyfin.Plugin.NextUpRewatch.dll meta.json /path/to/jellyfin/config/plugins/NextUpRewatch_1.0.0.0/
   ```
4. Restart your Jellyfin server / container:
   ```bash
   docker restart jellyfin
   ```

---

## ⚙️ Configuration

Once installed, go to **Jellyfin Dashboard** $\rightarrow$ **Plugins** $\rightarrow$ **NextUp Rewatch**:

* **Enable Rewatch Filter:** Toggle the filter on or off (default: `Enabled`).
* **Enable Debug Logging:** Logs filtering decisions to Jellyfin server logs for inspection.

---

## 🛠️ Building & Testing

The project targets .NET 8.0 (`net8.0`) matching Jellyfin 10.10.x ABI.

### Build with Docker (Recommended)
```bash
docker run --rm -v $(pwd):/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 sh -c "dotnet build -c Release && dotnet test -c Release"
```

### Build with Local .NET SDK
```bash
dotnet build -c Release
dotnet test -c Release
```

### Validate Repository Manifest
A dedicated validator checks `manifest.json` against schema standards, cross-references GUID with `Plugin.cs`, and checks remote asset checksums:
```bash
python3 scripts/validate-manifest.py --check-remote
```
*(Also automatically enforced in GitHub Actions CI).*

---

## 📄 License

Licensed under the [Apache License, Version 2.0](LICENSE).
Copyright © 2026 Soviann.
