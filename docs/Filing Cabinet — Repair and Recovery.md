# Filing Cabinet — Repair and Recovery

Filing Cabinet repair is designed around **analysis before mutation**.

Health analysis identifies conditions where catalog state, retained files, generated assets, or vault paths no longer agree. Repair tools then provide explicit recovery paths without hiding risk or silently rewriting trusted history.

The normal workflow is:

```text
Analyze
  ↓
Review findings
  ↓
Select repair candidates
  ↓
Apply deliberately
  ↓
Verify again
```

## Analyze First

Start with the desktop **Analyze** operation or the CLI verification command:

```powershell
FilingCabinet.Cli.exe verify --fail-on medium
```

Review findings before applying changes.

Pay particular attention to conditions involving:

- missing retained files
- hash mismatches
- files outside the vault
- broken paths
- unexpected duplicates
- incomplete ingest state
- catalog validation failures

Routine analysis is metadata-first.

Filing Cabinet does not automatically read every retained file end-to-end simply to produce a health report. Explicit hash verification remains available when stronger content evidence is needed.

When bounded hash verification is enabled, retained files up to 1 GB may be checked during health analysis. Hash verification for files larger than 16 GB may be deferred and reported as deferred instead of forcing an expensive full read.

## Common Findings

### Missing Retained File

The catalog references a retained file that is not present at the expected path.

Possible causes include:

- unavailable or unmounted vault storage
- accidental deletion
- interrupted migration
- incomplete recovery
- storage failure

Recommended response:

1. Confirm that the expected vault location is available.
2. Check whether the file exists at another known vault or backup location.
3. Restore from backup when appropriate.
4. Avoid deleting the catalog entry until the loss is understood.

A missing retained file should not be treated like a missing thumbnail or other rebuildable asset.

---

### Hash Mismatch

The retained file exists, but its current hash differs from the trusted catalog value.

Recommended response:

1. Treat the file as changed or suspect.
2. Compare it against known-good backup or source material.
3. Review whether an intentional replacement occurred.
4. Do not automatically overwrite the trusted catalog hash.

A mismatch is evidence that the current bytes cannot be assumed to be the originally recorded artifact.

Use explicit hash verification when a complete content check is required.

---

### Missing Thumbnail

A generated preview thumbnail is absent.

Recommended response:

1. Confirm that the retained source file still exists.
2. Rebuild the thumbnail.
3. Verify that preview generation completes successfully.

Because thumbnails are derived state, their loss generally does not threaten the retained source artifact.

---

### Failed Thumbnail Generation

Thumbnail generation failed or the retained source could not be decoded.

Recommended response:

1. Leave the retained source artifact unchanged.
2. Confirm that the source file is readable.
3. Retry generation if appropriate.
4. Treat repeated preview failure as a preview issue unless other verification findings indicate content damage.

---

### Missing Extracted Text

The artifact references extracted text, but the generated text asset is absent.

Recommended response:

1. Confirm that the retained source file is present.
2. Re-extract text if the source type is supported.
3. Verify search behavior after regeneration.

This primarily affects recall and search rather than retained content identity.

---

### Stale Extracted Text

Extracted text exists but no longer corresponds cleanly to current catalog state.

Recommended response:

1. Confirm the retained artifact is intact.
2. Regenerate extracted text when appropriate.
3. Verify the resulting search index.

Do not treat stale derived text as equivalent to source-file corruption.

---

### Orphan Retained File

A file exists under the vault's retained items area but no catalog artifact references it.

Possible causes include:

- interrupted ingest
- manual filesystem changes
- recovery work
- incomplete catalog writes

Recommended response:

1. Preview rescan results.
2. Inspect the orphan file.
3. Confirm that it belongs in the vault.
4. Adopt it only when its role is understood.

Do not automatically catalog every unknown file.

---

### Orphan Generated Asset

A thumbnail or extracted-text file exists but no current artifact references it.

Recommended response:

1. Report the orphan first.
2. Determine whether it belongs to a deleted, moved, or repaired artifact.
3. Remove it only through an explicit cleanup action.

Generated assets are lower risk, but unexplained state should still be reviewed before deletion.

---

### Missing Active Hash

An artifact does not contain one or more hash values required by the current active hash configuration.

Recommended response:

1. Confirm the retained source file exists.
2. Recompute the currently active hashes.
3. Preserve any historical hashes already stored for disabled algorithms.

Disabled algorithms should not be reported as missing merely because they are no longer active.

---

### Duplicate Content

Two or more catalog artifacts share the same cryptographic content identity.

Recommended response:

1. Review artifact context and provenance.
2. Determine whether both records are intentional.
3. Keep both when they represent legitimately distinct retention context.
4. Remove or consolidate only after operator review.

Identical bytes do not always mean redundant records.

---

### Relative Path Broken After Vault Move

The catalog's previous absolute path no longer resolves, but the artifact can be found through its relative location under the current vault root.

