---
name: egm-brainstorm
description: >-
  Explore a new feature or lab prompt (when no fixed spec exists) using the
  Elephant & Goldfish Model (EGM): clarifying questions, parallel clean-context
  stress test across 4 Goldfish personas, recording architectural forks in
  docs/DECISIONS.md, and preparing .dev/egm/concepts_brief.md for egm-draft.
---

# `egm-brainstorm` — Prompt Exploration & Design Without a Fixed Spec

Turns a rough idea, whiteboard photo, or lab prompt into a stress-tested concept (`.dev/egm/concepts_brief.md`) and recorded `D-NNN` decisions in [`docs/DECISIONS.md`](docs/DECISIONS.md).

## Hard Rules (CRITICAL)
1. **Artifact first:** never dump long concepts or walls of text into chat — write summaries into `.dev/egm/concepts_brief.md` and link it.
2. **Challenge by default:** first attack weak spots, conflicts with [`.agents/rules/ooand-invariants.md`](.agents/rules/ooand-invariants.md) (`INV-1..INV-9`), hidden scope races, and over-engineering. Zero flattery, praise, or routine apologies.
3. **Owner chooses architecture:** when a fork has 2–3 viable options (`IoC` key, data structure, graph sort timing, error-handling strategy), present up to 3 options with mechanism, pros/cons, and failure modes — the user picks.
4. **Direct Goldfish spawn (no nested subagents):** in Antigravity, subagents do not spawn subagents, so launch all 4 Goldfish personas directly and in parallel via `invoke_subagent` strictly **after** the user confirms the working hypothesis.

## Step-by-Step Process

### Step 1. Deep Problem Clarification (Chat Dialog)
1. Read [`.agents/rules/ooand-invariants.md`](.agents/rules/ooand-invariants.md), [`.memory-bank/context.md`](.memory-bank/context.md), and the latest entries in [`docs/DECISIONS.md`](docs/DECISIONS.md).
2. Ask targeted **open-ended** questions in chat (without leading the user) until clear:
   * What game/system mechanic is being added, and which scope (`RootScope`, game scope) owns it?
   * Which external boundaries (rule DSL input, commands, exceptions) require validation, and what trusts internal `IoC` contracts?
   * What is explicitly out of scope for this iteration (`Non-Goals`)?
   * Which edge cases exist (`A before B` graph cycles, unknown entity, empty input, concurrent scopes)?
3. Once clear, state a 2–4 sentence working hypothesis and ask the user in chat whether to proceed to the parallel Goldfish stress test or refine further.

### Step 2. Parallel 4-Lens Goldfish Stress Test (`Mode: BRAINSTORM`)
After user confirmation, invoke all 4 personas in parallel via `invoke_subagent` in clean contexts (passing **only** the working hypothesis, `Mode: BRAINSTORM`, and instructions to verify against `.agents/rules/ooand-invariants.md` and `.memory-bank/context.md`):
1. `egm-zoom-out` (`Role: "YAGNI & Reuse Goldfish"`) — cuts redundant abstractions and checks reuse of `IoC` / `Common/*.dll`.
2. `egm-design` (`Role: "Design Critic Goldfish"`) — hunts races, `INV-1..INV-9` violations, cycles, and failure modes.
3. `egm-comprehension` (`Role: "Teammate Clarity Goldfish"`) — evaluates clarity of contracts (`-ing` interfaces, `IoC` keys, errors) for the entire team.
4. `egm-implementation-readiness` (`Role: "Implementation Fit Goldfish"`) — checks code integration, `core.dll`/`scopes.dll` contracts, and risks of uncoverable branches (`100%`).

Synthesize and deduplicate findings into `.dev/egm/concepts_brief.md` using this structure:
```markdown
# <Feature Title>
* **Date:** YYYY-MM-DD

## Summary & Boundaries (Goals / Non-Goals)
## Key Goldfish Findings & Open Forks
## Evaluated Alternatives (Option / Pros / Cons / Chosen or Rejected Rationale)
## Proposed D-NNN Entries for docs/DECISIONS.md
```

### Step 3. Resolve Forks & Record in `docs/DECISIONS.md`
1. Share a link to `.dev/egm/concepts_brief.md` and 2–4 main forks/objections from the Goldfish in chat.
2. Wait for the user's choice on each fork (follow up if needed).
3. Append accepted `D-NNN` entries to the end of the **append-only** [`docs/DECISIONS.md`](docs/DECISIONS.md) log (`INV-9`).

### Step 4. Transition to Spec
Ask the user whether to proceed to drafting the formal spec via `egm-draft` (which picks up `.dev/egm/concepts_brief.md` and fresh `D-NNN` entries from `docs/DECISIONS.md`).
