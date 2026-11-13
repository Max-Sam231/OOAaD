#!/usr/bin/env python3
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

HARD_DENY_CMD_PATTERNS = [
    (r"\brm\s+-[a-zA-Z]*r[a-zA-Z]*f\b", "rm -rf"),
    (r"\brm\s+-[a-zA-Z]*f[a-zA-Z]*r\b", "rm -fr"),
    (r"\bfind\b.*\s-(delete|exec\s+rm|execdir\s+rm|ok\b|okdir\b)", "find delete/exec"),
    (r"\bxargs\b.*\brm\b", "xargs rm"),
    (r"\bgit\s+reset\s+--hard\b", "git reset --hard"),
    (r"\bgit\s+push\b.*(\s--force\b|\s--force-with-lease\b|\s-f\b)", "git push --force"),
    (r"\bgit\s+clean\s+-[a-zA-Z]*f", "git clean -f"),
    (r"\bgit\s+checkout\s+(\.|--\s+\.)(\s|$)", "git checkout ."),
    (r"\bgit\s+restore\s+.*(\.|--staged\s+\.)(\s|$)", "git restore ."),
    (r"\bgit\s+branch\s+-D\b", "git branch -D"),
    (r"\bgit\s+add\s+(-A\b|--all\b|\.(\s|$))", "blanket staging git add . / -A is forbidden (AGENTS.md §3)"),
    (r"--no-verify\b|--no-gpg-sign\b", "git hook bypass (--no-verify)"),
    (r"\b(curl|wget)\b.*\|\s*(sh|bash|zsh)\b", "curl/wget | sh"),
    (
        r"\b(shutil\.rmtree|os\.(?:remove|unlink|rmdir)|fs\.(?:rmSync|unlinkSync)|Path\([^)]*\)\.unlink)\b",
        "file deletion via inline script",
    ),
    (r"ghp_[a-zA-Z0-9]{36}|sk-[a-zA-Z0-9]{48}|ygmcp_[a-zA-Z0-9_-]{30,}", "plaintext token in CLI arguments"),
]

FORCE_ASK_CMD_PATTERNS = [
    (r"\bgit\s+commit\b", "git commit is allowed only upon explicit user request (AGENTS.md §3)"),
    (r"\bgit\s+push\b", "git push is allowed only upon explicit user request (AGENTS.md §3)"),
    (r"\bdotnet\s+add\b.*\bpackage\b", "adding a new NuGet package requires user confirmation (AGENTS.md §2)"),
    (r"(^|[;&|]\s*)rm\s+", "deleting files via rm requires user confirmation"),
    (r"(^|[;&|]\s*)sudo\s+", "sudo requires user confirmation"),
    (r"\bdocker\s+rm\b", "docker rm requires user confirmation"),
]


def emit(decision: str, reason: str = "") -> None:
    out = {"decision": decision}
    if reason:
        out["reason"] = reason
    print(json.dumps(out, ensure_ascii=False))
    sys.exit(0)


def check_command(cmd: str) -> None:
    if not cmd:
        emit("allow")
    for pattern, desc in HARD_DENY_CMD_PATTERNS:
        if re.search(pattern, cmd, re.IGNORECASE):
            emit("deny", f"guard-gate: blocked forbidden operation ({desc}).")
    for pattern, desc in FORCE_ASK_CMD_PATTERNS:
        if re.search(pattern, cmd, re.IGNORECASE):
            emit("force_ask", f"guard-gate: {desc}.")
    emit("allow")


def extract_decision_ids(text: str) -> set[str]:
    return set(re.findall(r"^##\s+(D-\d+)\b", text, flags=re.MULTILINE))


def has_recorded_decision() -> bool:
    dec = ROOT / "docs" / "DECISIONS.md"
    if not dec.is_file():
        return False
    return len(extract_decision_ids(dec.read_text(encoding="utf-8", errors="ignore"))) > 0


