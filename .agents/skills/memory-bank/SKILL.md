---
name: memory-bank
description: >-
  Maintains an up-to-date project knowledge base in .memory-bank/ (architecture,
  stack, core.dll/scopes.dll contracts, conventions, and tasks). Updates memory
  files after meaningful changes or explicitly outputs NO_MEMORY_UPDATE.
---

# Memory Bank Steward — Maintaining `.memory-bank/`

Keeps long-term project knowledge current in `.memory-bank/` so any teammate or new agent session can onboard immediately.

## `.memory-bank/` Structure
While compact (under 100 lines), a single index file `.memory-bank/context.md` is sufficient. As modules grow, sections may be split into directories under an `.memory-bank/index.md` entrypoint:

```text
.memory-bank/
├── context.md                  # Compact starter index (or index.md as project grows)
├── product-overview/           # Domain overview, scenarios, constraints
├── steerings/                  # Coding and testing conventions
├── tech-details/               # IoC, scope, and Roslyn adapter contracts, modules
└── tasks/                      # Active and planned tasks
```

Whenever creating any new file inside `.memory-bank/`, always add a link to it in `context.md` / `index.md`.

## Phase 1 — Assess Whether an Update Is Needed
After completing a milestone or feature, classify change importance:

| Level | Meaning | Example | Action |
| :--- | :--- | :--- | :--- |
| **3 (Major)** | New module or architectural mechanism | Added Roslyn adapter factory compiler or rule parser | Update `.memory-bank/` |
| **2 (Moderate)** | New contract, interface, or `IoC` key | Added `Commands.*` / `Adapters.*` keys or updated backlog | Update `.memory-bank/` |
| **1 (Minor)** | Local refactor inside a method | Renamed a private local variable | Skip |
| **0 (Noise)** | Chat Q&A, typo fix, scratch draft | Discussion without architectural changes | Skip |

Update `.memory-bank/` **only at level ≥ 2**.

## Phase 2 — Update Rules
1. Always read the current `.memory-bank/context.md` (and affected files) first to avoid duplication or contradictions.
2. Write concisely and factually, with zero fluff or value judgments.
3. **Never delete or dilute** `Common/core.dll` / `scopes.dll` contracts or invariant links without explicit user instruction.
4. Never store secrets, tokens, or machine-local paths.

## Phase 3 — Explicit Opt-Out (`NO_MEMORY_UPDATE`)
If stable project knowledge did not change (level < 2), explicitly emit at the end of your response:

```text
NO_MEMORY_UPDATE
reason: "<brief single-sentence reason>"
```
