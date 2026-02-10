# requirements: pip install pathspec
#
# pathFix.py
# ==========
# What the script does:
#   - Updates the // relative/path/to/file.cs header at the top of every .cs file
#   - Checks and corrects namespace if it exists (does NOT add if missing)
#   - Preserves existing { or ; after namespace name during replacement
#   - Correct namespace logic:
#       - Files directly in Plugin/     → mamba.TorchDiscordSync
#       - Files in Plugin/Subfolder/    → mamba.TorchDiscordSync.Plugin.Subfolder
#   - Skips folders and files defined in .gitignore
#   - Creates ZIP backup before changes (logs/backup/YYYY-MM-DD_HHMMSS.zip)
#   - Supports restore: python pathFix.py --restore last
#   - Detailed debug log: logs/YYYY-MM-DD_HHMMSS_debug.log

import os
import re
import zipfile
from pathlib import Path
import datetime
import argparse
from pathspec import PathSpec
from pathspec.patterns import GitWildMatchPattern
import shutil
import tempfile

# ────────────────────────────────────────────────
# CONFIGURATION
# ────────────────────────────────────────────────

ROOT_DIR = r"D:\g\dev\csharp\mamba.TorchDiscordSync"
BASE_NAMESPACE = "mamba.TorchDiscordSync"

LOG_SUBDIR = "logs"
LOG_BASE_NAME = "update_headers_and_namespaces.txt"
DEBUG_LOG_SUFFIX = "_debug.log"

BACKUP_SUBDIR = "backup"          # inside logs/

# ────────────────────────────────────────────────

def load_gitignore_spec(root: str) -> PathSpec | None:
    """Load .gitignore patterns if the file exists"""
    gitignore_path = Path(root) / ".gitignore"
    if not gitignore_path.is_file():
        print("Warning: .gitignore not found")
        return None

    with gitignore_path.open("r", encoding="utf-8") as f:
        lines = [line.strip() for line in f if line.strip() and not line.startswith("#")]

    return PathSpec.from_lines(GitWildMatchPattern, lines)


def get_relative_path(full_path: str, root: str) -> str:
    return os.path.relpath(full_path, root).replace("\\", "/")


def get_expected_namespace(rel_path: str, base_ns: str) -> str:
    """Generate correct namespace based on folder structure"""
    dir_part = os.path.dirname(rel_path)
    if not dir_part or dir_part == ".":
        return base_ns

    parts = dir_part.split("/")

    # Special case: files directly in Plugin/ folder → base namespace only
    if len(parts) == 1 and parts[0] == "Plugin":
        return base_ns

    # All other cases: keep Plugin as part of namespace if it's the root plugin folder
    # Example:
    #   Plugin/Services/...   → mamba.TorchDiscordSync.Plugin.Services
    #   Plugin/Core/...       → mamba.TorchDiscordSync.Plugin.Core
    #   Plugin/...            → mamba.TorchDiscordSync (already handled above)

    # If starts with Plugin, keep it
    if parts and parts[0] == "Plugin":
        # Do NOT remove Plugin here – it's intentional
        pass
    else:
        # If somehow not under Plugin, just append
        return ".".join([base_ns] + parts)

    return ".".join([base_ns] + parts)


def debug_write(debug_file, message: str):
    """Write to console and debug log file"""
    print(message)
    debug_file.write(message + "\n")
    debug_file.flush()


