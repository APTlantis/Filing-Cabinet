# Filing Cabinet Release Checklist

## v0.1.2.1 Local MSIX Verification Record

- [x] Package identity is `Aptlantis.FilingCabinet`, version `0.1.2.1`, X64.
- [x] Local candidate exists at `trust/Aptlantis.FilingCabinet_0.1.2.1_x64.msix`.
- [x] SHA-256 matches `trust/Filing-Cabinet-0.1.2.1.hashmanifest.toml`: `01C2DA991BA0B20FC172C6EDAC6E467F839E5F2E73D3EAF13966DE05879C6CA1`.
- [x] Detached PGP and SLH-DSA signatures exist for the 0.1.2.1 hash manifest.
- [x] `trust/Filing-Cabinnet v0.1.2.1 WACK.xml` records a complete WACK UI run with `OVERALL_RESULT=PASS` on 2026-09-02.
- [ ] Partner Center acceptance recorded for this exact package.
- [ ] Microsoft-signed Store-distributed package inspected for this exact package.
- [ ] Final distributed-package install, launch, data-safety, and recovery checks completed.

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
