import xml.etree.ElementTree as ET
import subprocess
import sys
import argparse
import re
import os
import zipfile
import configparser
import shutil
from datetime import datetime

# --- VERSION & METADATA ---
SCRIPT_VER = "1.2.0"

# --- CONFIGURATION & PATHS ---
script_dir = os.path.dirname(os.path.abspath(__file__))
config_file = os.path.join(script_dir, "config_sync.ini")

def load_config():
    config = configparser.ConfigParser()
    defaults = {
        'LogDir': 'logs',
        'VSCodePath': r"c:\dev\VSCode\bin\code.cmd",
        'ProjectName': 'mamba.TorchDiscordSync',
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

MANIFEST_PATH = "manifest.xml"
README_PATH = "README.md"
DEV_BRANCH = "dev"
RELEASE_BRANCH = "master"
PUBLISH_DIR = "build_staging"
FILES_TO_ZIP = ["Plugin/", "mamba.TorchDiscordSync.csproj", "mamba.TorchDiscordSync.sln", "manifest.xml", "README.md"]

RELEASE_BLACKLIST = [
    "sync.py", "build.py", "config_sync.ini", "config_check.ini", 
    "logs/", "build_staging/", "build_archive/", "Dependencies/", "doc/"
]

def log_and_print(message, level="INFO"):
    ts = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    formatted_msg = f"[{ts}] [{level}] {message}"
    print(formatted_msg)
    if not os.path.exists(LOG_DIR): os.makedirs(LOG_DIR)
    # Using a global-like path set in main
    try:
        with open(LOG_FILE_PATH, "a", encoding="utf-8") as f:
            f.write(formatted_msg + "\n")
    except: pass

def check_run(cmd):
    """Executes command and TERMINATES script if it fails. Absolute safety."""
    log_and_print(f"EXECUTING: {cmd}", "DEBUG")
    result = subprocess.run(cmd, shell=True, text=True, capture_output=True)
    if result.returncode != 0:
        error_msg = result.stderr.strip()
        log_and_print(f"FATAL ERROR: {error_msg}", "ERROR")
        print("\n" + "!"*60)
        print(f"CRITICAL FAILURE during: {cmd}")
        print(f"Reason: {error_msg}")
        print("Execution halted to prevent data loss.")
        print("!"*60 + "\n")
        sys.exit(1)
    output = result.stdout.strip()
    return output if output else "SUCCESS"

def get_project_version():
    try:
        if not os.path.exists(MANIFEST_PATH):
            return "0.0.0"
        tree = ET.parse(MANIFEST_PATH)
        root = tree.getroot()
        version_node = root.find('Version')
        return version_node.text.strip() if version_node is not None else "0.0.0"
    except: return "0.0.0"

def update_readme(version):
    try:
        if not os.path.exists(README_PATH): return
        with open(README_PATH, 'r', encoding='utf-8') as f: content = f.read()
        pattern = r"(?i)(\*?\*?version\*?\*?[:\s]+)([0-9\.]+)"
        new_content = re.sub(pattern, rf"\g<1>{version}", content)
        with open(README_PATH, 'w', encoding='utf-8') as f: f.write(new_content)
        log_and_print(f"README updated to {version}")
    except Exception as e:
        log_and_print(f"README update fail: {e}", "WARNING")

def create_full_backup(version):
    ts = datetime.now().strftime("%Y-%m-%d_%H%M%S")
    zip_name = f"FULL_BACKUP_{version}_{ts}.zip"
    zip_path = os.path.join(script_dir, "..", zip_name)
    log_and_print(f"Creating full backup: {zip_name}")
    try:
        with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for root, dirs, files in os.walk(script_dir):
                if ".git" in root or "logs" in root: continue
                for file in files:
                    fp = os.path.join(root, file)
                    zipf.write(fp, os.path.relpath(fp, script_dir))
        log_and_print(f"Backup Success: {zip_path}")
    except Exception as e:
        log_and_print(f"Backup Failed: {e}", "ERROR")

def create_zip(version, use_staging=False):
    ts = datetime.now().strftime("%Y-%m-%d_%H%M%S")
    mode = "Release" if use_staging else "Source"
    zip_name = f"{cfg.get('ProjectName')}_{mode}_v{version}_{ts}.zip"
    try:
        with zipfile.ZipFile(zip_name, 'w', zipfile.ZIP_DEFLATED) as zipf:
            if use_staging:
                if not os.path.exists(PUBLISH_DIR): return None
                for file in os.listdir(PUBLISH_DIR):
                    zipf.write(os.path.join(PUBLISH_DIR, file), file)
            else:
                for item in FILES_TO_ZIP:
                    if os.path.exists(item):
                        if os.path.isdir(item):
                            for r, d, files in os.walk(item):
                                for file in files:
                                    fp = os.path.join(r, file)
                                    zipf.write(fp, os.path.relpath(fp, os.getcwd()))
                        else: zipf.write(item)
        log_and_print(f"ZIP Created: {zip_name}")
        return zip_name
    except Exception as e:
        log_and_print(f"ZIP Error: {e}", "ERROR"); return None

def nuke_master_remote():
    """Wipes EVERYTHING on master branch except README.md. High-risk operation."""
    log_and_print("SAFETY CHECK: Switching to Master...", "INFO")
    
    # Prisilan checkout - ako ne uspije, skripta staje ovdje
    check_run(f"git checkout {RELEASE_BRANCH} -f")
    
    # Dvostruka provjera trenutne grane
    current = subprocess.run("git rev-parse --abbrev-ref HEAD", shell=True, text=True, capture_output=True).stdout.strip()
    if current != RELEASE_BRANCH:
        sys.exit(f"ABORTED: Checkout failed. We are still on {current}!")

    log_and_print(f"CONFIRMED ON {RELEASE_BRANCH}. NUKING...", "WARNING")
    
    deleted_count = 0
    for item in os.listdir("."):
        if item == ".git" or item == "README.md": continue
        if os.path.isdir(item):
            shutil.rmtree(item, ignore_errors=True)
        else:
            os.remove(item)
        deleted_count += 1
    
    check_run("git add -A")
    check_run('git commit -m "Repository Cleanup - Preparation for Clean Release" --allow-empty')
    check_run(f"git push {cfg.get('ReleaseRemote')} {RELEASE_BRANCH} --force")
    
    log_and_print(f"SUCCESS: {deleted_count} items removed from Master.")
    check_run(f"git checkout {DEV_BRANCH} -f")

def handle_dev(version, auto_yes):
    check_run(f"git checkout {DEV_BRANCH} -f")
    update_readme(version)
    check_run("git add .")
    
    status = subprocess.run("git diff --cached --name-status", shell=True, text=True, capture_output=True).stdout.strip()
    if not status:
        log_and_print("Workspace clean, nothing to sync.")
        return
    
    msg = "auto dev sync" if auto_yes else input(f"Dev commit msg (v{version}): ").strip()
    if not msg: return
    
    check_run(f'git commit -m "v{version} | {msg}"')
    check_run(f"git push {cfg.get('DevRemote')} {DEV_BRANCH}")

def handle_release(version, auto_yes, do_zip, do_deploy):
    log_and_print(f"STARTING CLEAN RELEASE v{version}", "WARNING")
    if not auto_yes and input("Rewrite Master history? (y/n): ").lower() != 'y': return

    zip_path = create_zip(version, use_staging=True) if (do_zip or do_deploy) else None

    # Step 1: Force Master to match Dev
    check_run(f"git checkout {RELEASE_BRANCH} -f")
    check_run(f"git reset --hard {DEV_BRANCH}")
    
    # Step 2: Delete blacklisted files
    for item in RELEASE_BLACKLIST:
        if os.path.exists(item):
            if os.path.isdir(item): shutil.rmtree(item, ignore_errors=True)
            else: os.remove(item)
    
    # Step 3: Finalize and Force Push
    check_run("git add -A")
    check_run("git rm -rf logs/ build_staging/ build_archive/ Dependencies/ doc/ --ignore-unmatch")
    check_run("git rm *.py *.ini --ignore-unmatch")
    check_run(f'git commit -m "Release v{version}" --allow-empty')
    check_run(f"git push {cfg.get('ReleaseRemote')} {RELEASE_BRANCH} --force")

    # Step 4: GitHub Release with Clobber
    if do_deploy and zip_path:
        repo_url = check_run(f"git remote get-url {cfg.get('ReleaseRemote')}")
        repo_name = repo_url.split("github.com/")[-1].replace(".git", "").replace(":", "/")
        
        # Reset tags to avoid "already exists" errors
        subprocess.run(f"git tag -d v{version}", shell=True)
        subprocess.run(f"git push {cfg.get('ReleaseRemote')} :refs/tags/v{version}", shell=True)
        
        check_run(f'gh release create v{version} "{zip_path}" --repo {repo_name} --title "Release v{version}" --clobber --notes "Clean Release."')

    check_run(f"git checkout {DEV_BRANCH} -f")
    log_and_print("RELEASE COMPLETE.")

def print_param_table():
    table = """
| Parameter     | Description                                           |
| :------------ | :---------------------------------------------------- |
| --nuke-master | Wipes everything on origin/master (EXCEPT README).    |
| --deploy      | Full release (Force Master = Dev, Strip Tools, Push). |
| --full-backup | Create ZIP of current project in parent folder.       |
| --zip         | Creates local ZIP of source code.                     |
| -y, --yes     | Skip confirmation prompts.                            |
| -o, --open    | Opens the log file in VS Code.                        |
"""
    print("\n--- AVAILABLE PARAMETERS ---")
    print(table)

if __name__ == "__main__":
    VER = get_project_version()
    LOG_FILE_PATH = os.path.join(LOG_DIR, f"{datetime.now().strftime('%Y-%m-%d_%H%M%S')}_{VER}_sync.log")
    
    parser = argparse.ArgumentParser(description=f"MAMBA SYNC TOOL v{SCRIPT_VER}")
    parser.add_argument("--nuke-master", action="store_true")
    parser.add_argument("--release", action="store_true")
    parser.add_argument("--deploy", action="store_true")
    parser.add_argument("--zip", action="store_true")
    parser.add_argument("--full-backup", action="store_true")
    parser.add_argument("-y", "--yes", action="store_true")
    parser.add_argument("-o", "--open", action="store_true")
    args = parser.parse_args()

    log_and_print(f"Mamba Sync Tool v{SCRIPT_VER} | Project Version: {VER}")
    
    if args.nuke_master:
        nuke_master_remote()
    elif args.full_backup:
        create_full_backup(VER)
    elif args.release or args.deploy:
        handle_release(VER, args.yes, args.zip, args.deploy)
    elif args.zip:
        create_zip(VER, use_staging=False)
    else:
        handle_dev(VER, args.yes)

    print_param_table()
    if args.open:
        log_path = os.path.abspath(LOG_FILE_PATH)
        if os.path.exists(VS_CODE_PATH): subprocess.run([VS_CODE_PATH, log_path], shell=True)
        else: os.startfile(log_path)