def update_file(filepath: str, gitignore_spec: PathSpec | None, base_ns: str, root: str, debug_file):
    rel = get_relative_path(filepath, root)
    expected_header = f"// {rel}"
    expected_ns = get_expected_namespace(rel, base_ns)

    debug_write(debug_file, f"\n{'─'*70}")
    debug_write(debug_file, f"DEBUG: Processing → {rel}")
    debug_write(debug_file, f"  Expected header : {expected_header!r}")
    debug_write(debug_file, f"  Expected ns     : {expected_ns!r}")

    try:
        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()
            lines = content.splitlines(keepends=False)
    except Exception as e:
        debug_write(debug_file, f"  Read error: {e}")
        return False, [f"ERROR reading: {e}"]

    debug_write(debug_file, "  First ~10 lines of file:")
    for i, line in enumerate(lines[:10], 1):
        debug_write(debug_file, f"    {i:2} | {line!r}")

    changed = False
    log_lines = []

    # ── Header handling ────────────────────────────────────────────────
    header_line = lines[0] if lines else ""
    header_pattern = re.compile(r"^// .+\.cs$")

    debug_write(debug_file, f"  Current header  : {header_line!r}")
    if header_pattern.match(header_line):
        if header_line.strip() != expected_header.strip():
            debug_write(debug_file, "  → Header needs update")
            lines[0] = expected_header
            changed = True
            log_lines.append(f"Header updated: {expected_header}")
        else:
            log_lines.append("Header already correct")
    else:
        debug_write(debug_file, "  → Header not recognized → adding")
        lines.insert(0, expected_header)
        changed = True
        log_lines.append(f"Header added: {expected_header}")

    # ── Namespace handling ─────────────────────────────────────────────
    # Capture only the namespace name, preserve { ; //comment etc.
    ns_pattern = re.compile(
        r"^\s*namespace\s+([a-zA-Z0-9_\.]+)\b",
        re.MULTILINE | re.IGNORECASE
    )

    match = ns_pattern.search(content)
    if match:
        current_ns = match.group(1)
        debug_write(debug_file, f"  Found namespace : {current_ns!r}")
        debug_write(debug_file, f"  Comparison      : {current_ns!r} == {expected_ns!r} ? → {current_ns == expected_ns}")

        if current_ns != expected_ns:
            debug_write(debug_file, "  → Namespace needs update")

            content = ns_pattern.sub(
                lambda m: f"namespace {expected_ns}",
                content,
                count=1
            )
            lines = content.splitlines(keepends=False)
            changed = True
            log_lines.append(f"Namespace updated: {current_ns} → {expected_ns}")
        else:
            log_lines.append("Namespace already correct")
    else:
        debug_write(debug_file, "  → Namespace NOT found by regex")
        log_lines.append("Namespace missing or unrecognized format")

    if changed:
        debug_write(debug_file, "  → Saving changes...")
        try:
            with open(filepath, "w", encoding="utf-8", newline="\n") as f:
                f.write("\n".join(lines) + "\n")
            debug_write(debug_file, "  → Saved successfully")
        except Exception as e:
            debug_write(debug_file, f"  Write error: {e}")
            log_lines.append(f"ERROR writing: {e}")
            return False, log_lines
    else:
        debug_write(debug_file, "  → No changes needed")

    return changed, log_lines


def create_backup_zip(root: str, gitignore_spec: PathSpec | None, timestamp: str) -> Path:
    backup_dir = Path(root) / LOG_SUBDIR / BACKUP_SUBDIR
    backup_dir.mkdir(parents=True, exist_ok=True)

    zip_path = backup_dir / f"{timestamp}.zip"

    print(f"Creating backup → {zip_path}")

    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for dirpath, dirnames, filenames in os.walk(root):
            if gitignore_spec:
                dirnames[:] = [d for d in dirnames if not gitignore_spec.match_file(os.path.join(dirpath, d))]

            for filename in filenames:
                if not filename.lower().endswith(".cs"):
                    continue

                fullpath = os.path.join(dirpath, filename)
                if gitignore_spec and gitignore_spec.match_file(fullpath):
                    continue

                rel = get_relative_path(fullpath, root)
                zf.write(fullpath, arcname=rel)

    print(f"Backup created: {zip_path}")
    return zip_path


