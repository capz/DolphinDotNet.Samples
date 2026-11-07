# Large GameCube ISO defragmentation — implementation plan

**Status:** Approved scope for implementation; not a claim that these features ship today.  
**Target:** Full-size GameCube GCM/ISO images (normally **1,459,978,240 bytes**) on FAT32/exFAT SD media.  
**Ownership:** All SDK/runtime, allocator, filesystem, journal and native test implementation belongs in [capz/DolphinDotNet](https://github.com/capz/DolphinDotNet). This Samples repository owns this plan, **headless acceptance scenarios** and **API-consumer examples only**. No low-level SDK code belongs here.

## Baseline: do not reimplement

The merged DolphinDotNet storage feature already exposes `Dolphin.IO.Defrag.OptIn`, `MinimumFileSize`, `BeginScope`, `Analyze`, `Optimize`, `GetStatistics`/`ResetStatistics`, `WriteContiguousFrom`, plus `Storage.CreateContiguousFile`. Native code checks FAT32/exFAT extent counts and supports verified, journaled create-new allocation and selective safe-copy repair. `StorageIntegrity` already exercises 1 MiB benchmarks and a power-interruption rehearsal.

These are **not** a volume planner. Missing capabilities are free-space extent maps, file-owner/cluster indexing, allocation preflight, 1.36-GiB progress/resume, capacity-aware relocation planning, cross-device evacuation and restoration, and transaction recovery spanning two physical devices.

## User-visible goal

Given `gcloader:/games/example.iso` (or `sd:/`, `carda:/`, `cardb:/`) and optional independently mounted scratch media, **determine whether a one-extent ISO is achievable before any destructive operation**, present a deterministic dry-run plan, and execute with bounded RAM and recoverable, verified copies. The planner selects:

1. **Direct verified copy** if a single free physical cluster interval on the destination can hold the ISO *while the source remains intact*.
2. **ISO evacuation** to a second physical device if target free space cannot hold source plus staging: copy and verify the ISO to scratch; delete the source only after durable cross-volume recovery metadata exists; reserve a new contiguous target allocation, restore and verify the ISO.
3. **Minimal obstruction evacuation** when sufficient total free clusters exist but no suitable contiguous run: identify which *movable files* occupy the best candidate interval, evacuate only those files to scratch, release their destination allocation, verify the new free interval, relocate the ISO, and restore evacuated files with verified contiguous allocation where feasible.
4. **External full rebuild** when incremental evacuation is not feasible: safely stage every required file on a sufficiently large second device or a PC-managed card, then rebuild target data in allocation-aware order.
5. **Optional experimental in-place cluster mover** only as a separate, disabled-by-default last phase. This must not be used by normal `Execute` and requires its own raw FAT/exFAT metadata engine, journal protocol and physical acceptance gate.

An SD Gecko (A/B), SD2SP2 and a firmware-supported **writable** GC Loader raw SD device may all serve as destination or scratch if they are verified to be distinct physical volumes. The planner must not assume GC Loader simultaneous ODE/DVD operation, mounting or writes are supported.

## Safety invariants (non-negotiable)

- **No silent raw metadata writes:** the production path relies on filesystem-managed writes, reservation, checked physical extents and rename; the experimental mover, if ever built, has a separately reviewed raw-metadata boundary.
- **Always keep one verified surviving copy.** Never delete the source until a complete scratch copy has been synced, hashed, reopened, validated, and committed to recoverable transaction state. Never delete a scratch copy until the corresponding destination has been verified.
- **Cross-device operations are not atomic.** Persist a versioned transaction record on every participating writable volume *before* destructive transitions. The record contains operation ID, canonical path, stable volume fingerprints, lengths, checksums, stage names, metadata, checkpoint and sequence number. Detect and reconcile missing/old/conflicting partners; if scratch is absent, return `NeedsScratchMedia` without modifying the remaining authoritative copy.
- **Protect unrelated files.** No broad deletes, no arbitrary journal-provided paths, no unverified overwrite, no reuse of stale stage artifacts. Restrict staging names to deterministic internal paths and guard collisions. Preserve FAT/exFAT attributes/timestamps where representable; explicitly report lost or unsupported creation metadata.
- **Guard mounted ISO and active handles.** Unmount `dvd:/` if the image backs it, reject live handles, and serialize mutations per physical volume. Do not touch `dvd:/`, memory-card save files, reserved/system clusters, open files or directory structures.
- **Revalidate at every transition.** Verify device identity, read-only/writable capability, cluster size, media generation/partition signature, plan inputs, available space and occupied/free cluster layout. Any stale plan becomes `PlanInvalidated` rather than blindly executing.
- **RAM remains bounded** (target <= 64 KiB of transfer scratch plus small fixed metadata buffers); do not load a 1.36-GiB image or a full-volume cluster bitmap into managed RAM. The 1,459,978,240-byte image must remain strictly below existing signed 32-bit `off_t` limits; check size before every native cast and prevent arithmetic overflow.
- **Aligned I/O != contiguous allocation.** Write in driver-safe, sector-aligned buffers (e.g. 32/64 KiB, GameCube DMA alignment), but only report success when **physical FAT32/exFAT extents == 1**. Directory placement/cluster size may require padding of scratch buffers, not alteration of the ISO's logical byte length.
- **No absolute atomicity promise.** FAT/exFAT firmware/card flush semantics cannot guarantee survival from arbitrary power removal; use deterministic fault injection and explicit real-device acceptance.

## Architecture and API ownership

**DolphinDotNet SDK** should own:

- Read-only `VolumeAllocationScanner` over the mounted, cached partition: filesystem type, cluster geometry, total/free clusters, **largest contiguous free run**, free-run iterator and file-to-extent ownership maps. FAT32 uses FAT entries; exFAT uses the allocation bitmap (not FAT-only inference). Honor partition offsets, sector sizes and read error propagation. The scanner never writes sectors.
- Bounded, deterministic `RelocationPlanner` over immutable snapshots. Candidate intervals must exclude unmovable clusters; score a plan by *whole-file evacuation bytes*, number of moved files, scratch capacity, predicted I/O and copy count. Account for ISO source clusters potentially being freed by evacuation; a volume can have plenty of total free space but no contiguous window. The planner must choose between direct, ISO evacuation, blocker evacuation and full rebuild based on actual reachability, not a heuristic alone.
- `Defrag.Plan(path, scratchDevice?)` with immutable `DefragPlan`: strategy, byte/cluster requirements, largest run, source/target/scratch identifiers, ordered relocations, temporary peak capacity, estimated bytes read/written, risks and reasons a strategy is unavailable. Planning is **dry-run / read-only**.
- `Defrag.Execute(plan, options, progress, cancellation)` returning a structured operation result with per-file outcome, before/after extent counts, checksums, elapsed time, total bytes moved and recovery state. Keep streaming, bounded-buffer and cancellation semantics explicit; do not pretend a user callback is a reliable durability checkpoint.
- `Defrag.GetPendingOperations` / `Resume` / `Rollback` where safe, plus a read-only `InspectRecovery`. Every recovery transition is idempotent. Provide an explicit `NeedsScratchMedia` state rather than guessing about missing media.
- Shared native verified-copy/cross-device transaction utilities and **resumable staged copies**: progress checkpoints only after sync; per-chunk hashes or checksums with a final full-file cryptographic hash / authoritative manifest. A mismatch fails closed. Never resume into a published file.
- Volume locking, stable identity/fingerprints, device support probe, space checks, and fallback error outcomes. Small sub-2-GiB files remain supported; over-limit inputs fail preflight with a precise reason.
- A manual, separately authorized **experimental cluster-move interface**, deliberately excluded from the default feature or general execution API until independent review and power-cut matrix pass.

**Samples repository** should later add only a **headless** `StorageIntegrity` plan/execute validation scenario and documentation consuming public SDK APIs, once the APIs are released through NuGet. Keep existing BENCH/POWER behavior unchanged until then. Do **not** add a visual UI as part of this feature.

Proposed SDK shape (illustrative; finalize contracts in SDK PR before implementing):
```csharp
var plan = Defrag.Plan(
    "gcloader:/games/example.iso",
    scratchDevice: StorageDevice.SdSerialPort2);

if (plan.CanExecute)
{
    // Explicit decision: no implicit destructive execution during planning.
    var result = Defrag.Execute(plan, options, progress, cancellationToken);
}
// After reboot: Defrag.GetPendingOperations() / Resume(...) / Rollback(...).
```

## Phased delivery and commit boundaries

### P0 — Contracts, constraints, fixtures, and safety baseline

- [ ] Reconcile SDK `main` APIs with actual NuGet package consumed by Samples; avoid adding sample calls to unpublished APIs.
- [ ] Document `off_t`/exFAT limits, writable device matrix, source-backed `dvd:/`, minimum memory, metadata fidelity and recovery invariants.
- [ ] Add native allocation-map fixtures for FAT32/exFAT with physical sectors, multiple partitions, nondefault clusters, holes, malformed metadata, journal collisions and Unicode paths.
- [ ] Add a deterministic **1,459,978,240-byte sparse/virtual-data fixture** that does not allocate 1.36 GiB in CI.
- [ ] Commit baseline plan/contracts and tests independently of data mutation.

### P1 — Read-only free-space map and relocation planner (first executable SDK slice)

- [ ] Native FAT32 FAT-chain / free-run scanner and exFAT allocation-bitmap scanner; root/partition consistency checks. Public read-only stats.
- [ ] Read-only traversal mapping cluster extents to owning files and identifying nonmovable ranges, with 32-bit RAM constraints.
- [ ] Deterministic candidate-window selection and file-evacuation scoring; preflight device, capacity and mounted-disc guards.
- [ ] `Plan` returns direct/ISO-evacuation/blocker-evacuation/full-rebuild estimates or an explainable no-plan result. No writes.

### P2 — Direct relocation and large-file streaming/recovery

- [ ] Improve existing safe-copy path for full-size ISO with 16/32/64 KiB aligned chunk processing, physical extent verification and accurate progress.
- [ ] Avoid redundant full-file passes where safe; preserve end-to-end checksums, sync verification, metadata and recovery semantics.
- [ ] Correct cancellation, interrupted-transfer and staged-copy resume/rollback behavior; never publish partial ISO.
- [ ] Benchmark real 1.36-GiB files; log read/write throughput and write amplification.

### P3 — Two-device ISO evacuation / restoration

- [ ] Validate scratch/destination are **different physical devices**, mounted, writable, and have enough capacity. Reject GC Loader unsupported firmware and DVD conflicts.
- [ ] Versioned, checksummed, replicated cross-volume journal; crash injection for both media disappearing at every phase.
- [ ] Stage source ISO to scratch and verify; commit authority transition; delete source; reserve/rebuild contiguous destination; verify and publish; remove scratch only after durable completion.
- [ ] Resume after reboot, power loss, media reinsertion and unavailable scratch without data loss.

### P4 — Candidate-range obstruction evacuation

- [ ] Enumerate all files occupying chosen target interval; identify unmovable/unknown ownership and reject unsafe plans.
- [ ] Plan **minimal whole-file** offloads to scratch with ordered return/restoration and peak-space accounting; support ISO+blocker mixed cases.
- [ ] Evacuate/cross-check/commit/delete blockers; verify one contiguous target free run; move ISO; restore files with one-extent reservation where possible, with explicit warnings if not feasible.
- [ ] Commit staged progress per file; safe cancellation/recovery across all files and devices.

### P5 — External whole-card rebuild and acceptance

- [ ] External full rebuild mode for cases where P4 cannot create a run: backed-up target + verified second-device manifest; copy data back in cluster-aware order.
- [ ] Provide dry-run summary and explicit destructive authorization; preserve directory hierarchy, names, checksums, attributes and supported timestamps.
- [ ] Headless sample consumption using **published** SDK; sample documentation for SD Gecko A/B, SD2SP2 and GC Loader.
- [ ] Real hardware tests: FAT32/exFAT, 1.36 GiB, nearly full media, heterogeneous clusters, GC Loader firmware versions, power cuts and media removal.

### P6 — Experimental in-place cluster relocation (separate PR and release gate)

- [ ] Design and threat-model raw FAT32/exFAT FAT/bitmap updates, scratch-cluster ownership, chain consistency and reverse recovery.
- [ ] Simulate every torn write / checkpoint / metadata corruption with exhaustive deterministic host tests.
- [ ] Add a **separate opt-in experimental API**, not accessible via normal `Defrag.Execute`; release only after independent review and actual hardware acceptance. Otherwise keep explicitly deferred and report as unsupported.

## Acceptance matrix

| Scenario | Expected behavior |
|---|---|
| Destination has one free >= ISO cluster run | Direct contiguous stage and verify; original remains until commit |
| Total free > 1.36 GiB but largest run < ISO | Show blockers/evacuation plan; never claim direct success |
| No free run but scratch can hold complete ISO | ISO evacuated and verified before deletion; restored with one physical extent |
| Scratch can hold only a few small blockers | Choose minimal blocking files if it creates a viable run |
| Scratch device is absent, read-only, same physical card, too small or GC Loader cannot write | Explain unavailable strategy; no mutation |
| Source ISO backs mounted `dvd:/` | Refuse planning for execution / require unmount and revalidation |
| Missing scratch after power cut | `NeedsScratchMedia`; do not discard remaining authoritative copy |
| Device swapped after plan, unmount, open file, metadata change, stale allocation map | Plan invalidated, require fresh scan |
| Enforced power cut before/after copy, journal, delete, rename, restore | At least one verified surviving copy; idempotent recovery |
| FAT32 and exFAT (incl. Unicode long names), sectors/clusters/partition offsets | Correct read-only ownership and physical extent counts |
| Corrupted stage, journal, partial copy, mismatched checksum, disk full | Fail closed; no unrelated file deletion or silent success |
| Whole-card rebuild or in-place mode unavailable | Honest unsupported result; no hidden destructive fallback |
| Standard GameCube ISO 1,459,978,240 B | Streaming and counters stay within signed native 32-bit limits |

## CI and code review gates

1. Native host tests: free-run scanner/ownership; overlapping clusters and corrupt allocation maps; planner optimum/capacity checks; every journal checkpoint and repeat recovery.
2. Compiler/AOT smoke: managed plan/result contracts, zero-copy bounded streams, 32-bit PowerPC build, deterministic failure result mapping, performance and memory regressions.
3. Sample CI: no new SDK APIs before the pinned NuGet version exports them; test fixtures can be documented ahead of packaging.
4. Hardware acceptance is a separately signed-off manual gate. A green emulator/host CI run does **not** prove cross-volume power-loss safety.
5. No automatic merge of the SDK PR until all required checks pass; never auto-merge if no build pipeline exists.

## Cloud Codex agent handoff

**Workspace:** `capz/DolphinDotNet`, base `main`, create/use `feature/iso-defrag-planning-and-evacuation`; track the associated SDK PR.  
**Read first:** this plan, `docs/storage-runtime.md`, `source/storage.c`, `source/storage_gamecube.c`, `source/file_extents.c`, `source/file_optimize.c`, `source/contiguous_write.c`, `include/dnd_file_extents.h`, `include/dnd_file_optimize.h` and existing native/managed storage tests.  
**First implementation task:** deliver P1 read-only free-space and obstruction planning, with tests and commits per milestone; leave all destructive offload operations disabled until the P3 journal contract passes CI.  
**Branching:** keep SDK changes solely in the SDK PR. Do not modify Samples until the SDK API is actually released via NuGet.  
**Validation:** run portable native tests and relevant managed/GameCube CI after each phase; investigate failures and push minimal fixes; report measured hardware results separately.  
**Safety:** no implicit deletion, no raw FAT writes outside P6, no fake hardware validation and no merging without confirmed green checks.
