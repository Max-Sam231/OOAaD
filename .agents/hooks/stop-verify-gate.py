#!/usr/bin/env python3
import hashlib
import json
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STATE_FILE = ROOT / ".dev" / ".stop-gate-state.json"
REVIEW_FILE = ROOT / ".dev" / ".last-review.md"
COV_DIR = ROOT / ".dev" / "stop-coverage"


def is_ignored_path(rel: str) -> bool:
    parts = rel.split("/")
    if parts[0] in {".dev", ".git", "контекст"}:
        return True
    return any(p in {"bin", "obj"} for p in parts)


def has_git_head(root: Path) -> bool:
    if not (root / ".git").exists():
        return False
    res = subprocess.run(
        ["git", "-C", str(root), "rev-parse", "--verify", "HEAD"],
        capture_output=True,
        text=True,
    )
    return res.returncode == 0


def compute_cs_state_sha(root: Path) -> str:
    h = hashlib.sha256()
    records: list[tuple[str, str]] = []

    if (root / ".git").exists():
        proc = subprocess.run(
            ["git", "-C", str(root), "status", "--porcelain", "-uall"],
            capture_output=True,
            text=True,
        )
        if proc.returncode == 0:
            for line in proc.stdout.splitlines():
                if len(line) < 4:
                    continue
                status = line[:2]
                raw_rel = line[3:].strip().strip('"')
                if " -> " in raw_rel:
                    raw_rel = raw_rel.split(" -> ", 1)[1].strip('"')
                if not raw_rel.endswith(".cs") or is_ignored_path(raw_rel):
                    continue
                records.append((raw_rel, status))
    else:
        for cs in sorted(root.rglob("*.cs")):
            rel = cs.relative_to(root).as_posix()
            if not is_ignored_path(rel):
                records.append((rel, "??"))

    if not records:
        return "EMPTY"

    for rel, status in sorted(set(records)):
        h.update(f"{status}:{rel}\n".encode("utf-8"))
        fp = root / rel
        if fp.is_file():
            h.update(fp.read_bytes())
        h.update(b"\0")

    return h.hexdigest()[:12]


def count_tests_in_text(text: str) -> int:
    return len(re.findall(r"\[\s*(?:Fact|Theory)\b", text))


def check_test_count_not_dropped(root: Path) -> str | None:
    if not has_git_head(root):
        return None

    cur_count = 0
    for cs in root.rglob("*.cs"):
        rel = cs.relative_to(root).as_posix()
        if not is_ignored_path(rel):
            cur_count += count_tests_in_text(cs.read_text(encoding="utf-8", errors="ignore"))

    ls_proc = subprocess.run(
        ["git", "-C", str(root), "ls-tree", "-r", "--name-only", "HEAD"],
        capture_output=True,
        text=True,
    )
    if ls_proc.returncode != 0:
        return None

    head_count = 0
    for rel in ls_proc.stdout.splitlines():
        if not rel.endswith(".cs") or is_ignored_path(rel):
            continue
        show_proc = subprocess.run(
            ["git", "-C", str(root), "show", f"HEAD:{rel}"],
            capture_output=True,
            text=True,
        )
        if show_proc.returncode == 0:
            head_count += count_tests_in_text(show_proc.stdout)

    if cur_count < head_count:
        return (
            f"stop-verify-gate: xUnit test count decreased ({cur_count} vs {head_count} in HEAD). "
            "Deleting tests is forbidden (AGENTS.md §2)."
        )
    return None


def find_dotnet_targets(root: Path) -> list[Path]:
    slns = [
        p
        for p in sorted(root.rglob("*.sln")) + sorted(root.rglob("*.slnx"))
        if not is_ignored_path(p.relative_to(root).as_posix())
    ]
    if slns:
        return [slns[0]]
    test_projs = [
        p
        for p in sorted(root.rglob("*.csproj"))
        if not is_ignored_path(p.relative_to(root).as_posix()) and "test" in p.name.lower()
    ]
    if test_projs:
        return test_projs
    return [
        p
        for p in sorted(root.rglob("*.csproj"))
        if not is_ignored_path(p.relative_to(root).as_posix())
    ]


