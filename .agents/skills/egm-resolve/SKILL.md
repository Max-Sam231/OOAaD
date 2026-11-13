---
name: egm-resolve
description: >-
  Resolve Goldfish review findings (.dev/egm/goldfish_findings.md) in the
  author's context (Elephant): classify as PROPOSE FIX / CONTEST / ESCALATE /
  DECLINE, align with the user, and apply targeted edits to docs/designs/<slug>.md.
---

# `egm-resolve` — Triage & Apply Fixes After Goldfish Review

Runs in the main author context (Elephant — retains rationale behind decisions) to fairly separate genuine spec gaps from unfounded nitpicks and align fixes with the user.

## Inputs
* `TargetFile`: path to the reviewed spec `docs/designs/<slug>.md`.
* `FindingsFile`: `.dev/egm/goldfish_findings.md` (produced by `egm-review`).

## Step-by-Step Process

### Step 1. Classify Every Finding
Read `TargetFile` and `FindingsFile`. Assign exactly one category to each item:
* **`PROPOSE FIX`** — valid finding: write an exact surgical edit for `TargetFile`.
* **`CONTEST`** — the reviewer is mistaken, and refutation **already exists in the document or codebase** accessible to the reviewer (cite exact `file:line` or `D-NNN`). Never contest with "that's intentional"; **never contest a `comprehension` gap** (if a fresh reader could not understand the doc, explanation is genuinely missing).
* **`ESCALATE`** — conflicts with an unstated agreement outside the files or changes an architectural decision (requires user choice and possibly a new `D-NNN` entry in `docs/DECISIONS.md`).
* **`DECLINE`** — minor `Nit` with no impact on implementation or clarity.

Do not bloat the spec with robotic noise if human teammates do not need it — use `ESCALATE` on borderline readability vs conciseness calls.

### Step 2. Write `.dev/egm/goldfish_review_report.md`
Generate a structured report covering every finding (`ID`, `Reviewer`, `Severity`, `Scope`, `AI Suggestion`, `Justification`, `Proposed Edit`) and save it to `.dev/egm/goldfish_review_report.md`.

### Step 3. Align with the User
If any `ESCALATE` items exist, list them in chat first. Link `.dev/egm/goldfish_review_report.md` and offer in chat:
1. **`auto accept`** — apply all `PROPOSE FIX` edits (skipping `CONTEST`/`DECLINE`; awaiting your decision on `ESCALATE`).
2. **`interactive`** — resolve disputed items one by one in chat.
3. **`continue`** — leave the spec as-is.

**Stop and wait for the user's answer** (never edit `TargetFile` before confirmation).

### Step 4. Apply Edits & Enforce Iteration Cap (Circuit Breaker)
1. After confirmation, apply surgical updates to `TargetFile` (`edit_file`). If any edit alters an architectural decision, append a new `D-NNN` entry to [`docs/DECISIONS.md`](docs/DECISIONS.md).
2. If needed, run **at most 1 re-review pass** on modified sections via `egm-review` (max 2 spec revision rounds total). If non-blocking `Major`/`Nit` disagreements remain, record the explicit trade-off for user decision.
3. Once zero `Blocker` items remain, the spec is ready for TDD implementation (`tdd-workflow` + `csharp-developer`).