def has_approved_spec() -> bool:
    designs_dir = ROOT / "docs" / "designs"
    if not designs_dir.is_dir():
        return False
    for md in designs_dir.rglob("*.md"):
        if md.name.upper() in {"README.MD", "SITEMAP.MD"}:
            continue
        if md.read_text(encoding="utf-8", errors="ignore").strip():
            return True
    return False


def is_gated_cs(rel: str) -> bool:
    if not rel.endswith(".cs"):
        return False
    parts = rel.split("/")
    if parts[0] in {".dev", "контекст"}:
        return False
    return not any(p.lower() in {"bin", "obj"} for p in parts[:-1])


def check_file_edit(tool_name: str, args: dict) -> None:
    raw_path = args.get("Path") or args.get("TargetFile") or args.get("file_path") or ""
    if not raw_path:
        emit("allow")

    p = Path(raw_path)
    target = (ROOT / p).resolve() if not p.is_absolute() else p.resolve()
    try:
        rel = target.relative_to(ROOT).as_posix()
    except ValueError:
        emit("allow")

    if (
        rel == "AGENTS.md"
        or rel.startswith(".agents/")
        or rel == ".github/workflows/ci.yaml"
        or rel.startswith("scripts/githooks/")
    ):
        emit(
            "force_ask",
            f"guard-gate: modifying protected harness file ({rel}) requires explicit human approval.",
        )

    if rel == "docs/DECISIONS.md" and target.is_file():
        existing_ids = extract_decision_ids(target.read_text(encoding="utf-8", errors="ignore"))
        if tool_name == "write_file" and not args.get("Append", False):
            new_ids = extract_decision_ids(args.get("Content") or "")
            missing = sorted(existing_ids - new_ids)
            if missing:
                emit(
                    "deny",
                    f"guard-gate (INV-9): docs/DECISIONS.md is append-only. Attempted to delete entries {', '.join(missing)}.",
                )
        elif tool_name in {"edit_file", "replace_file_content"}:
            removed_ids = extract_decision_ids(args.get("Target") or args.get("TargetContent") or "")
            kept_ids = extract_decision_ids(args.get("Replacement") or args.get("ReplacementContent") or "")
            missing = sorted(removed_ids - kept_ids)
            if missing:
                emit(
                    "deny",
                    f"guard-gate (INV-9): docs/DECISIONS.md is append-only. Attempted to delete entries {', '.join(missing)}.",
                )
        elif tool_name == "multi_replace_file_content":
            for chunk in args.get("ReplacementChunks") or []:
                removed_ids = extract_decision_ids(chunk.get("TargetContent") or "")
                kept_ids = extract_decision_ids(chunk.get("ReplacementContent") or "")
                missing = sorted(removed_ids - kept_ids)
                if missing:
                    emit(
                        "deny",
                        f"guard-gate (INV-9): docs/DECISIONS.md is append-only. Attempted to delete entries {', '.join(missing)}.",
                    )

    if is_gated_cs(rel):
        if not has_recorded_decision() or not has_approved_spec():
            emit(
                "deny",
                "guard-gate: C# edits are blocked. First record decision D-NNN in docs/DECISIONS.md and create a spec in docs/designs/<slug>.md.",
            )

    emit("allow")


def main() -> None:
    raw = sys.stdin.read()
    if not raw.strip():
        emit("allow")
    try:
        payload = json.loads(raw)
    except Exception:
        emit("allow")

    tool_call = payload.get("toolCall") or {}
    tool_name = (tool_call.get("name") or payload.get("toolName") or "").lower()
    args = tool_call.get("args") or payload.get("toolArgs") or {}

    if tool_name == "run_command":
        check_command(args.get("CommandLine") or args.get("command") or "")
    elif tool_name in {"write_file", "edit_file", "replace_file_content", "multi_replace_file_content"}:
        check_file_edit(tool_name, args)
    else:
        emit("allow")


if __name__ == "__main__":
    main()
