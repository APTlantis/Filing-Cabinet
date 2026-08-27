# Filing Cabinet Release Checklist

## v0.1.2 Store MSIX Release Record

- [x] Package identity uses `Aptlantis.FilingCabinet`.
- [x] `Package.appxmanifest` version is `0.1.2.0`.
- [x] Store candidate file exists at `trust/Aptlantis.FilingCabinet_0.1.2.0_x64.msix`.
- [x] ARHS hash manifest exists at `trust/FileCabinet-0.1.2.0.hashmanifest.toml`.
- [x] Detached PGP signature exists for the ARHS hash manifest.
- [x] Detached SLH-DSA signature exists for the ARHS hash manifest.
- [x] Source records distinguish Store package acceptance from certification, publication, and Microsoft-signed distribution verification.
- [x] Microsoft Store listing captured as public-availability evidence on 2026-08-26.
- [ ] Microsoft-signed Store-distributed package verified.
- [ ] Install or launch behavior verified from the final distributed package.
- [ ] Data-safety and recovery notes verified against the final distributed package.
- [ ] Final release claim made only after the Store-distributed package and release records agree.

## Historical MSI Evidence

- [x] v0.1.1 MSI exists at `artifacts/installer/FilingCabinet-0.1.1.0-win-x64.msi`.
- [x] v0.1.1 MSI hash manifest exists at `artifacts/installer/FilingCabinet_msi-0.1.1.0.hashmanifest.toml`.
- [ ] No current v0.1.2 MSI evidence has been recorded.

## Notes

The historical package-validation record is not a substitute for a distributed-package inspection. Keep Store availability, Microsoft Store signing, local install/launch verification, and package-specific provenance records separate for future releases.
