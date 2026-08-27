# Filing Cabinet v0.1.2 - Historical Store MSIX Candidate

Date: 2026-08-21

## Scope

This record preserves the v0.1.2.0 Store MSIX candidate evidence used before public availability. Filing Cabinet is now available through the Microsoft Store; the active package manifest version is `0.1.2.1`.

## Package Evidence

- Store product name: `Filing Cabinet`
- Store identity name: `Aptlantis.FilingCabinet`
- Publisher: `CN=81D6747D-F84F-4EFF-ACAA-9635D91ACCD0`
- Package version: `0.1.2.0`
- Package file: `trust/Aptlantis.FilingCabinet_0.1.2.0_x64.msix`
- Package size: `9,922,897` bytes
- Store-reported size: `9.4 MB`
- Hash manifest: `trust/FileCabinet-0.1.2.0.hashmanifest.toml`
- Detached PGP signature: `trust/FileCabinet-0.1.2.0.hashmanifest.toml.asc`
- Detached SLH-DSA signature: `trust/FileCabinet-0.1.2.0.hashmanifest.toml.sphincs`

## Hash Evidence

- SHA-256: `32f2212336d6d0d5f66b3162fdcb548cb7d4718bfd18d5ecb79af09c6985cdf2`
- BLAKE3-256: `06275140402bc6b0f19dccaa44587cc34d500d239bfa62ba6d6adaf3643ec195`
- KT128: recorded in `trust/FileCabinet-0.1.2.0.hashmanifest.toml`

## Signing Boundary

The detached PGP and SLH-DSA files sign the ARHS hash manifest for preservation and provenance. They do not replace Microsoft Store signing.

## Verification Boundary

This historical record documents the candidate package and local trust files. It does not independently certify installation, launch, uninstall, or the signature of the distributed package; public availability is recorded by the current Store listing capture in the project README.

Historical MSI evidence remains at `artifacts/installer/FilingCabinet-0.1.1.0-win-x64.msi` and `artifacts/installer/FilingCabinet_msi-0.1.1.0.hashmanifest.toml`.
