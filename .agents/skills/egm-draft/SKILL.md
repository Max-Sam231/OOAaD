---
name: egm-draft
description: >-
  Step-by-step authoring of a technical spec in docs/designs/<slug>.md from
  .dev/egm/concepts_brief.md and recorded decisions in docs/DECISIONS.md. Produces
  architecture, Create/Modify file list, and a 100%-coverage test plan.
---

# `egm-draft` — Spec Authoring (`docs/designs/<slug>.md`)

Turns approved decisions in [`docs/DECISIONS.md`](docs/DECISIONS.md) and `.dev/egm/concepts_brief.md` into an unambiguous implementation plan for `csharp-developer`.

## Hard Rules
1. **Artifact first:** keep the working draft in `.dev/egm/design_draft.md`; never dump the full spec into chat — share a brief section summary (2–3 key decisions) and a link for sign-off.
2. **Zero praise or filler:** point out weak spots and check compliance with [`.agents/rules/ooand-invariants.md`](.agents/rules/ooand-invariants.md) (`INV-1..INV-9`).
3. **No production code (`NO CODE`):** only `-ing` interface signatures, `IoC` string keys, data schemas, and test case lists belong in the spec — not implementation bodies.
4. **Zero-Decision readiness:** by Step 5, `csharp-developer` must have zero remaining open design choices.

## Step-by-Step Process

### Step 1. Gather Input Context
1. Check in order: (a) user-provided file; (b) `.dev/egm/concepts_brief.md`; (c) linked `D-NNN` entries in [`docs/DECISIONS.md`](docs/DECISIONS.md). If context is missing, propose starting with `egm-brainstorm` or ask clarifying questions in chat.
2. Cross-check [`.memory-bank/context.md`](.memory-bank/context.md) and current `.cs` files in `src/`/`tests/` so the spec never cites non-existent code as existing.

### Steps 2–4. Incrementally Author `.dev/egm/design_draft.md`
Write and align each section sequentially (after updating `.dev/egm/design_draft.md`, summarize key choices in chat and wait for user confirmation: "next" or edits):

* **Section 1 — Context, Problem, Goals & Non-Goals:**
  * References to `D-NNN` entries in `docs/DECISIONS.md`.
  * Problem statement, measurable Goals, and explicit Non-Goals (scope boundaries).
  * Core scenarios (`Given → When → Then`), including boundary failures.
* **Section 2 — Architecture & Alternatives:**
  * Component design, `-ing` interfaces (`INV-5`), `IoC.Register`/`Resolve` keys (`INV-1`), scope boundaries (`INV-7`), and OCP compliance (`INV-3` — new classes instead of modifying old ones).
  * **Alternatives Considered:** why rejected options were discarded (linking `D-NNN` and `concepts_brief.md`).
  * **Risks & Constraints:** race conditions, command-graph cycles, compilation/validation failures.
* **Section 3 — `## Detailed Implementation Plan` & Verification:**
  * Explicit list of **every** file (`Create` or `Modify`) in `src/` and `tests/` with exact class names, signatures, and behavioral contracts.
  * **100% Coverage xUnit Test Plan (`line = 100%`, `branch = 100%`, `INV-8`):** explicit cases covering Happy path, every `if`/error/exception branch, scope isolation, and concurrency (with zero dead branches).
  * Verification commands: `python3 .agents/hooks/ooand-lint-gate.py --check-all .`, `dotnet format --verify-no-changes`, `dotnet build --no-incremental`, `dotnet test --collect:"XPlat Code Coverage"`.

### Step 5. Reconcile & Publish to `docs/designs/<slug>.md`
1. **Reconcile with brief and `DECISIONS.md`:** re-read `.dev/egm/concepts_brief.md` and `docs/DECISIONS.md` — list any divergence (if the draft changes a decision or introduces a new `IoC` key/dependency, get explicit user approval and append a new `D-NNN` entry to `docs/DECISIONS.md`).
2. Save the finalized spec to `docs/designs/<slug>.md` (this unlocks `.cs` edits in `guard-gate`).
3. Share the link to `docs/designs/<slug>.md` and offer to run an independent spec critique via `egm-review` before coding.
