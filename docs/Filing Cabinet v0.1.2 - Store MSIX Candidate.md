# Filing Cabinet v0.1.2 - Store MSIX Candidate

Date: 2026-08-21

## Scope

Filing Cabinet v0.1.2 is the Store MSIX candidate line for the public Windows GUI distribution path. It keeps the local-first vault workflow from the v0.1.1 compliance alignment work and aligns the package identity with the Microsoft Store product record.

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

The detached PGP and SLH-DSA files sign the ARHS hash manifest for preservation and provenance. They do not replace Microsoft Store signing. Public Windows release claims still require certification/publication status and verification of the Microsoft-signed distributed Store package.

## Verification Boundary

This record documents the Store candidate package and local trust files. It does not certify installation, launch, uninstall, Store signing, or public availability.

Historical MSI evidence remains at `artifacts/installer/FilingCabinet-0.1.1.0-win-x64.msi` and `artifacts/installer/FilingCabinet_msi-0.1.1.0.hashmanifest.toml`.