Recommended response:

1. Preview the rebind candidate.
2. Confirm that the located file is the expected retained artifact.
3. Rebind the catalog path through the approved repair action.

Avoid ad hoc path rewriting when Filing Cabinet can resolve the relationship deterministically.

---

### File Outside the Vault

A catalog artifact references a path outside the selected vault boundary.

Recommended response:

1. Inspect the external location.
2. Determine whether the condition is intentional or historical.
3. Restore or copy the file into the vault when appropriate.
4. Update ownership only through an explicit approved action.

External references reduce portability and weaken the vault's ownership boundary.

---

### Interrupted Ingest

Ingest stopped after part of the operation completed.

Possible states include:

- retained file exists but no catalog entry was created
- catalog entry exists but required metadata is incomplete
- generated assets are partially present

Recommended response:

1. Analyze the resulting state.
2. Use orphan adoption or deterministic metadata repair as appropriate.
3. Confirm the artifact before finalizing the repaired record.

Avoid starting over destructively when the already-retained evidence can be recovered safely.

---

### Catalog Backup Validation Failure

An exported or restored catalog cannot be loaded correctly or fails its expected roundtrip validation.

Recommended response:

1. Refuse the questionable restore.
2. Keep the currently working catalog unchanged.
3. Preserve the failed backup for investigation.
4. Restore from another known-good snapshot if available.

Catalog replacement should fail safely.

---

### Vault Portability Failure

A moved vault cannot resolve retained artifacts correctly under its new root.

Recommended response:

1. Confirm the expected vault structure.
2. Review path-rebind candidates.
3. Verify retained content where appropriate.
4. Apply path changes only after the mapping is understood.

Do not solve portability problems by deleting unresolved records.

## Desktop Recovery Tools

The desktop application provides operator-facing repair and recovery actions including:

- **Analyze**
- **Rescan**
- **Apply Selected Repair Candidates**
- **Hash Check**
- **Restore Copy**
- **Quarantine**
- **Delete Forever**

Long-running maintenance operations should run asynchronously so the interface remains responsive during:

- hashing
- orphan scans
- repair preparation
- generated asset rebuilds
- health analysis

## CLI Recovery Tools

The CLI exposes the same recovery model for scripting and headless operation.

Preview before mutation:

```powershell
FilingCabinet.Cli.exe repair-preview --json
```

Apply approved repair operations:

```powershell
FilingCabinet.Cli.exe repair --apply --yes
```

Rescan and adopt approved retained files:

```powershell
FilingCabinet.Cli.exe rescan --apply --yes
```

Rebuild generated thumbnails:

```powershell
FilingCabinet.Cli.exe rebuild-thumbnails --apply --yes
```

Mutating commands require both:

```text
--apply
--yes
```

This keeps reporting and mutation clearly separated.

## Integrity and Recovery Matrix

| Scenario | Detection | Expected Report | Safe Repair Action | Risk |
|---|---|---|---|---|
| Stored file missing | Catalog path is empty or file does not exist | Missing-file count and affected artifacts | Restore if possible; preserve catalog evidence | Medium |
| Duplicate retained file | Two or more artifacts share SHA-256 | Duplicate groups and samples | Review context; operator decides | Low |
| Orphan file under retained items | File exists with no catalog reference | Orphan count and samples | Preview rescan, then adopt deliberately | Low |
| Missing thumbnail | Artifact expects thumbnail but file is absent | Missing-thumbnail count | Regenerate from retained source | Low |
| Failed thumbnail generation | Preview generation fails | Generation failure | Preserve source; retry if useful | Low |
| Orphan thumbnail | Thumbnail has no artifact reference | Orphan count | Review before cleanup | Low |
| Stale extracted text | Extracted text has no valid current relationship | Stale-index count | Regenerate or clean up after review | Low |
| Missing extracted text | Referenced extracted-text file is absent | Missing-index count | Re-extract from supported source | Medium |
| Active hash missing | Current active hash has no stored value | Incomplete verification finding | Recompute active hashes | Low |
| Hash mismatch | Recomputed value differs from catalog | Mismatch count and artifacts | Investigate; do not overwrite automatically | High |
| Broken path after vault move | Old path fails but relative path resolves | Rebind candidates | Rebind after approval | Medium |
| File outside vault | Catalog path resolves outside vault | External-path finding | Copy or restore into vault after review | Medium |
| Interrupted ingest | File/catalog/generated state only partially exists | Orphan or incomplete-state finding | Adopt or complete deterministically | Medium |
| Catalog backup roundtrip failure | Backup cannot validate | Backup validation failure | Refuse restore; retain current catalog | High |
| Vault portability roundtrip failure | Moved vault cannot resolve retained files correctly | Rebind or unresolved-path finding | Repair mappings before destructive changes | High |

## Quarantine

Quarantine is appropriate when a retained file should no longer be treated as normal trusted vault content but should not yet be destroyed.

