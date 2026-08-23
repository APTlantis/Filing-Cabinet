# Filing Cabinet User Guide

![.NET Version](https://img.shields.io/badge/.NET-10.0-blue.svg)

Filing Cabinet is a local-first desktop vault for digital artifacts you want to keep, find, verify, and recover later. It is not meant to replace your normal folders. Think of it as a deliberate retention space for files that deserve more structure than "somewhere in Downloads" but do not fit neatly into one project folder.

Good candidates include installers, disk images, manifests, configuration files, keys, screenshots, datasets, archives, torrents, generated assets, recovery documents, and research artifacts.

## Current Release

Current version: **Filing Cabinet v0.1.2**.

The v0.1.2 Store MSIX candidate keeps the stable vault workflow from `0.1.1` and brings the active Windows package evidence into current DRS alignment. It publishes the MIT License, records Release Hasher manifests, and establishes the Windows MSIX -> Microsoft Store path as the forward public distribution route. The active Windows package version is `0.1.2.0`.

Current Windows package evidence:

- Reserved Store product name: `Filing Cabinet`
- Store identity: `Aptlantis.FilingCabinet`
- Store publisher: `CN=81D6747D-F84F-4EFF-ACAA-9635D91ACCD0`
- Store ID: `9N29X9KR70R3`
- Store-accepted MSIX candidate: `trust/Aptlantis.FilingCabinet_0.1.2.0_x64.msix`
- Package version and architecture: `0.1.2.0`, X64
- Device family: Windows.Desktop, min version `10.0.18362.0`
- Language: `en-us`
- Capabilities: `runFullTrust`, `Microsoft.storeFilter.core.notSupported_8wekyb3d8bbwe`
- Store-reported file size: `9.4 MB`
- ARHS hash manifest: `trust/FileCabinet-0.1.2.0.hashmanifest.toml`
- Detached manifest signatures: `trust/FileCabinet-0.1.2.0.hashmanifest.toml.asc` and `trust/FileCabinet-0.1.2.0.hashmanifest.toml.sphincs`
- Public Windows signing authority: Microsoft Store signs the distributed package after certification/publication
- Historical/local MSI evidence: `artifacts/installer/FilingCabinet-0.1.1.0-win-x64.msi`

`Package.appxmanifest` follows the accepted Store identity/display pattern, and `Aptlantis.FilingCabinet_0.1.2.0_x64.msix` has been accepted by Partner Center package validation. The detached PGP and SLH-DSA files sign the ARHS hash manifest for preservation/provenance evidence; they do not replace Microsoft Store signing for the public MSIX distribution.

This release keeps the mature vault workflow: Vault Health has its own workspace, default analysis is metadata-first, large-file hash reads stay explicit, and bulk repair selection remains available for large repair lists. Current builds also combine Preview and Relations in the right panel, group hash settings by purpose, and start new catalogs with SHA-256 only unless the operator opts into additional hashes.

Release notes:

- `docs/Filing Cabinet v0.1.2 - Store MSIX Candidate.md`
- `docs/Filing Cabinet - Release Checklist.md`
- `docs/Filing Cabinet - Installer Hash Manifest.md`

License: MIT. See `LICENSE` and `docs/Filing Cabinet - License.md`.

## Who Filing Cabinet Is For

Filing Cabinet is for people who are willing to do a little more work up front so important files are easier to trust, find, and recover later.

It will usually take more intention than leaving a file in Downloads. You may choose an intake mode, review metadata, add a reason, adjust tags, or mark trust and retention priority. Filing Cabinet should keep making that process easier, faster, and more guided, but the basic tradeoff is intentional: the vault asks for a small amount of context while the artifact is still fresh so your future self is not left guessing.

The effort scales. Even rudimentary metadata gives you more long-term context than the filesystem provides by itself. The more of Filing Cabinet's fields and workflows you use, the richer the future result becomes, and the harder it is for important artifacts to become anonymous or lost over time.

That makes Filing Cabinet a better fit for high-signal artifacts than for every casual file. The reward is a vault that can explain what something is, where it came from, why it mattered, whether it changed, and how to recover it when the original context is gone.

## The Core Idea

Filing Cabinet stores important files inside a selected vault folder and keeps a catalog of what each file is, where it came from, how it was classified, what hashes identify it, and what searchable text was extracted from it.

The app is local-first. Vault files live on your machine, under the vault path you choose. The lightweight application catalog lives in AppData, and the vault itself contains portable subfolders for retained items, exports, quarantine, thumbnails, and extracted text.

Typical vault layout:

```text
K:\Filing Cabinet\
  items\
  catalog\
  quarantine\
  exports\
  thumbnails\
  extracted-text\
```

## Interface And Theme

Filing Cabinet uses the **Blue Slate** dark theme. The theme is intentionally semantic rather than decorative:

- deep blue-black surfaces carry the shell, sidebars, tables, and inspectors
- cyan and teal identify focus, primary actions, active navigation, and technical data surfaces
- amber and brass identify warnings, priority, preflight notes, and attention states
- violet and indigo identify taxonomy, archive, vault, and classification states
- green identifies healthy, indexed, verified, and completed states
- local danger and build extensions identify quarantine/destructive flows and large/package-like artifacts

The goal is fast scanning in dense vault views: bright color should tell you what kind of state you are looking at before it simply looks bright.

## Vaults

The vault list in the left sidebar shows the available vault entries. A vault entry points to a folder such as `K:\` or `K:\Filing Cabinet`.

Use the folder button beside **VAULTS** to set the selected vault folder. If a stale vault points to a drive you do not have anymore, select it and use the remove button beside the folder button. Removing a vault entry removes it from Filing Cabinet's list; it does not delete files from disk.

The current vault title appears at the top of the main area. Storage and item counts are derived from the catalog and vault state.

## Adding Files

The drop zone is the main intake path. You can drag files or folders onto it, or click it to browse for files.

Filing Cabinet supports two intake modes:

- **Move into vault** removes the original after the file is safely copied into the vault. This makes Filing Cabinet the owner of that retained artifact.
- **Copy into vault** keeps the original where it is and stores a retained copy in the vault.

The active mode is shown directly inside the drop zone as **INTAKE MODE** so you can check it before dropping a batch of files. The mode button under **Ingest Options** toggles between move and copy.

The Windows Explorer context menu includes **Copy to Filing Cabinet** and **Move to Filing Cabinet**. The historical MSI registers classic registry verbs. The MSIX package manifest registers packaged File Explorer commands backed by `FilingCabinetShellExtension.dll`, which opens Filing Cabinet and ingests the selected files, folders, or drives using that one-time intake mode without changing the app's default drop-zone setting.

When a file is ingested, Filing Cabinet:

- places it under `items\yyyy\MM\`
- avoids filename collisions by renaming safely
- records the original path
- computes the active hashes selected in Settings
- infers type, category, and starter tags
- extracts searchable text for text-like files
- updates activity, stats, categories, and tags

New catalogs start with SHA-256 as the only active hash. Settings can add BLAKE3, KangarooTwelve, SHA3-256, Skein, legacy digests such as MD5 or Whirlpool, and compatibility checksums when you need to match an existing vendor, archive, firmware, or release record.

## Finding Files

The search box at the top searches the catalog and extracted text. It can match names, types, categories, paths, original paths, notes, tags, hashes, and extracted text content.

The left sidebar gives narrower ways to browse:

- **All Items** shows the full catalog.
- **Recent** and **Inbox** focus on recently ingested or recently modified items.
- **Starred** shows items you marked as important.
- **Quarantine** shows items moved into the quarantine category/folder.
- **Categories** filter by inferred or edited category.
- **Tags** filter by tag. The tag search field narrows the visible tag cloud.

Filters can be combined. For example, you can view recent items in a category, or search within a selected tag.

## The Artifact Table

The table at the bottom is the main catalog view. Selecting a row updates the right panel.

The table toolbar includes:

- sort by name
- sort by modified date
- compact/comfortable row density
- Vault Health

The density toggle is only a display preference. It does not change catalog data. The `Health` button opens the Vault Health workspace without starting a scan or repair.

## Preview, Relations, And Details

The right panel shows the selected artifact.

For images, Filing Cabinet generates a cached thumbnail under the vault's `thumbnails` folder and uses it for preview. For text-like files, it renders a text preview. For unsupported binary formats such as archives, installers, disk images, and other retained files, it keeps the file as a first-class artifact and shows a format-aware fallback card with a category-aware Blue Slate accent.

The **Preview & Relations** tab pairs the preview box with the top related-artifact matches. Relations are capped to a compact top-five list in the panel and show inspectable reasons such as shared tags, same category, same source folder, shared extension family, release markers, source provenance, and nearby hash evidence.

The details area shows editable metadata and file facts:

- name
- category
- tags
- rating
- retention reason
- why this matters
- source provenance
- acquisition method
- trust classification
- retention priority
- archive status
- stored path
- type
- created and modified timestamps
- a focused integrity hash summary
- hash verification status
- extracted text status and index path
- thumbnail/preview generation status
- original source path
- notes

Use **Save** to persist metadata edits. Use **Revert** to reload the selected artifact's current catalog values.

Structured operator metadata is separate from free-form notes. Use it to preserve the human reason an artifact was retained, where it came from, how it was acquired, how much you trust it, and whether it is active, archived, quarantined, or waiting for review.

The Details hash summary shows a standard set of current strong hashes plus any active or retained legacy values. It does not list every optional checksum by default, so broad compatibility support stays available without crowding the artifact view.

## Settings And Hash Choices

The Settings panel groups hash choices by purpose:

- **Recommended** starts with SHA-256, the default industry-standard integrity hash.
- **Modern strong hashes** includes BLAKE3, KangarooTwelve, SHA3-256, and Skein for operators who want additional cryptographic evidence.
- **Legacy cryptographic hashes** includes MD5 and Whirlpool for matching older published digests.
- **Compatibility checksums** and **compatibility non-crypto hashes** are for matching existing records from old tools, archives, devices, or manifests. They are not security evidence.

At least one hash must remain active. Changing the active hash selection changes future ingest, explicit hash checks, health verification, and recompute-hash repairs. Existing retained hash values remain in the catalog so old evidence can still be reviewed.

## Metadata And Recall

The **Relations** area uses deterministic catalog signals only. Relation reasons remain inspectable, such as duplicate hashes, shared tags, same original folder, same ingest session, matching filename tokens, shared extension family, shared provenance tokens, shared release markers, shared hash prefixes, and shared extracted-text keywords.

The navigation panel includes built-in discovery scopes for common recall and cleanup tasks:

- **Unverified** shows artifacts with unverified or questionable trust, or hashes that are not verified.
- **Missing Preview** shows generated previews that are referenced but missing.
- **Repair Needed** shows artifacts with vault health findings.
- **Duplicate Candidates** shows artifacts that share a SHA-256 hash with another catalog item.
- **Same Source Batch** shows artifacts near the selected artifact's source folder and ingest session; without a selected item it shows batch clusters.
- **Large Artifacts** shows artifacts at or above 1 GB.

These views combine with text search, category filters, and tag filters.

## Artifact Actions

The action grid in the right panel contains the operational file actions.

**Open Location** opens File Explorer with the stored vault file selected.

**Open File** opens the stored file with the system default application.

**Restore Copy** lets you choose a destination folder and copies the selected vault file back out. This keeps the vault copy intact.

**Toggle Star** marks or unmarks an artifact as important.

**Add Tags** adds a starter `review` tag to the edit box. Save the metadata to persist it.

**Hash Check** recomputes hashes for the stored file and updates the hash status. If a stored hash does not match, Filing Cabinet reports the mismatch.

**Quarantine** moves the stored file into the vault's `quarantine` folder and updates the artifact category. This is safer than deleting when you are unsure.

**Delete Forever** permanently deletes the stored file and removes the catalog entry after confirmation. This is intended for files you are sure you no longer need.

## Text Extraction

Filing Cabinet extracts text from text-like files during ingest and rescan adoption. Extracted text is stored under `extracted-text\yyyy\MM\` and linked from the artifact record.

This makes retained config files, manifests, scripts, markdown, JSON, TOML, YAML, XML, logs, CSV files, and similar artifacts searchable by content.

Binary files are marked **Not extractable**. Failed extraction is recorded as **Extraction failed** rather than silently pretending the file was indexed.

## Thumbnail Generation

Filing Cabinet generates deterministic local thumbnails for image files during ingest and rescan adoption. Thumbnail files are stored under `thumbnails\yyyy\MM\` inside the vault and referenced by the catalog.

Non-renderable retained artifacts such as installers, archives, torrents, and disk images use format-aware fallback cards instead of shell thumbnails. Repair checks report missing generated thumbnails and attempt to regenerate them when the original vault file is present.

## Related Items

The **Relations** section in the right panel shows the top related artifacts directly under the preview. Related items are deterministic local matches with explainable reasons. They are meant to help you quickly spot nearby artifacts such as a manifest and archive, related images, sibling installers, release documents, or files with matching tags.

## Repair, Rescan, And Backups

Filing Cabinet includes a few recovery-oriented tools.

**Analyze** checks vault health without mutating retained files. It reports missing stored files, duplicate hash groups, missing hashes, hash mismatches, missing thumbnails, orphan thumbnails, missing extracted-text indexes, stale extracted-text indexes, path rebind candidates, files outside the active vault, and incomplete metadata.

**Apply Selected** runs only safe repair candidates after confirmation, such as recomputing missing hashes, regenerating missing thumbnails, re-extracting missing text indexes, and rebinding stale absolute paths when the vault-relative file exists under the active vault. Review-only findings remain visible but are skipped by controlled execution.

Repair activity is written to the vault-local `catalog\repair-log.jsonl` history and summarized in the Vault Health workspace.

**Rescan** looks for files under the vault's `items` folder that are not yet in the catalog and adopts them as cataloged artifacts.

**Back up catalog** writes a portable catalog snapshot into the vault's `exports` folder and validates that the exported JSON can be read back as a usable catalog. This is useful before manual vault work, experimentation, or moving data between machines.

The bottom status strip summarizes vault storage, cataloged size, free space, active scope, and repair status.

## CLI And Headless Operations

Filing Cabinet also includes a separate console executable for scripting and scheduled operations:

```powershell
FilingCabinet.Cli.exe --help
FilingCabinet.Cli.exe ingest --copy --vault K:\Filing Cabinet C:\Downloads\artifact.zip
FilingCabinet.Cli.exe verify --fail-on medium --json
FilingCabinet.Cli.exe search "firmware manifest" --scope all
FilingCabinet.Cli.exe export --output K:\Filing Cabinet\exports
FilingCabinet.Cli.exe report --format json --output K:\Filing Cabinet\exports\health.json
FilingCabinet.Cli.exe repair-preview --json
FilingCabinet.Cli.exe repair --apply --yes
FilingCabinet.Cli.exe rescan --apply --yes
FilingCabinet.Cli.exe rebuild-thumbnails --apply --yes
FilingCabinet.Cli.exe package --output K:\Filing Cabinet\exports\FilingCabinetPackage --zip
```

The CLI writes real stdout/stderr and returns script-friendly exit codes: `0` for success, `1` for command/runtime failure, `2` when verification findings meet the requested threshold, and `3` for partial ingest or partial repair/rebuild. Mutating repair, rescan, and thumbnail rebuild commands require both `--apply` and `--yes`; without `--apply`, they report what would happen.

The `package` command writes a deterministic vault export containing catalog JSON, catalog JSONL, retained items, extracted text, thumbnails, repair logs, and a vault-health report. Use `--zip` for a single cold-storage archive.

## Preservation Docs

Filing Cabinet's preservation model is documented in:

### Preservation Model

- [Design and Preservation Model](docs/Filing Cabinet%20%E2%80%94%20Design%20and%20Preservation%20Model.md)
- [Vault Lifecycle and Verification Model](docs/Filing Cabinet%20%E2%80%94%20Vault%20Lifecycle%20and%20Verification%20Model.md)
- [Repair and Recovery](docs/Filing Cabinet%20%E2%80%94%20Repair%20and%20Recovery.md)

### Technical Rationale

- [Installer Hash Manifest](docs/Filing Cabinet%20-%20Installer%20Hash%20Manifest.md)
- [Hashing and Compatibility](docs/Filing Cabinet%20%E2%80%94%20Hashing%20and%20Compatibility.md)
- [Privacy Policy](docs/Filing Cabinet%20Privacy%20Policy.md)
- [License](docs/Filing Cabinet%20-%20License.md)

### Release Notes

- [v0.1.2 - Store MSIX Candidate](docs/Filing Cabinet%20v0.1.2%20-%20Store%20MSIX%20Candidate.md)
- [Release Checklist](docs/Filing Cabinet%20-%20Release%20Checklist.md)

## Design Boundaries

Filing Cabinet is intentionally focused on deliberate curation rather than automatic inference.

Text extraction currently handles text-like files, not image text or scanned PDFs.

PDF preview is currently a retained-file fallback rather than full document rendering.

Windows shell thumbnails are not used yet. Preview generation is intentionally local and deterministic.

## Operational Notes

Filing Cabinet is designed around cautious ownership:

- Move mode is for files you want the vault to own.
- Copy mode is for files you want retained without disturbing the original.
- Restore Copy gets a file back out without removing it from the vault.
- Quarantine isolates questionable files without deleting them.
- Delete Forever is the explicit irreversible removal path.

When in doubt, use Copy mode or Quarantine first. Use Delete Forever only when you are certain the retained file and catalog entry should be removed.

## Developer Notes

The app is a WPF/VB project targeting `.NET 10.0-windows`.

Useful commands:

```powershell
dotnet build FilingCabinet.vbproj --no-restore
dotnet test FilingCabinet.Tests\FilingCabinet.Tests.vbproj --no-restore
```

MSIX packages that include Explorer context-menu integration must build the native shell-extension DLL before packing:

```powershell
installer\build-msix.ps1
```

If the app is currently running, Windows may lock `bin\Debug\net10.0-windows\FilingCabinet.exe`. For verification while the app is open, build without an app host into a temporary output path:

```powershell
dotnet build FilingCabinet.vbproj --no-restore -p:OutputPath=.verify-build\ -p:UseAppHost=false
```



