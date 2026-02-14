# requirements:
#   pip install pathspec
#
# What this script does:
#   - Updates // relative/path/to/file.cs header on top of every .cs file
#   - Checks / fixes / adds namespace declaration based on folder structure
#   - Respects .gitignore patterns (skips ignored files & folders)
#   - Creates timestamped log file in logs/ subdir with changes summary

import os
import re
from pathlib import Path
import datetime
from pathspec import PathSpec
from pathspec.patterns import GitWildMatchPattern

# ────────────────────────────────────────────────
# CONFIGURATION
# ────────────────────────────────────────────────

ROOT_DIR = r"D:\g\dev\csharp\mamba.TorchDiscordSync"
BASE_NAMESPACE = "mamba.TorchDiscordSync"

LOG_SUBDIR = "logs"
LOG_BASE_NAME = "update_headers_and_namespaces.txt"   # base name → will get timestamp prefix

FILE_PATTERN = "*.cs"

# ────────────────────────────────────────────────

def load_gitignore_spec(root: str) -> PathSpec | None:
    """Load .gitignore patterns if file exists"""
    gitignore_path = Path(root) / ".gitignore"
    if not gitignore_path.is_file():
        print("Warning: .gitignore not found → only basic skips will be applied")
        return None

    with gitignore_path.open("r", encoding="utf-8") as f:
        lines = [
            line.strip()
            for line in f
            if line.strip() and not line.startswith("#")
        ]

    return PathSpec.from_lines(GitWildMatchPattern, lines)


def get_relative_path(full_path: str, root: str) -> str:
    return os.path.relpath(full_path, root).replace("\\", "/")


def get_expected_namespace(rel_path: str, base_ns: str) -> str:
    """Convert folder structure to namespace"""
    dir_part = os.path.dirname(rel_path)
    if not dir_part or dir_part == ".":
        return base_ns

    parts = dir_part.split("/")
    return ".".join([base_ns] + parts)


def update_file(filepath: str, gitignore_spec: PathSpec | None, base_ns: str, root: str):
    """Process single .cs file - header + namespace"""
    rel = get_relative_path(filepath, root)
    expected_header = f"// {rel}"
    expected_ns = get_expected_namespace(rel, base_ns)

    print(f"Processing: {rel}")

    try:
        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()
            lines = content.splitlines(keepends=False)
    except Exception as e:
        print(f"  Error reading file: {e}")
        return False, [f"ERROR reading: {e}"]

    changed = False
    log_lines = []

    # ── Header handling ───────────────────────────────────────
    header_line = lines[0] if lines else ""
    header_pattern = re.compile(r"^// .+\.cs$")

    if header_pattern.match(header_line):
        if header_line.strip() != expected_header.strip():
            lines[0] = expected_header
            changed = True
            log_lines.append(f"Header updated: {expected_header}")
        else:
            log_lines.append("Header already correct")
    else:
        lines.insert(0, expected_header)
        changed = True
        log_lines.append(f"Header added: {expected_header}")

    # ── Namespace handling ────────────────────────────────────
    ns_pattern = re.compile(r"^\s*namespace\s+([a-zA-Z0-9_\.]+)\s*;", re.MULTILINE)
    match = ns_pattern.search(content)

    if match:
        current_ns = match.group(1)
        if current_ns != expected_ns:
            new_ns_line = f"namespace {expected_ns};"
            content = ns_pattern.sub(new_ns_line, content, count=1)
            lines = content.splitlines(keepends=False)
            changed = True
            log_lines.append(f"Namespace updated: {current_ns} → {expected_ns}")
        else:
            log_lines.append("Namespace already correct")
    else:
        # Find good insertion point (after header & usings)
        insert_idx = 1
        while insert_idx < len(lines) and (
            lines[insert_idx].strip().startswith("using ")
            or not lines[insert_idx].strip()
        ):
            insert_idx += 1

        lines.insert(insert_idx, f"namespace {expected_ns};")
        changed = True
        log_lines.append(f"Namespace added: {expected_ns}")

    if changed:
        try:
            with open(filepath, "w", encoding="utf-8", newline="\n") as f:
                f.write("\n".join(lines) + "\n")
            print("  → updated")
        except Exception as e:
            print(f"  Error writing file: {e}")
            log_lines.append(f"ERROR writing: {e}")
            return False, log_lines

    else:
        print("  → no changes")

    return changed, log_lines


def main():
    gitignore_spec = load_gitignore_spec(ROOT_DIR)

    log_dir = Path(ROOT_DIR) / LOG_SUBDIR
    log_dir.mkdir(exist_ok=True)

    # Generate timestamp prefix
    now = datetime.datetime.now()
    timestamp = now.strftime("%Y-%m-%d_%H%M%S")
    log_filename = f"{timestamp}_{LOG_BASE_NAME}"
    logfile = log_dir / log_filename

    changed_count = 0
    processed_count = 0
    log_entries = []

    with logfile.open("w", encoding="utf-8") as log:
        log.write(f"Update run started: {now.strftime('%Y-%m-%d %H:%M:%S')}\n")
        log.write(f"Root: {ROOT_DIR}\n")
        log.write(f"Base namespace: {BASE_NAMESPACE}\n")
        log.write(f"Log file: {log_filename}\n")
        log.write("=" * 60 + "\n\n")

        for dirpath, dirnames, filenames in os.walk(ROOT_DIR):
            # Remove ignored directories from traversal
            if gitignore_spec:
                dirnames[:] = [
                    d for d in dirnames
                    if not gitignore_spec.match_file(os.path.join(dirpath, d))
                ]

            for filename in filenames:
                if not filename.endswith(".cs"):
                    continue

                fullpath = os.path.join(dirpath, filename)

                # Skip if ignored by .gitignore
                if gitignore_spec and gitignore_spec.match_file(fullpath):
                    continue

                processed_count += 1
                was_changed, messages = update_file(fullpath, gitignore_spec, BASE_NAMESPACE, ROOT_DIR)

                if was_changed:
                    changed_count += 1

                log.write(f"{get_relative_path(fullpath, ROOT_DIR)}\n")
                for msg in messages:
                    log.write(f"  {msg}\n")
                log.write("\n")

        summary = (
            f"{'='*60}\n"
            f"Finished.\n"
            f"Processed .cs files : {processed_count}\n"
            f"Changed files       : {changed_count}\n"
            f"Log saved to        : {logfile}\n"
        )

        log.write(summary)
        print(summary)


if __name__ == "__main__":
    main()