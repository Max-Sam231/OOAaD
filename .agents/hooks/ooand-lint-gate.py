#!/usr/bin/env python3
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PENDING_FILE = ROOT / ".dev" / ".lint-pending.txt"

ALLOWED_NEW_TYPES = {
    "Vector",
    "Angle",
    "Object",
    "List",
    "Dictionary",
    "ConcurrentDictionary",
    "Queue",
    "ConcurrentQueue",
    "Stack",
    "HashSet",
    "StringBuilder",
    "MemoryStream",
    "CancellationTokenSource",
    "ManualResetEventSlim",
    "SemaphoreSlim",
    "AutoResetEvent",
    "ReaderWriterLockSlim",
    "Guid",
    "TimeSpan",
}

INFRA_FILE_MARKERS = (
    "Register",
    "Factory",
    "Compiler",
    "Generator",
    "Builder",
    "Init",
    "Bootstrap",
)


def strip_strings_and_find_comments(source: str) -> tuple[str, list[tuple[int, str]]]:
    out: list[str] = []
    comments: list[tuple[int, str]] = []
    interp_stack: list[dict[str, int | str]] = []
    active_str: dict[str, int | str] | None = None
    i = 0
    n = len(source)
    line = 1

    while i < n:
        if active_str is not None:
            kind = str(active_str["kind"])
            dollars = int(active_str["dollars"])
            quotes = int(active_str["quotes"])

            if kind == "raw":
                closing = '"' * quotes
                if source.startswith(closing, i):
                    end_q = i
                    while end_q < n and source[end_q] == '"':
                        end_q += 1
                    span = end_q - i
                    out.extend(" " * span)
                    i = end_q
                    active_str = None
                    continue
                if dollars > 0 and source[i] == "{":
                    b_cnt = 0
                    while i + b_cnt < n and source[i + b_cnt] == "{":
                        b_cnt += 1
                    if b_cnt >= 2 * dollars:
                        out.extend(" " * (2 * dollars))
                        i += 2 * dollars
                        continue
                    if b_cnt >= dollars:
                        extra = b_cnt - dollars
                        if extra > 0:
                            out.extend(" " * extra)
                            i += extra
                        out.append("{")
                        out.extend(" " * (dollars - 1))
                        i += dollars
                        active_str["brace_depth"] = 1
                        interp_stack.append(active_str)
                        active_str = None
                        continue
                if source[i] == "\n":
                    line += 1
                    out.append("\n")
                else:
                    out.append(" ")
                i += 1
                continue

            if kind == "verbatim":
                if source.startswith('""', i):
                    out.extend("  ")
                    i += 2
                    continue
                if source[i] == '"':
                    out.append(" ")
                    i += 1
                    active_str = None
                    continue
                if dollars > 0:
                    if source.startswith("{{", i) or source.startswith("}}", i):
                        out.extend("  ")
                        i += 2
                        continue
                    if source[i] == "{":
                        out.append("{")
                        i += 1
                        active_str["brace_depth"] = 1
                        interp_stack.append(active_str)
                        active_str = None
                        continue
                if source[i] == "\n":
                    line += 1
                    out.append("\n")
                else:
                    out.append(" ")
                i += 1
                continue

            if source[i] == "\\" and i + 1 < n:
                out.extend("  ")
                i += 2
                continue
            if source[i] == '"':
                out.append(" ")
                i += 1
                active_str = None
                continue
            if dollars > 0:
                if source.startswith("{{", i) or source.startswith("}}", i):
                    out.extend("  ")
                    i += 2
                    continue
                if source[i] == "{":
                    out.append("{")
                    i += 1
                    active_str["brace_depth"] = 1
                    interp_stack.append(active_str)
                    active_str = None
                    continue
            if source[i] == "\n":
                line += 1
                out.append("\n")
            else:
                out.append(" ")
            i += 1
            continue

        if source.startswith("//", i):
            comments.append((line, "INV-8: single-line comment '//' is forbidden in C# code"))
            while i < n and source[i] != "\n":
                out.append(" ")
                i += 1
            continue

        if source.startswith("/*", i):
            comments.append((line, "INV-8: block comment '/* ... */' is forbidden in C# code"))
            out.extend("  ")
            i += 2
            while i < n and not source.startswith("*/", i):
                if source[i] == "\n":
                    line += 1
                    out.append("\n")
                else:
                    out.append(" ")
                i += 1
            if i < n:
                out.extend("  ")
                i += 2
            continue

        if source[i] in ("$", "@", '"'):
            j = i
            dollars = 0
            ats = 0
            while j < n and source[j] in ("$", "@"):
                if source[j] == "$":
                    dollars += 1
                else:
                    ats += 1
                j += 1
            if j < n and source[j] == '"' and ats <= 1 and not (ats == 1 and dollars > 1):
                q_cnt = 0
                while j + q_cnt < n and source[j + q_cnt] == '"':
                    q_cnt += 1
                if q_cnt >= 3 and ats == 0:
                    prefix_len = (j - i) + q_cnt
                    out.extend(" " * prefix_len)
                    i += prefix_len
                    active_str = {"kind": "raw", "dollars": dollars, "quotes": q_cnt, "brace_depth": 0}
                    continue
                if ats == 1:
                    prefix_len = (j - i) + 1
                    out.extend(" " * prefix_len)
                    i += prefix_len
                    active_str = {"kind": "verbatim", "dollars": dollars, "quotes": 1, "brace_depth": 0}
                    continue
                if q_cnt == 2 and dollars == 0:
                    out.extend("  ")
                    i += 2
                    continue
                prefix_len = (j - i) + 1
                out.extend(" " * prefix_len)
                i += prefix_len
                active_str = {"kind": "regular", "dollars": dollars, "quotes": 1, "brace_depth": 0}
                continue

        if source[i] == "'":
            out.append(" ")
            i += 1
            while i < n:
                if source[i] == "\\" and i + 1 < n:
                    out.extend("  ")
                    i += 2
                elif source[i] == "'":
                    out.append(" ")
                    i += 1
                    break
                elif source[i] == "\n":
                    line += 1
                    out.append("\n")
                    i += 1
                else:
                    out.append(" ")
                    i += 1
            continue

        ch = source[i]
        if ch == "\n":
            line += 1
            out.append("\n")
            i += 1
            continue

        if ch == "{" and interp_stack:
            interp_stack[-1]["brace_depth"] = int(interp_stack[-1]["brace_depth"]) + 1
            out.append("{")
            i += 1
            continue

        if ch == "}" and interp_stack:
            interp_stack[-1]["brace_depth"] = int(interp_stack[-1]["brace_depth"]) - 1
            out.append("}")
            i += 1
            if int(interp_stack[-1]["brace_depth"]) == 0:
                active_str = interp_stack.pop()
            continue

        out.append(ch)
        i += 1

    return "".join(out), comments