def verify_build_test_coverage(root: Path) -> str | None:
    targets = find_dotnet_targets(root)
    if not targets:
        return "stop-verify-gate: .cs files detected, but no .sln / .csproj file found to build and verify 100% coverage."

    shutil.rmtree(COV_DIR, ignore_errors=True)
    COV_DIR.mkdir(parents=True, exist_ok=True)

    for target in targets:
        build_proc = subprocess.run(
            ["dotnet", "build", str(target), "--no-incremental"],
            capture_output=True,
            text=True,
        )
        if build_proc.returncode != 0:
            tail = "\n".join((build_proc.stdout + "\n" + build_proc.stderr).splitlines()[-25:])
            return f"stop-verify-gate: dotnet build failed:\n{tail}"

        test_proc = subprocess.run(
            [
                "dotnet",
                "test",
                str(target),
                "--no-build",
                '--collect:XPlat Code Coverage',
                "--results-directory",
                str(COV_DIR),
            ],
            capture_output=True,
            text=True,
        )
        if test_proc.returncode != 0:
            tail = "\n".join((test_proc.stdout + "\n" + test_proc.stderr).splitlines()[-25:])
            return f"stop-verify-gate: dotnet test failed:\n{tail}"

    xml_files = sorted(COV_DIR.rglob("coverage.cobertura.xml"))
    if not xml_files:
        return "stop-verify-gate: coverage.cobertura.xml not found (ensure coverlet.collector is referenced in the test project)."

    for xml_path in xml_files:
        tree = ET.parse(xml_path)
        cov_el = tree.getroot()
        lines_valid = int(cov_el.attrib.get("lines-valid", "0"))
        if lines_valid == 0:
            return (
                "stop-verify-gate (INV-8): Cobertura report contains lines-valid=0 "
                "(ensure the test project references and executes the production project)."
            )
        line_rate = float(cov_el.attrib.get("line-rate", "0"))
        branch_rate = float(cov_el.attrib.get("branch-rate", "0"))
        if line_rate < 1.0 or branch_rate < 1.0:
            uncovered: list[str] = []
            for cls in cov_el.findall(".//class"):
                cls_file = cls.attrib.get("filename", "?")
                cls_lr = float(cls.attrib.get("line-rate", "1"))
                cls_br = float(cls.attrib.get("branch-rate", "1"))
                if cls_lr < 1.0 or cls_br < 1.0:
                    missed_lines = [
                        ln.attrib.get("number", "?")
                        for ln in cls.findall("./lines/line")
                        if int(ln.attrib.get("hits", "0")) == 0
                        or (
                            ln.attrib.get("branch") == "true"
                            and "100%" not in ln.attrib.get("condition-coverage", "100%")
                        )
                    ]
                    uncovered.append(f"{cls_file} (lines/branches: {', '.join(missed_lines[:10])})")
            details = "; ".join(uncovered[:6])
            return (
                f"stop-verify-gate (INV-8): exact 100% line and branch coverage required, "
                f"actual line={line_rate * 100:.2f}%, branch={branch_rate * 100:.2f}%. "
                f"Uncovered: {details}"
            )

    return None