Typical reasons include:

- unexpected hash mismatch
- questionable provenance
- suspected corruption
- potentially unsafe content
- an unresolved recovery situation

Quarantine should preserve enough metadata to explain:

- what artifact was quarantined
- why
- when
- from which original retained location

Quarantine is an evidence-preserving action, not a substitute for understanding the finding.

## Restore Copy

Restore Copy returns an artifact to a chosen destination while preserving the vault copy.

Use it when:

- a working copy is needed
- an artifact needs to be returned to another system
- recovery testing is being performed
- the vault copy should remain authoritative

Restore should not silently transfer ownership away from the vault.

## Delete Forever

Delete Forever is the explicit irreversible removal path.

It should remain clearly distinct from:

- quarantine
- repair
- rescan
- generated-asset cleanup
- restore

Before destructive removal, confirm that any evidence or metadata worth retaining has been exported or preserved elsewhere.

## Recovery Principle

When there is uncertainty, preserve evidence first.

Prefer:

```text
report
export
package
quarantine
review
```

before:

```text
overwrite
discard
delete
```

The goal of repair is not to make warnings disappear.

The goal is to return the vault to a state that is understandable, verifiable, and trustworthy.

## Repair UX Iteration Process

Use this process when a repair or vault-health workflow is technically correct but not easy to understand in the desktop app.

The goal is to improve operator confidence without changing the safety model.

```text
Observe the confusing moment
  ↓
Name the hidden state
  ↓
Expose the reason in the UI
  ↓
Pin the behavior with tests
  ↓
Verify before packaging
```

### 1. Capture the Confusing Moment

Start from the operator question, not from the implementation.

Examples:

- Why is this button disabled?
- Is the app currently doing work?
- Why was this row selected automatically?
- Why can this row not be selected?
- Why did this option work last time but not now?
- What will Apply actually change?

Do not begin by adding new repair behavior. First identify which existing state is hidden from the operator.

### 2. Separate Repair Semantics from Repair Explanation

Keep these boundaries distinct:

```text
Repair semantics:
what the app is allowed to do

Repair explanation:
what the app tells the operator about that decision
```

For example, review-only findings should remain review-only. The improvement is to say that their checkboxes are disabled because Filing Cabinet will not choose an interpretive or destructive fix automatically.

### 3. Make Workflow Phase Visible

Every vault-health surface should make the current phase obvious:

- no analysis has run yet
- analysis is running
- repair application is running
- findings are published
- no findings exist
- apply is unavailable because nothing automatic is selected

When a command is disabled, pair the disabled state with a nearby explanation. Do not rely on the user discovering a tooltip.

### 4. Make Row Selection Rules Visible

Each repair candidate row should explain:

- whether it is automatic or review-only
- whether it is selected
- why it was selected by default or left unselected
- what kind of state it will touch
- whether it may read retained file content

Recommended impact labels:

| Impact | Meaning |
|---|---|
| Catalog only | Updates catalog metadata without rewriting retained files |
| Generated asset + catalog | Regenerates derived state such as thumbnails and updates the catalog |
| Reads retained file + catalog | Reads retained content to compute or verify catalog metadata |
| Reads retained file + generated index | Reads retained content to rebuild derived search text |
| Read-only review | Reports a condition but does not apply an automatic fix |

### 5. Keep Safe Defaults Boring

Safe default selection should stay conservative:

- select deterministic catalog/path repairs when the mapping is explicit
- select regenerable derived assets when the retained source is present
- leave expensive hash recomputation unselected unless explicitly requested
- leave duplicate content, hash mismatch, outside-vault files, and incomplete provenance as review-only

The UI may make these choices clearer, but it should not quietly broaden what gets applied.

### 6. Test the Explanation Layer

Add focused tests for the text or state that prevents confusion.

Useful checks include:

- review-only rows report that they cannot be applied automatically
- expensive automatic rows explain that they may read retained files
- safe catalog repairs report catalog-only impact
- the Apply summary changes when nothing is selected versus when automatic repairs are selected

These tests are not copywriting tests for every word. They are guardrails for the operator-facing contract.

### 7. Verify the Whole Loop

Before packaging or reinstalling, run:

```powershell
dotnet test .\FilingCabinet.Tests\FilingCabinet.Tests.vbproj
```

For UI-facing changes, also do a manual smoke pass after reinstall:

1. Open the Vault Health dashboard.
2. Confirm the dashboard says no analysis has run yet.
3. Run Analyze Health.
4. Confirm progress/status changes while work is running.
5. Confirm Apply explains why it is disabled or what it will do.
6. Inspect automatic, expensive automatic, and review-only rows.
7. Select and clear visible repairs.
8. Apply only a known safe candidate.
9. Run Analyze Health again and confirm the resulting state is understandable.

The pass is successful when the operator can tell what Filing Cabinet is doing, why an option is available or unavailable, and what kind of state a selected repair will affect.
