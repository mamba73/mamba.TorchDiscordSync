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
SCRIPT_VER = "1.2.2"

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

# Blacklist of internal items that MUST NOT be on the Master branch
RELEASE_BLACKLIST = [
    "sync.py", "build.py", "config_sync.ini", "config_check.ini", 
    "logs/", "build_staging/", "build_archive/", "Dependencies/", "doc/"
]

def log_and_print(message, level="INFO"):
    ts = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    formatted_msg = f"[{ts}] [{level}] {message}"
    print(formatted_msg)
    if not os.path.exists(LOG_DIR): os.makedirs(LOG_DIR)
    try:
        with open(LOG_FILE_PATH, "a", encoding="utf-8") as f:
            f.write(formatted_msg + "\n")
    except: pass

def check_run(cmd):
    """Executes command and TERMINATES script if it fails for absolute safety."""
    log_and_print(f"EXECUTING: {cmd}", "DEBUG")
    result = subprocess.run(cmd, shell=True, text=True, capture_output=True)
    if result.returncode != 0:
        error_msg = result.stderr.strip()
        log_and_print(f"FATAL ERROR: {error_msg}", "ERROR")
        sys.exit(1)
    output = result.stdout.strip()
    return output if output else "SUCCESS"

def get_project_version():
    try:
        if not os.path.exists(MANIFEST_PATH): return "0.0.0"
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
        log_and_print(f"README updated to version {version}")
    except Exception as e:
        log_and_print(f"README update failed: {e}", "WARNING")

def create_full_backup(version):
    ts = datetime.now().strftime("%Y-%m-%d_%H%M%S")
    zip_name = f"FULL_BACKUP_{version}_{ts}.zip"
    zip_path = os.path.join(script_dir, "..", zip_name)
    log_and_print(f"Starting full backup: {zip_name}")
    try:
        with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for root, dirs, files in os.walk(script_dir):
                if ".git" in root or "logs" in root: continue
                for file in files:
                    fp = os.path.join(root, file)
                    zipf.write(fp, os.path.relpath(fp, script_dir))
        log_and_print(f"Backup saved to: {zip_path}")
    except Exception as e:
        log_and_print(f"Backup failed: {e}", "ERROR")

def create_zip(version, use_staging=False):
    ts = datetime.now().strftime("%Y-%m-%d_%H%M%S")
    mode = "Release" if use_staging else "Source"
    zip_name = f"{cfg.get('ProjectName')}_{mode}_v{version}_{ts}.zip"
    try:
        with zipfile.ZipFile(zip_name, 'w', zipfile.ZIP_DEFLATED) as zipf:
            if use_staging:
                if not os.path.exists(PUBLISH_DIR):
                    log_and_print("Staging directory missing! ZIP aborted.", "ERROR")
                    return None
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
        log_and_print(f"SUCCESS: {mode} ZIP created: {zip_name}")
        return zip_name
    except Exception as e:
        log_and_print(f"ZIP creation error: {e}", "ERROR"); return None

def nuke_master_remote():
    """Wipes everything on master branch except README.md for a clean start."""
    log_and_print("SAFETY CHECK: Switching to Master branch...", "INFO")
    check_run(f"git checkout {RELEASE_BRANCH} -f")
    
    current = subprocess.run("git rev-parse --abbrev-ref HEAD", shell=True, text=True, capture_output=True).stdout.strip()
    if current != RELEASE_BRANCH:
        sys.exit(f"CRITICAL: Failed to switch to {RELEASE_BRANCH}!")

    log_and_print("Nuking all files on Master (except README)...", "WARNING")
    for item in os.listdir("."):
        if item == ".git" or item == "README.md": continue
        if os.path.isdir(item): shutil.rmtree(item, ignore_errors=True)
        else: os.remove(item)
    
    check_run("git add -A")
    check_run('git commit -m "Cleanup: Preparing for clean history release" --allow-empty')
    check_run(f"git push {cfg.get('ReleaseRemote')} {RELEASE_BRANCH} --force")
    log_and_print("Master branch is now empty on GitHub.")
    check_run(f"git checkout {DEV_BRANCH} -f")

def handle_dev(version, auto_yes):
    check_run(f"git checkout {DEV_BRANCH} -f")
    update_readme(version)
    check_run("git add .")
    
    status = subprocess.run("git diff --cached --name-status", shell=True, text=True, capture_output=True).stdout.strip()
    if not status:
        log_and_print("Dev branch is clean. Nothing to sync.")
        return
    
    msg = "auto dev sync" if auto_yes else input(f"Enter dev commit message (v{version}): ").strip()
    if not msg: return
    
    check_run(f'git commit -m "v{version} | {msg}"')
    check_run(f"git push {cfg.get('DevRemote')} {DEV_BRANCH}")

