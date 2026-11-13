---
name: spec-review
description: >-
  Independent acceptance review of current code changes in a fresh isolated
  context via code-reviewer-qa, recording the verdict in .dev/.last-review.md
  for stop-verify-gate. Run before task completion once tests are green.
---

# `spec-review` — Clean-Context Diff Acceptance Review

Verifies current code changes against the spec `docs/designs/<slug>.md`, decision log `docs/DECISIONS.md`, and invariants `.agents/rules/ooand-invariants.md`, checks test honesty and 100% Coverlet coverage (`line = 100%`, `branch = 100%`), and writes `.dev/.last-review.md` for `stop-verify-gate`.

Review **must** execute in a separate clean context via `invoke_subagent` (`TypeName: "code-reviewer-qa"`) that did not author the code.

## When NOT to Invoke
When the session did not create or modify any `.cs` files (only brainstorms, `DECISIONS.md`, or specs in `docs/designs/`).

## Step-by-Step Protocol

### 1. Capture the Code Slice & Compute `reviewed_sha`
Compute the exact hash of current `.cs` changes (using the exact same algorithm as `stop-verify-gate`):

```bash
python3 .agents/hooks/stop-verify-gate.py --print-sha
```

If the output is `EMPTY`, no C# changes exist — stop this skill.

### 2. Invoke `code-reviewer-qa` in a Clean Context
Call `invoke_subagent` with:
* `TypeName`: `"code-reviewer-qa"`
* `Role`: `"Independent QA & Invariant Evaluator"`
* `Prompt`: pass **facts only, zero self-justifications or commentary**:
  1. Path to the active spec `docs/designs/<slug>.md`.
  2. Links to `.agents/rules/ooand-invariants.md` and `docs/DECISIONS.md`.
  3. List of affected files and instructions to run `git diff` (or inspect new files in `src/` and `tests/`), execute `python3 .agents/hooks/ooand-lint-gate.py --check-all .`, `dotnet build --no-incremental`, and `dotnet test --collect:"XPlat Code Coverage"`, verifying `line-rate == 1.0` and `branch-rate == 1.0`.
  4. Instructions to return YAML findings (`severity`, `category`, `file`, `line`, `cites`, `problem`, `suggested_fix`) and a final line `VERDICT: CLEAN` or `VERDICT: ISSUES_FOUND`.

### 3. Write `.dev/.last-review.md`
Create or overwrite `.dev/.last-review.md` using this exact header format (parsed by `stop-verify-gate.py`):

```yaml
verdict: CLEAN
reviewed_sha: <12-char sha from step 1>
reviewer: code-reviewer-qa
spec: docs/designs/<slug>.md
findings: <count of HIGH/MEDIUM findings>
```

Append the full `code-reviewer-qa` report below the header.

### 4. Handle the Verdict
* If `verdict: CLEAN` — update `.memory-bank/context.md` if needed (`memory-bank`) and complete the task.
* If `verdict: ISSUES_FOUND` — send findings to `csharp-developer` for remediation (respecting the max 2-round Circuit Breaker), recompute `reviewed_sha`, and re-run `spec-review`. Any `.cs` edit after review changes `reviewed_sha` and invalidates `.dev/.last-review.md`.
