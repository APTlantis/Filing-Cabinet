# Filing Cabinet

## Purpose

Filing Cabinet is the governed project for Filing Cabinet, a local-first Windows desktop vault for retaining, cataloging, previewing, verifying, and recovering technical artifacts. The project includes a VB.NET/WPF application, CLI, tests, Windows MSIX packaging, a historical WiX installer workflow, documentation, integrity metadata, and repair/recovery facilities.

## Governance

- [Filing Cabinet.manifest.toml](Filing%20Cabinet.manifest.toml)
- [Project proposal](Project-Proposal.md)
- [AGENTS.md](AGENTS.md)
- [User and operator README](README.md)
- [Desktop Application Release Standard](D:/.city_hall/DRS/README.md)
- [Windows GUI MSIX And Microsoft Store Workflow](../Windows-GUI-MSIX-Store-Workflow.md)

## Current state

Version `0.1.2` is the active Microsoft Store release line, with package manifest version `0.1.2.1`. The prior v1.x installer train is retained as pre-standard historical evidence and no longer acts as current release authority.

The public Windows release path is MSIX through the Microsoft Store, where the Store signs the public package. Filing Cabinet is available in the Store under the following product identity:

- Package identity name: `Aptlantis.FilingCabinet`
- Publisher: `CN=81D6747D-F84F-4EFF-ACAA-9635D91ACCD0`
- Publisher display name: `Aptlantis`
- Package family name: `Aptlantis.FilingCabinet_jfrcsngvdwx7g`
- Store ID: `9N29X9KR70R3`

The supplied Store listing capture confirms the public availability of Filing Cabinet. The retained `Aptlantis.FilingCabinet_0.1.2.0_x64.msix` package was accepted by Partner Center package validation as version `0.1.2.0`, X64, Windows.Desktop min version `10.0.18362.0`, language `en-us`, with capabilities `runFullTrust` and `Microsoft.storeFilter.core.notSupported_8wekyb3d8bbwe`, reported size `9.4 MB`. That retained package evidence is historical. The local `0.1.2.1` MSIX has matching hash evidence and a full WACK pass, but no Partner Center acceptance or Store-distributed-package inspection is recorded for it.

Current local 0.1.2.1 package evidence:

- MSIX package: `trust/Aptlantis.FilingCabinet_0.1.2.1_x64.msix`
- ARHS hash manifest: `trust/Filing-Cabinet-0.1.2.1.hashmanifest.toml`
- Detached PGP signature: `trust/Filing-Cabinet-0.1.2.1.hashmanifest.toml.asc`
- Detached SLH-DSA signature: `trust/Filing-Cabinet-0.1.2.1.hashmanifest.toml.sphincs`
- WACK report: `trust/Filing-Cabinnet v0.1.2.1 WACK.xml` (`OVERALL_RESULT=PASS`)

The detached PGP and SLH-DSA files sign the hash manifest for preservation/provenance evidence. They do not replace Microsoft Store signing for the public MSIX distribution. The signed hash manifest’s internal `path` retains the packager-provided location without the `trust` segment; its artifact filename, recorded size, and SHA-256 match the package retained at the path above.

The v0.1.0 local release was verified on 2026-08-04 with source build, 104 passing tests, WiX MSI packaging, SHA-256 hashing, unsigned Authenticode status, and launch verification from the published executable. The MSI lifecycle was later verified on 2026-08-17 with quiet install, shell integration checks, installed CLI version, installed WPF launch, installed documentation payload, and quiet uninstall cleanup. That MSI record remains historical/local direct-distribution evidence.

The project is licensed under the MIT License. The historical 0.1.2.0 Store MSIX candidate has a dedicated hash manifest at `trust/FileCabinet-0.1.2.0.hashmanifest.toml`. The historical MSI artifact remains `artifacts/installer/FilingCabinet-0.1.1.0-win-x64.msi` with hash manifest `artifacts/installer/FilingCabinet_msi-0.1.1.0.hashmanifest.toml`; no v0.1.2 MSI artifact is recorded.

Governance and product-facing records use **Filing Cabinet**. Compact `FilingCabinet` identifiers remain where Windows, .NET, package identity, executable naming, artifact filenames, or repository slugs should not contain spaces.

## Architecture and workflows

- WPF desktop application and VB.NET domain/services.
- Separate CLI and MSTest projects.
- Local JSON catalog and user-selected portable vault storage.
- Deterministic ingest, preview, relation, health, repair, export, and integrity workflows.
- Native `IExplorerCommand` shell-extension DLL for packaged MSIX **Copy to Filing Cabinet** and **Move to Filing Cabinet** Explorer commands.
- `winapp` MSIX workflow using `Package.appxmanifest` and generated assets under `Assets`.
- PowerShell/WiX installer pipeline under `installer` retained as local/direct-distribution evidence.

## Verification entry points

Follow `README.md`, the Store-aligned `Package.appxmanifest`, and the DRS MSIX workflow. Future public release verification must cover source build, tests, MSIX/MSIXUPLOAD creation, Store identity alignment, Store submission status, Store signing authority, ARHS hash evidence for the submitted package, installation or launch verification, data-safety notes, license inclusion, and documentation/manifests aligned to the resulting artifact.



