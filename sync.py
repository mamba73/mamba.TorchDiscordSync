import xml.etree.ElementTree as ET
import subprocess
import sys
import argparse
import re
import os
import zipfile
import configparser
from datetime import datetime, timedelta

# --- CONFIGURATION & PATHS ---
script_dir = os.path.dirname(os.path.abspath(__file__))
config_file = os.path.join(script_dir, "sync_config.ini")

def load_config():
    config = configparser.ConfigParser()
    defaults = {
        'LogDir': 'logs',
        'VSCodePath': r"c:\dev\VSCode\bin\code.cmd",
        'ProjectName': 'mambaTorchDiscordSync',
        'DevRemote': 'private',
        'ReleaseRemote': 'origin',
        'KeepLogsDays': '7'
    }
    updated = False
    if not os.path.exists(config_file):
        config['SETTINGS'] = defaults
        updated = True
    else:
        config.read(config_file)
        if 'SETTINGS' not in config:
            config['SETTINGS'] = {}; updated = True
        for key, value in defaults.items():
            if not config.has_option('SETTINGS', key):
                config.set('SETTINGS', key, value); updated = True
    if updated:
        with open(config_file, 'w') as f: config.write(f)
    return config['SETTINGS']

cfg = load_config()
LOG_DIR = os.path.join(script_dir, cfg.get('LogDir'))
VS_CODE_PATH = cfg.get('VSCodePath')

# Constants
MANIFEST_PATH = "manifest.xml"
README_PATH = "README.md"
DEV_BRANCH = "dev"
RELEASE_BRANCH = "master"
FILES_TO_ZIP = ["Plugin/", "mamba.TorchDiscordSync.csproj", "mamba.TorchDiscordSync.sln", "manifest.xml", "README.md"]

HELP_DASHBOARD = """
COMMIT CONVENTIONS:
  feat:  New feature      fix:   Bug fix        refac: Cleanup
  perf:  Optimization     chore: Build/Tooling  docs:  Readme/Docs
======================================================================"""

def log_and_print(message):
    print(message)
    ts = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    with open(LOG_FILE_PATH, "a", encoding="utf-8") as f:
        f.write(f"[{ts}] {message}\n")

def clean_old_logs():
    if not os.path.exists(LOG_DIR): return
    try:
        days = int(cfg.get('KeepLogsDays', 7))
        now = datetime.now()
        for f in os.listdir(LOG_DIR):
            f_path = os.path.join(LOG_DIR, f)
            if os.path.isfile(f_path) and f.endswith(".log"):
                f_time = datetime.fromtimestamp(os.path.getmtime(f_path))
                if now - f_time > timedelta(days=days):
                    os.remove(f_path)
                    log_and_print(f"CLEANUP: Deleted old log {f}")
    except Exception as e: print(f"Log cleanup failed: {e}")

def run(cmd):
    log_and_print(f"EXECUTING: {cmd}")
    result = subprocess.run(cmd, shell=True, text=True, capture_output=True)
    if result.returncode != 0:
        log_and_print(f"ERROR: {result.stderr.strip()}")
        # Forced open on error for debugging
        open_log_file()
        sys.exit(1)
    return result.stdout.strip()

def open_log_file():
    log_path = os.path.abspath(LOG_FILE_PATH)
    if os.path.exists(VS_CODE_PATH):
        subprocess.run([VS_CODE_PATH, log_path], shell=True)
    else:
        os.startfile(log_path)

def get_version():
    try:
        tree = ET.parse(MANIFEST_PATH)
        return tree.getroot().find('Version').text
    except: sys.exit("CRITICAL: Manifest error.")

def update_readme(version):
    try:
        with open(README_PATH, 'r', encoding='utf-8') as f: content = f.read()
        pattern = r"(\*\*Version\*\*:\s*)(.*)"
        new_content = re.sub(pattern, lambda m: f"{m.group(1)}{version}", content)
        with open(README_PATH, 'w', encoding='utf-8') as f: f.write(new_content)
        log_and_print(f"SUCCESS: README updated to {version}")
    except Exception as e: log_and_print(f"WARNING: README update failed: {e}")

