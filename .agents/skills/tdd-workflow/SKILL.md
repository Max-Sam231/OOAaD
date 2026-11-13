---
name: tdd-workflow
description: >-
  Test-Driven Development discipline (RED → GREEN → REFACTOR) for C# / .NET 10
  in SpaceBattle with mandatory 100% line and branch coverage.
---

# TDD Workflow (RED → GREEN → REFACTOR)

> Failing test first, minimal production code second, clean refactoring third.

---

## 1. Three Laws of TDD & Cycle

1. Never write production code in `src/` before a failing xUnit test exists in `tests/`.
2. Write only enough test code to reproduce a failure (contract compile error or failing assertion).
3. Write only enough production code to pass the failing test while complying with `.agents/rules/ooand-invariants.md`.

Execution sequence:
1. **RED:** write a test for behavior/edge case → run `dotnet test` and confirm it fails for the expected reason.
2. **GREEN:** write minimal implementation via `App.ICommand` / `App.Ioc.Resolve` → run `dotnet test` and confirm all tests pass.
3. **REFACTOR:** remove duplication and clean structure without breaking OCP (`INV-3`) and without adding comments (`INV-8`).

---

## 2. SpaceBattle (OOAnD) Test Requirements

| Rule | Requirement |
| :--- | :--- |
| **Coverlet Coverage** | **100% line coverage (`line = 100%`) and 100% branch coverage (`branch = 100%`)**. Any uncovered `if`/`?:`/`??` branch blocks acceptance. |
| **AAA Structure** | Strictly Arrange → Act → Assert with zero comments (`// Arrange` violates `INV-8` — separate blocks with blank lines). |
| **Scope Isolation** | Initialize `new InitCommand().Execute()` and an isolated scope `Ioc.Resolve<object>("IoC.Scope.Create")` in each test class so parallel xUnit execution never leaks state across tests. |
| **Behavior Verification** | Mock narrow `-ing` interfaces (`IMovingObject`, `IRotatingObject`) via `NSubstitute` and assert observable outcomes (`Received`, exceptions) rather than private internals. |
| **Honesty** | `[Fact(Skip = ...)]`, empty `catch` blocks, and assertion-free tests are forbidden. |

---

## 3. Spec Test-Case Priority

1. **Happy path** — nominal execution of command / strategy / `IoC` dependency resolution.
2. **Failures & boundary conditions** — unreadable or unwritable adapter property, division by zero / zero vector, insufficient fuel, command-order graph cycles (`A before B`, `B before A`), unknown `IoC` key.
3. **Isolation & concurrency** — independence across game scopes and thread safety of `RootScope`.

> Reminder: commits after `REFACTOR` are **never** created automatically (`AGENTS.md` §3) — only upon explicit user instruction.