def is_ignored_cs(rel: str) -> bool:
    parts = rel.split("/")
    if parts[0] in {".dev", ".git", "контекст"}:
        return True
    for p in parts:
        if p in {"bin", "obj"}:
            return True
    return False


def is_test_cs(rel: str) -> bool:
    parts = rel.split("/")
    if parts[0].lower() == "tests":
        return True
    return any(p.lower().endswith("tests") or p.lower().endswith(".test") for p in parts[:-1])


def lint_cs_file(path: Path, root: Path) -> list[str]:
    try:
        rel = path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return []

    if not rel.endswith(".cs") or is_ignored_cs(rel) or not path.is_file():
        return []

    raw = path.read_text(encoding="utf-8", errors="ignore")
    stripped, comment_hits = strip_strings_and_find_comments(raw)
    issues: list[str] = [f"{rel}:{ln} — {msg}" for ln, msg in comment_hits]

    if is_test_cs(rel):
        for idx, line_txt in enumerate(stripped.splitlines(), start=1):
            if re.search(r"\bSkip\s*=", line_txt):
                issues.append(f"{rel}:{idx} — INV-8: skipping tests via Skip is forbidden")
        return issues

    fname = path.name
    is_infra = any(m in fname for m in INFRA_FILE_MARKERS)
    is_factory_or_compiler = ("Factory" in fname) or ("Compiler" in fname)

    for idx, (orig_line, clean_line) in enumerate(
        zip(raw.splitlines(), stripped.splitlines()), start=1
    ):
        if re.search(r"\binterface\s+I[A-Za-z0-9_]*(?:able|eble)\b", clean_line):
            issues.append(
                f"{rel}:{idx} — INV-5: '-able' suffix in interface name is forbidden (use '-ing', e.g., IMovingObject)"
            )

        if re.search(r"\bswitch\s*[\(\{]", clean_line):
            issues.append(f"{rel}:{idx} — INV-2: 'switch' branching is forbidden in production code")

        if fname.endswith("Command.cs") and not is_infra:
            if re.search(r"\bIDictionary\s*<\s*string\s*,\s*object", clean_line):
                issues.append(
                    f"{rel}:{idx} — INV-2: direct IDictionary<string, object> access inside commands is forbidden (use an adapter)"
                )

        if fname == "Game.cs" and "IoC.Scope" in orig_line:
            issues.append(
                f"{rel}:{idx} — INV-7: Game must not reference IoC.Scope.* (use InSeparateScopeCommand)"
            )

        if not is_factory_or_compiler and re.search(r"\bActivator\s*\.\s*CreateInstance\b", clean_line):
            issues.append(
                f"{rel}:{idx} — INV-6: Activator.CreateInstance is forbidden outside one-time adapter factory initialization"
            )

        if not is_infra and not re.search(r"\bwhere\s+\w+\s*:\s*[^\{]*\bnew\s*\(\s*\)", clean_line):
            for m in re.finditer(r"\bnew\s+([A-Z][A-Za-z0-9_]*)\s*(?:<[^>]+>)?\s*\(", clean_line):
                prefix = clean_line[: m.start()]
                if re.search(r"\bthrow\s+$", prefix):
                    continue
                type_name = m.group(1)
                if type_name in ALLOWED_NEW_TYPES or type_name.endswith("Exception"):
                    continue
                issues.append(
                    f"{rel}:{idx} — INV-1: direct 'new {type_name}(...)' is forbidden in application code (resolve via Ioc.Resolve)"
                )

    return issues