def restore_from_backup(root: str, restore_key: str):
    backup_dir = Path(root) / LOG_SUBDIR / BACKUP_SUBDIR
    if not backup_dir.exists():
        print("No backup directory found")
        return

    backups = sorted(backup_dir.glob("*.zip"), key=lambda p: p.stat().st_mtime, reverse=True)

    if not backups:
        print("No backup files found")
        return

    if restore_key == "last":
        zip_to_restore = backups[0]
    else:
        zip_to_restore = backup_dir / f"{restore_key}.zip"
        if not zip_to_restore.exists():
            print(f"Backup {restore_key}.zip not found")
            return

    print(f"Restoring from: {zip_to_restore}")

    with tempfile.TemporaryDirectory() as temp_dir:
        with zipfile.ZipFile(zip_to_restore, "r") as zf:
            zf.extractall(temp_dir)

        for extracted_file in Path(temp_dir).rglob("*.cs"):
            rel = extracted_file.relative_to(temp_dir)
            target = Path(root) / rel

            if target.exists():
                shutil.copy2(extracted_file, target)
                print(f"  Restored: {rel}")
            else:
                print(f"  Skipped (missing target): {rel}")

    print("Restore completed")


def main():
    parser = argparse.ArgumentParser(description="Update headers and namespaces in .cs files")
    parser.add_argument("--restore", type=str, nargs="?", const="last",
                        help="Restore from backup: 'last' or specific timestamp")

    args = parser.parse_args()

    gitignore_spec = load_gitignore_spec(ROOT_DIR)

    if args.restore:
        restore_from_backup(ROOT_DIR, args.restore)
        return

    log_dir = Path(ROOT_DIR) / LOG_SUBDIR
    log_dir.mkdir(exist_ok=True)

    now = datetime.datetime.now()
    timestamp = now.strftime("%Y-%m-%d_%H%M%S")
    log_filename = f"{timestamp}_{LOG_BASE_NAME}"
    logfile = log_dir / log_filename

    debug_log_filename = f"{timestamp}{DEBUG_LOG_SUFFIX}"
    debug_log_path = log_dir / debug_log_filename
    debug_file = open(debug_log_path, "w", encoding="utf-8")
    debug_file.write(f"Debug log started: {now}\n")
    debug_file.write(f"Root directory: {ROOT_DIR}\n")
    debug_file.write("=" * 70 + "\n\n")

    backup_zip = create_backup_zip(ROOT_DIR, gitignore_spec, timestamp)

    changed_count = 0
    processed_count = 0

    with logfile.open("w", encoding="utf-8") as log:
        log.write(f"Run started: {now}\n")
        log.write(f"Root: {ROOT_DIR}\n")
        log.write(f"Base namespace: {BASE_NAMESPACE}\n")
        log.write(f"Backup: {backup_zip.name}\n")
        log.write("=" * 70 + "\n\n")

        for dirpath, dirnames, filenames in os.walk(ROOT_DIR):
            if gitignore_spec:
                dirnames[:] = [d for d in dirnames if not gitignore_spec.match_file(os.path.join(dirpath, d))]

            for filename in filenames:
                if not filename.lower().endswith(".cs"):
                    continue

                fullpath = os.path.join(dirpath, filename)
                if gitignore_spec and gitignore_spec.match_file(fullpath):
                    continue

                processed_count += 1
                was_changed, messages = update_file(
                    fullpath, gitignore_spec, BASE_NAMESPACE, ROOT_DIR, debug_file
                )

                if was_changed:
                    changed_count += 1

                log.write(f"{get_relative_path(fullpath, ROOT_DIR)}\n")
                for msg in messages:
                    log.write(f"  {msg}\n")
                log.write("\n")

        summary = (
            f"{'='*70}\n"
            f"Finished.\n"
            f"Processed .cs files : {processed_count}\n"
            f"Changed files       : {changed_count}\n"
            f"Main log            : {logfile}\n"
            f"Backup created      : {backup_zip}\n"
        )

        log.write(summary)
        print(summary)

    debug_file.write("\n" + "="*70 + "\n")
    debug_file.write(f"Debug log completed. Processed {processed_count} files, changed {changed_count}\n")
    debug_file.close()
    print(f"\nDebug log saved to: {debug_log_path}")


if __name__ == "__main__":
    main()