def verify_review_artifact(expected_sha: str) -> str | None:
    if not REVIEW_FILE.is_file():
        return (
            f"stop-verify-gate: missing .dev/.last-review.md. "
            f"Run the spec-review skill (clean-context code-reviewer-qa) for reviewed_sha: {expected_sha} before stopping."
        )
    text = REVIEW_FILE.read_text(encoding="utf-8", errors="ignore")
    m_verdict = re.search(r"^verdict:\s*(\S+)", text, flags=re.MULTILINE | re.IGNORECASE)
    m_sha = re.search(r"^reviewed_sha:\s*(\S+)", text, flags=re.MULTILINE | re.IGNORECASE)
    verdict = (m_verdict.group(1).strip() if m_verdict else "").upper()
    rev_sha = m_sha.group(1).strip() if m_sha else ""

    if verdict != "CLEAN":
        return f"stop-verify-gate: verdict in .dev/.last-review.md is '{verdict or 'NONE'}', expected 'CLEAN'."
    if rev_sha != expected_sha:
        return (
            f"stop-verify-gate: review is stale (reviewed_sha={rev_sha or 'NONE'}, current slice={expected_sha}). "
            f"Re-run spec-review on current changes."
        )
    return None


def check_circuit_breaker(conv_id: str, sha: str) -> bool:
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
    key = f"{conv_id}:{sha}"
    data: dict[str, int] = {}
    if STATE_FILE.is_file():
        try:
            data = json.loads(STATE_FILE.read_text(encoding="utf-8"))
        except Exception:
            data = {}
    count = int(data.get(key, 0))
    if count >= 2:
        return True
    data[key] = count + 1
    STATE_FILE.write_text(json.dumps(data), encoding="utf-8")
    return False


def clear_circuit_breaker() -> None:
    STATE_FILE.unlink(missing_ok=True)


def emit_stop(decision: str, reason: str = "") -> None:
    out: dict[str, str] = {"decision": decision}
    if reason:
        out["reason"] = reason
    print(json.dumps(out, ensure_ascii=False))
    sys.exit(0)


def main() -> None:
    if len(sys.argv) >= 2 and sys.argv[1] == "--print-sha":
        target_root = Path(sys.argv[2]).resolve() if len(sys.argv) >= 3 else ROOT
        print(compute_cs_state_sha(target_root))
        sys.exit(0)

    if len(sys.argv) >= 2 and sys.argv[1] == "--verify-coverage-only":
        target_root = Path(sys.argv[2]).resolve() if len(sys.argv) >= 3 else ROOT
        err = verify_build_test_coverage(target_root)
        if err:
            print(err, file=sys.stderr)
            sys.exit(1)
        sys.exit(0)

    raw = sys.stdin.read()
    if not raw.strip():
        emit_stop("allow")
    try:
        payload = json.loads(raw)
    except Exception:
        emit_stop("allow")

    if payload.get("terminationReason", "NO_TOOL_CALL") != "NO_TOOL_CALL":
        emit_stop("allow")

    sha = compute_cs_state_sha(ROOT)
    if sha == "EMPTY":
        clear_circuit_breaker()
        emit_stop("allow")

    conv_id = str(payload.get("conversationId") or "default")

    lint_proc = subprocess.run(
        [sys.executable, str(ROOT / ".agents" / "hooks" / "ooand-lint-gate.py"), "--check-all", str(ROOT)],
        capture_output=True,
        text=True,
    )
    if lint_proc.returncode != 0:
        if check_circuit_breaker(conv_id, sha):
            emit_stop("allow")
        emit_stop(
            "continue",
            "stop-verify-gate: ooand-lint-gate violations detected:\n" + lint_proc.stdout.strip(),
        )

    drop_err = check_test_count_not_dropped(ROOT)
    if drop_err:
        if check_circuit_breaker(conv_id, sha):
            emit_stop("allow")
        emit_stop("continue", drop_err)

    cov_err = verify_build_test_coverage(ROOT)
    if cov_err:
        if check_circuit_breaker(conv_id, sha):
            emit_stop("allow")
        emit_stop("continue", cov_err)

    rev_err = verify_review_artifact(sha)
    if rev_err:
        if check_circuit_breaker(conv_id, sha):
            emit_stop("allow")
        emit_stop("continue", rev_err)

    clear_circuit_breaker()
    emit_stop("allow")


if __name__ == "__main__":
    main()