def check_ocp_git_warnings(root: Path) -> list[str]:
    git_dir = root / ".git"
    if not git_dir.exists():
        return []
    proc = subprocess.run(
        ["git", "-C", str(root), "diff", "--name-status", "HEAD"],
        capture_output=True,
        text=True,
    )
    if proc.returncode != 0:
        return []
    warnings: list[str] = []
    for line in proc.stdout.splitlines():
        parts = line.split("\t")
        if len(parts) >= 2 and parts[0].startswith("M"):
            rel = parts[1]
            if (
                rel.endswith("Command.cs")
                and not is_ignored_cs(rel)
                and not is_test_cs(rel)
                and not any(m in Path(rel).name for m in INFRA_FILE_MARKERS)
            ):
                warnings.append(
                    f"{rel}:1 — OCP-WARN (INV-3): existing command class modified (status M). Verify rule extension does not belong in a new command class."
                )
    return warnings


def run_check_all(root: Path) -> int:
    hard_issues: list[str] = []
    for cs in sorted(root.rglob("*.cs")):
        hard_issues.extend(lint_cs_file(cs, root))
    ocp_warnings = check_ocp_git_warnings(root)

    for item in hard_issues:
        print(f"[ERROR] {item}")
    for warn in ocp_warnings:
        print(f"[WARN]  {warn}")

    return 1 if hard_issues else 0


def handle_hook() -> None:
    raw = sys.stdin.read()
    if not raw.strip():
        print("{}")
        return
    try:
        payload = json.loads(raw)
    except Exception:
        print("{}")
        return

    if "toolCall" in payload:
        if not payload.get("error"):
            args = (payload.get("toolCall") or {}).get("args") or {}
            raw_path = args.get("Path") or args.get("TargetFile") or args.get("file_path") or ""
            if raw_path:
                p = Path(raw_path)
                target = (ROOT / p).resolve() if not p.is_absolute() else p.resolve()
                issues = lint_cs_file(target, ROOT)
                if issues:
                    PENDING_FILE.parent.mkdir(parents=True, exist_ok=True)
                    existing = (
                        PENDING_FILE.read_text(encoding="utf-8", errors="ignore")
                        if PENDING_FILE.is_file()
                        else ""
                    )
                    merged = "\n".join(filter(None, [existing.strip(), "\n".join(issues)]))
                    PENDING_FILE.write_text(merged + "\n", encoding="utf-8")
        print("{}")
        return

    if "invocationNum" in payload and PENDING_FILE.is_file():
        pending = PENDING_FILE.read_text(encoding="utf-8", errors="ignore").strip()
        PENDING_FILE.unlink(missing_ok=True)
        if pending:
            msg = (
                "ooand-lint-gate detected OOAnD invariant violations in modified .cs files. "
                "Fix them immediately before continuing:\n" + pending
            )
            print(
                json.dumps(
                    {
                        "injectSteps": [{"systemMessage": {"systemMessage": msg}}],
                        "terminationBehavior": "force_continue",
                    },
                    ensure_ascii=False,
                )
            )
            return

    print("{}")


def main() -> None:
    if len(sys.argv) >= 2 and sys.argv[1] == "--check-all":
        target_root = Path(sys.argv[2]).resolve() if len(sys.argv) >= 3 else ROOT
        sys.exit(run_check_all(target_root))
    handle_hook()


if __name__ == "__main__":
    main()
