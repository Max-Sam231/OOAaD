---
name: egm-review
description: >-
  Independent review of a completed spec docs/designs/<slug>.md using Goldfish
  personas (egm-zoom-out, egm-comprehension, egm-design,
  egm-implementation-readiness) in clean isolated contexts without author chat history.
---

# `egm-review` — Clean-Context Goldfish Spec Review

Audits `docs/designs/<slug>.md` through independent reviewers with zero memory of how the author arrived at it. Catches over-engineering, hidden assumptions, and missing details **before** coding begins.

## Hard Rules
1. **Strict Goldfish isolation:** each reviewer is spawned directly via `invoke_subagent` and receives **only** the spec path, line range (for partial reviews), `Mode: REVIEW`, and instructions to verify against `.agents/rules/ooand-invariants.md` + `.memory-bank/context.md` + real repository code (for `egm-implementation-readiness`). Never pass the author's reasoning or chat history.
2. **Direct parallel execution:** invoke personas directly; if a subagent fails or errors, retry once, then record status `not run` (never treat a crashed reviewer as "0 findings").
3. **Artifact first:** never flood chat with raw reports — write findings to `.dev/egm/` and provide a concise summary with a link.

## Spec Split Convention
Locate the `## Detailed Implementation Plan` heading in `docs/designs/<slug>.md`:
* `FeatureRange` = lines above it (Problem, Goals, Architecture, Alternatives, Risks).
* `DetailedRange` = lines from that heading to EOF (`Create`/`Modify` files and 100% Test Plan).

## Run Modes (ask the user in chat before starting unless specified)
1. **Consolidated run (`consolidated`, default recommendation)** — runs all 4 lenses in parallel without pauses and compiles `.dev/egm/goldfish_findings.md`.
2. **Interactive step-by-step (`interactive`)** — pauses after each lens to ask in chat.
3. **Focused (`feature_spec` or `implementation`)** — architecture only or code/test plan only.

## Execution Sequence (`Mode: REVIEW`)
1. **`egm-zoom-out`** (`Role: "Zoom Out Goldfish"`, `full file`) — YAGNI, simplicity, reuse of `IoC` / `Common/*.dll`, avoiding implementation weeds in architecture.
2. **`egm-comprehension`** (`Role: "Comprehension Goldfish"`, `full file`) — paraphrases the document in its own words (*What this proposes* / *How the current system works*) and flags unclear parts for teammates. **Always** preserve both paraphrase blocks in the report!
3. **`egm-design`** (`Role: "Design Critic Goldfish"`, `FeatureRange`) — alternatives, `INV-1..INV-9`, `DECISIONS.md`, scope races, cycles, and boundary failures.
4. **`egm-implementation-readiness`** (`Role: "Implementation Readiness Goldfish"`, `DetailedRange`) — Zero-Decision readiness for `csharp-developer`, checking `Create`/`Modify` against real files and `Common/*.dll`, and verifying 100% line/branch test sufficiency.

## Compiling `.dev/egm/goldfish_findings.md`
Aggregate all findings (with `egm-comprehension` paraphrases at the top), assign stable IDs (`zoom-1`, `design-1`, `impl-1`...), severities (`🔴 Blocker`, `🟡 Major`, `🟢 Nit`), and reviewer status (`completed` / `not run`).

After writing `.dev/egm/goldfish_findings.md`, share the link and severity summary in chat, then offer to triage and apply fixes via `egm-resolve`.