def handle_release(version, auto_yes, do_zip, do_deploy):
    log_and_print(f"STARTING CLEAN HISTORY RELEASE v{version}", "WARNING")
    if not auto_yes and input("This will flatten Master history. Proceed? (y/n): ").lower() != 'y': return

    zip_path = create_zip(version, use_staging=True) if (do_zip or do_deploy) else None

    # Step 1: Ensure we are on Master and it matches Dev's latest code
    check_run(f"git checkout {RELEASE_BRANCH} -f")
    check_run(f"git reset --hard {DEV_BRANCH}")
    
    # Step 2: Strip internal tools and documentation
    log_and_print("Removing internal files from Master index...", "DEBUG")
    for item in RELEASE_BLACKLIST:
        if os.path.exists(item):
            if os.path.isdir(item): shutil.rmtree(item, ignore_errors=True)
            else: os.remove(item)
    
    # Step 3: Finalize Master State
    check_run("git add -A")
    check_run("git rm -rf logs/ build_staging/ build_archive/ Dependencies/ doc/ --ignore-unmatch")
    check_run("git rm *.py *.ini --ignore-unmatch")

    # Step 4: Overwrite Master with a single "flattened" commit
    check_run(f'git commit -m "Release v{version}" --allow-empty')
    check_run(f"git push {cfg.get('ReleaseRemote')} {RELEASE_BRANCH} --force")

    # Step 5: GitHub Release Management
    if do_deploy and zip_path:
        repo_url = check_run(f"git remote get-url {cfg.get('ReleaseRemote')}")
        repo_name = repo_url.split("github.com/")[-1].replace(".git", "").replace(":", "/")
        
        # Cleanup old tags to prevent release conflicts
        subprocess.run(f"git tag -d v{version}", shell=True)
        subprocess.run(f"git push {cfg.get('ReleaseRemote')} :refs/tags/v{version}", shell=True)
        
        check_run(f'gh release create v{version} "{zip_path}" --repo {repo_name} --title "Release v{version}" --clobber --notes "Official Clean Release v{version}"')

    check_run(f"git checkout {DEV_BRANCH} -f")
    log_and_print(f"FINISH: Master branch history flattened and pushed to {cfg.get('ReleaseRemote')}")

def print_param_table():
    table = """
| Parameter     | Description                                           |
| :------------ | :---------------------------------------------------- |
| --nuke-master | Deletes everything on origin/master (Clean Start).    |
| --deploy      | Full release (Force Master=Dev, Strip Tools, Push).   |
| --full-backup | Creates a FULL project ZIP in the parent folder.      |
| --zip         | Creates a local ZIP of the source code.               |
| -y, --yes     | Skip confirmation prompts (Auto-pilot).               |
| -o, --open    | Opens the log file in the default editor.             |
"""
    print("\n--- AVAILABLE PARAMETERS ---")
    print(table)

if __name__ == "__main__":
    VER = get_project_version()
    LOG_FILE_PATH = os.path.join(LOG_DIR, f"{datetime.now().strftime('%Y-%m-%d_%H%M%S')}_{VER}_sync.log")
    
    parser = argparse.ArgumentParser(
        description=f"MAMBA SYNC TOOL v{SCRIPT_VER} - Automation for Dev/Master synchronization.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Usage Examples:
  python sync.py                # Standard sync: Update README and push to Private Dev.
  python sync.py --nuke-master  # WIPE origin/master (except README) to start fresh.
  python sync.py --deploy -y    # Release to Public: Reset Master, Strip tools, Force Push, Create GH Release.
  python sync.py --full-backup  # Create a complete ZIP backup of the project.
        """
    )
    
    parser.add_argument("--nuke-master", action="store_true", 
                        help="DANGER: Wipes EVERYTHING on the remote master branch except README.md. Use this to clean up leaked files.")
    parser.add_argument("--release", action="store_true", 
                        help="Merges Dev to Master, strips internal scripts/logs, and performs a force-push to flatten history.")
    parser.add_argument("--deploy", action="store_true", 
                        help="Performs a --release AND automatically creates a GitHub Release with the compiled ZIP attached.")
    parser.add_argument("--zip", action="store_true", 
                        help="Creates a Source Code ZIP (or Release ZIP if used with --deploy).")
    parser.add_argument("--full-backup", action="store_true", 
                        help="Creates a comprehensive ZIP backup of the entire project directory in the parent folder.")
    parser.add_argument("-y", "--yes", action="store_true", 
                        help="Auto-confirm all prompts.")
    parser.add_argument("-o", "--open", action="store_true", 
                        help="Automatically opens the log file after execution.")

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