# Filing Cabinet Project Proposal

## Project Type

Desktop application

## Responsibility Posture

adoptable

## Readiness Level

ready

## Governing Standards

- Proposal: PPS
- Workspace: WGS
- Delivery: DRS
- Supporting: ARHS and AAMHS

## Problem Statement

High-signal technical files, installers, notes, hashes, screenshots, and release evidence accumulate across folders where they are easy to lose, duplicate, or strip of context. A durable local vault needs to preserve the artifact, its metadata, its verification state, and the operator's reason for keeping it.

## Mission

Filing Cabinet is a local-first Windows desktop vault for retaining, cataloging, previewing, verifying, repairing, and recovering technical artifacts with deterministic metadata and integrity evidence.

## Design Boundaries

In scope:

- Windows desktop vault application, CLI, tests, documentation, and packaging.
- Local filesystem vault plus AppData catalog.
- Artifact records, metadata, tags, previews, extracted text, relations, health checks, repair history, and export workflows.
- MSIX public distribution through Microsoft Store with Microsoft signing.
- ARHS release hash manifests and AAMHS preservation signatures where appropriate.

Out of scope:

- Cloud vault storage as a normal requirement.
- Automatic destructive repair without operator review.
- Public Windows release claims before Store certification/publication and Microsoft-signed package verification.
- Treating detached PGP or SLH-DSA signatures as a substitute for platform package signing.

## Success Criteria

- [ ] Users can retain, find, preview, verify, repair, and export local technical artifacts.
- [ ] Vault data remains local-first and recoverable.
- [ ] Release manifests, source release notes, packaged docs, checklist, installer/MSIX hashes, and signing/provenance records agree.
- [ ] Microsoft Store MSIX workflow is verified for the public Windows GUI release path.
- [ ] Historical MSI evidence remains clearly separated from the current public distribution authority.

## Failure Criteria

- [ ] Vault state becomes opaque or unrecoverable without the app.
- [ ] Automatic repair deletes or mutates retained files without explicit operator control.
- [ ] Release records conflate Store acceptance, certification/publication, and Microsoft-signed distribution verification.
- [ ] Manifest, README, package, hash, and release note versions drift without being recorded.

## Constraints

- Technical: Windows desktop application with WPF/VB.NET, CLI, tests, MSIX packaging, and historical WiX MSI evidence.
- Scope: local artifact preservation and recovery, not cloud document management.
- Runtime: Windows x64.
- Data: user-selected vault root and local AppData catalog.

## Risks

- Risk: Release evidence across manifests, README, docs, hashes, and Store records can drift.
- Mitigation: Treat version and release-record synchronization as a release gate.

- Risk: Store package acceptance can be mistaken for public release completion.
- Mitigation: Preserve the distinction between accepted package candidate, certification/publication, Microsoft Store signing, install/launch verification, and final release claim.

## Roadmap

1. Keep the active Store MSIX line aligned with product identity Filing Cabinet / Aptlantis.FilingCabinet.
2. Reconcile manifest and Project-README drift around the current Store candidate package version `0.1.2.0` before the next release claim.
3. Rebuild, rehash, and resubmit package candidates whenever packaged shell integration or release payload changes.
4. Verify Microsoft-signed distributed Store package, launch, data safety, docs, and recovery behavior before public release claims.

## Version Milestone Sketch

### v0.1.2.0

- Purpose: Store-aligned public Windows GUI package candidate with current product identity, release hashes, documentation payload, and shell integration posture reconciled.
- Completion shape: Source build, tests, MSIX package, Partner Center state, ARHS hash manifest, detached preservation signatures, packaged docs, install/launch behavior, and release notes all agree; any remaining certification/publication gaps are explicit.
- Responsibility posture: adoptable
- Complete project endpoint: yes
- Deferred: Cloud vault services and automatic destructive repair.

### v0.2

- Purpose: Next feature-bearing vault release after the Store distribution baseline is trustworthy.
- Completion shape: New vault behavior has migration, repair, release, and recovery evidence without weakening v0.1.x integrity boundaries.
- Responsibility posture: adoptable
- Complete project endpoint: no
- Deferred: Cross-platform vault behavior unless separately proposed.
