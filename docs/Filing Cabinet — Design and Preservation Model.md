# Filing Cabinet — Design and Preservation Model

Filing Cabinet is a deterministic local-first vault for preserving high-signal technical artifacts and the operational context surrounding them.

It exists because important files often outlive the context that made them important. A retained artifact is not only bytes. It is also source, purpose, trust state, verification history, operator intent, and recovery history.

Filing Cabinet is designed to preserve that surrounding context as deliberately as it preserves the file itself.

## Files Are Artifacts, Not Just Bytes

A file can remain perfectly intact and still become effectively useless if nobody remembers what it is, where it came from, or why it was kept.

Useful retained context can include:

- where the file came from
- when it was captured
- whether it was copied or moved
- what source batch it belonged to
- what project, vendor, device, customer, release, or incident it relates to
- why it was retained
- what hashes identify its content
- what trust classification was assigned
- what repair or recovery actions have occurred since capture

That context helps future operators answer not merely whether a file exists, but whether it is still understandable and trustworthy.

## The Deliberate Retention Tradeoff

Filing Cabinet intentionally asks for a little more effort when an artifact enters the vault.

That work may include:

- choosing copy or move intake
- reviewing the generated category
- adding tags
- choosing a concise preservation purpose
- recording source provenance
- marking operator trust separately from cryptographic verification
- writing notes while the surrounding context is still fresh

This is more work than dropping a file into a normal folder and walking away.

That tradeoff is deliberate.

Files are easiest to understand at the moment they are retained. At that time, the operator often still knows where the file came from, why it matters, whether it is trusted, and what would be difficult to rediscover later.

Six months later, some of that context may be weaker. Years later, it may be gone.

Filing Cabinet asks for enough structure to preserve that knowledge before it decays.

The vault does not require perfect metadata to be useful. Even a lightly reviewed artifact with a category, tags, hashes, source path, retained path, and extracted text already has more long-term context than a loose file sitting in an ordinary folder.

The richer the record, however, the stronger the future payoff.

The compact operator-context fields are:

- purpose: why it was worth preserving
- provenance: what kind of source it came from
- trust: how the operator regards it, separate from whether its bytes verify
- optional notes for details that do not fit those choices
- tags and custom metadata for deeper organization

Priority and archive status remain catalog lifecycle properties rather than part of the operator-context pass.

The goal is not to turn artifact intake into form-filling. Starter values, inferred categories, bulk tools, intake profiles, saved views, review queues, and repair suggestions should reduce friction while preserving operator judgment.

Filing Cabinet should make a quick pass useful and a careful pass substantially more valuable.

## Operator-Authored Context

Generated metadata can assist recall, but operator-authored context remains more important.

The operator may know details the system cannot infer reliably:

- the driver that fixed a particular device
- the firmware image actually used during a deployment
- the release package that was shipped
- the configuration snapshot taken before a failure
- the installer that later disappeared from a vendor site
- the document that recorded an important operational decision

Filing Cabinet should therefore make it easy to add meaningful context through:

- purpose, provenance, trust, and notes
- tags
- categories
- custom metadata
- starred state
- retention and trust fields
- relationship review

Automatic inference may assist organization, but it should not quietly rewrite the story of a retained artifact.

## Local-First Ownership

Filing Cabinet is local-first by design.

The vault is meant to remain useful without:

- a subscription
- a remote service
- an account login
- a cloud index
- a hidden database server

For Filing Cabinet, local-first means:

- retained files live in a user-selected local vault
- catalog data is stored locally
- generated assets are local files
- health analysis runs locally
- repair and rescan decisions remain operator-approved
- CLI operations work without requiring the desktop UI

The vault can be copied, backed up, inspected, packaged, and restored with ordinary filesystem tools.

Local-first does not mean backup is unnecessary. Filing Cabinet focuses on retention structure, context, and verification. It is not a substitute for redundant storage or a broader backup strategy.

A strong backup should preserve the vault root, catalog state, exported catalog snapshots, deterministic vault packages, and installer artifacts needed to reinstall the same release.

## Determinism and Explainability

Determinism matters because preservation is a long-term promise.

The system should be able to explain:

- what it retained
- where it stored the artifact
- how the content was identified
- what metadata was recorded
- what generated assets were created
- what changed later
- what repairs or recovery actions were performed

Filing Cabinet uses deterministic retention to keep important state transitions inspectable:

- retained paths are organized under the vault
- catalog entries record source and storage context
- hashes identify file content
- generated assets can be rebuilt from retained files
- health reports are derived from catalog and filesystem state
- CLI output is stable enough for scripts and scheduled checks

The goal is not to make every workflow automatic.

The goal is to make every important transition understandable.

## Search Is Not Trust

Search helps find things, but search alone does not preserve context or establish trust.

A file may be searchable while still having serious unanswered questions:

- its original source may be unknown
- it may have changed since capture
- its folder may no longer explain why it mattered
- duplicate copies may be indistinguishable
- generated metadata may obscure uncertainty

Filing Cabinet treats search as one part of recall rather than the foundation of trust.

Content identity, source information, operator-authored metadata, verification state, and recovery history remain separate concerns.

## Generated Assets Support the Artifact

Filing Cabinet may create local generated assets such as:

- thumbnails
- preview fallbacks
- extracted text
- relation hints

These improve recall and navigation, but they do not replace the retained source file.

Generated assets should remain rebuildable whenever possible.

Filing Cabinet deliberately avoids heavier or less predictable extraction pipelines such as PDF page rendering, image-text OCR, and Windows shell thumbnail extraction. Preview and thumbnail generation remain local and deterministic.

## What Belongs in the Vault

Filing Cabinet is not intended to replace the normal filesystem.

Downloads folders, project directories, scratch areas, and ordinary document folders remain appropriate for transient or routine work.

The vault is intended for the smaller set of files where future trust and recoverability matter more than immediate convenience.

Good candidates include:

- installers that may disappear from vendor sites
- drivers and firmware that solved a real problem
- manifests, logs, and configuration snapshots
- keys and recovery files
- operational documents
- datasets and research artifacts
- release packages and build evidence
- archives that need provenance and verification

The vault is a deliberate retention space, not a general-purpose junk drawer.

## What Filing Cabinet Is Not

Filing Cabinet is intentionally narrow.

It is:

- not cloud storage
- not an enterprise document management system
- not a filesystem replacement
- not an automatic inference workspace
- not a generalized productivity suite

This boundary is part of the design.

Filing Cabinet becomes stronger when it remains focused on preservation, context, verification, recovery, and operator control.

## Practical Design Principles

Filing Cabinet should continue to follow these principles:

1. Preserve context while it is still fresh.
2. Keep retained files understandable without a remote service.
3. Prefer operator-authored meaning over automatic inference.
4. Make important state transitions deterministic and inspectable.
5. Preserve historical metadata instead of silently rewriting it.
6. Separate retained source files from generated supporting assets.
7. Analyze before mutating vault state.
8. Make destructive operations explicit.
9. Keep CLI behavior predictable and script-friendly.
10. Avoid expanding into workflows that belong to the normal filesystem or a general productivity suite.

The practical test is simple.

An operator should be able to return to the vault later and answer:

- What is this?
- Where did it come from?
- Why was it retained?
- Can I trust it?
- Has it changed?
- What is related to it?
- What repairs or recovery actions have occurred?
- How can it be exported or restored?

When those answers are available without guesswork, Filing Cabinet is doing its job.