def create_zip(version):
    ts = datetime.now().strftime("%Y-%m-%d_%H%M%S")
    zip_name = f"{ts}_PROJECT_{cfg.get('ProjectName')}_(v{version}).zip"
    log_and_print(f"STARTING ARCHIVE: {zip_name}")
    try:
        with zipfile.ZipFile(zip_name, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for item in FILES_TO_ZIP:
                if os.path.exists(item):
                    if os.path.isdir(item):
                        for r, d, files in os.walk(item):
                            for file in files:
                                fp = os.path.join(r, file)
                                zipf.write(fp, fp)
                    else: zipf.write(item)
        log_and_print(f"SUCCESS: ZIP created: {os.path.abspath(zip_name)}")
    except Exception as e: log_and_print(f"ERROR: ZIP failed: {e}"); sys.exit(1)

def handle_dev(version, auto_yes):
    branch = run("git rev-parse --abbrev-ref HEAD")
    if branch != DEV_BRANCH:
        if not auto_yes:
            if input(f"On '{branch}', not '{DEV_BRANCH}'. Continue? (y/n): ").lower() != 'y': sys.exit("Aborted.")
    
    update_readme(version)
    if auto_yes:
        msg = "automatic dev sync"
    else:
        print(HELP_DASHBOARD)
        msg = input(f"Enter dev commit message (v{version}) [Empty to Abort]: ").strip()
        if not msg: 
            log_and_print("ABORTED: No commit message provided.")
            sys.exit(0)
    
    run("git add .")
    run(f'git commit -m "v{version} | {msg}"')
    run(f"git push {cfg.get('DevRemote')} {DEV_BRANCH}")
    log_and_print("FINISH: Dev push complete.")

def handle_release(version, auto_yes):
    log_and_print(f"CRITICAL: Release process v{version}")
    if not auto_yes:
        if input(f"Confirm SQUASH RELEASE to {cfg.get('ReleaseRemote')}? (y/n): ").lower() != 'y': sys.exit("Aborted.")

    update_readme(version)
    run(f"git checkout {RELEASE_BRANCH}")
    run(f"git merge {DEV_BRANCH} --squash --allow-unrelated-histories")
    run("git rm --cached *.py --ignore-unmatch")
    run(f'git commit -m "Release v{version}"')
    run(f"git push {cfg.get('ReleaseRemote')} {RELEASE_BRANCH}")
    run(f"git checkout {DEV_BRANCH}")
    log_and_print(f"FINISH: Public release successful.")

if __name__ == "__main__":
    if not os.path.exists(LOG_DIR): os.makedirs(LOG_DIR)
    clean_old_logs()
    
    VER = get_version()
    LOG_FILE_PATH = os.path.join(LOG_DIR, f"{datetime.now().strftime('%Y-%m-%d_%H%M%S')}_{VER}_git.log")

    parser = argparse.ArgumentParser(
        description="MAMBA SYNC TOOL - Automation for Dev and Public repos.",
        epilog=HELP_DASHBOARD,
        formatter_class=argparse.RawTextHelpFormatter
    )
    parser.add_argument("--release", action="store_true", help="Squash merge dev to master and push to public origin")
    parser.add_argument("--zip", action="store_true", help="Create project ZIP archive without git actions")
    parser.add_argument("-y", "--yes", action="store_true", help="Automatic mode (skip prompts and confirmation)")
    parser.add_argument("-o", "--open", action="store_true", help="Open the log file in VS Code/Editor after execution")
    
    args = parser.parse_args()

    if args.zip: 
        create_zip(VER)
    elif args.release: 
        handle_release(VER, args.yes)
    else: 
        handle_dev(VER, args.yes)

    # Only open if explicitly requested via -o
    if args.open:
        open_log_file()
    else:
        print(f"\nDONE! Log saved to: {os.path.relpath(LOG_FILE_PATH)}")
