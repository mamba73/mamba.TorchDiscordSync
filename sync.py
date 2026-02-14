import xml.etree.ElementTree as ET
import subprocess
import sys
import argparse
import re
import os
import zipfile
import configparser
from datetime import datetime

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
        'KeepLogsDays': '7',
        'ScriptVersion': '1.1.4'
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
SCRIPT_VER = cfg.get('ScriptVersion')

MANIFEST_PATH = "manifest.xml"
README_PATH = "README.md"
DEV_BRANCH = "dev"
RELEASE_BRANCH = "master"
PUBLISH_DIR = "build_staging"
FILES_TO_ZIP = ["Plugin/", "mamba.TorchDiscordSync.csproj", "mamba.TorchDiscordSync.sln", "manifest.xml", "README.md"]

def log_and_print(message, level="INFO"):
    ts = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    formatted_msg = f"[{ts}] [{level}] {message}"
    print(formatted_msg)
    if not os.path.exists(LOG_DIR): os.makedirs(LOG_DIR)
    with open(LOG_FILE_PATH, "a", encoding="utf-8") as f:
        f.write(formatted_msg + "\n")

def run(cmd):
    log_and_print(f"EXECUTING: {cmd}", "DEBUG")
    result = subprocess.run(cmd, shell=True, text=True, capture_output=True)
    if result.returncode != 0:
        log_and_print(f"FAILED: {result.stderr.strip()}", "ERROR")
        return None
    output = result.stdout.strip()
    if output:
        log_and_print(f"RESULT: {output[:100]}...", "DEBUG")
    return output if output else "SUCCESS"

def get_project_version():
    try:
        if not os.path.exists(MANIFEST_PATH):
            sys.exit(f"CRITICAL: {MANIFEST_PATH} not found!")
        tree = ET.parse(MANIFEST_PATH)
        root = tree.getroot()
        version_node = root.find('Version')
        if version_node is not None:
            return version_node.text.strip()
        sys.exit("CRITICAL: <Version> tag not found in manifest.xml")
    except Exception as e:
        sys.exit(f"CRITICAL: Error parsing manifest.xml: {e}")

def update_readme(version):
    try:
        if not os.path.exists(README_PATH): return
        with open(README_PATH, 'r', encoding='utf-8') as f: content = f.read()
        # Fixed regex with \g<1> for group safety
        pattern = r"(?i)(\*?\*?version\*?\*?[:\s]+)([0-9\.]+)"
        new_content = re.sub(pattern, rf"\g<1>{version}", content)
        with open(README_PATH, 'w', encoding='utf-8') as f: f.write(new_content)
        log_and_print(f"README version synchronized to {version}")
    except Exception as e:
        log_and_print(f"README update failed: {e}", "WARNING")

def create_full_backup(version):
    ts = datetime.now().strftime("%Y-%m-%d_%H%M%S")
    branch = run("git rev-parse --abbrev-ref HEAD")
    zip_name = f"{ts}_{version}_FULL_mambaTDS_{branch}.zip"
    zip_path = os.path.join(script_dir, "..", zip_name)
    
    log_and_print(f"Starting FULL BACKUP: {zip_name}")
    try:
        with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for root, dirs, files in os.walk(script_dir):
                for file in files:
                    file_path = os.path.join(root, file)
                    arcname = os.path.relpath(file_path, script_dir)
                    zipf.write(file_path, arcname)
        log_and_print(f"FULL BACKUP SUCCESS: {os.path.abspath(zip_path)}")
    except Exception as e:
        log_and_print(f"FULL BACKUP FAILED: {e}", "ERROR")

def create_zip(version, use_staging=False):
    ts = datetime.now().strftime("%Y-%m-%d_%H%M%S")
    mode = "Release" if use_staging else "Source"
    zip_name = f"{cfg.get('ProjectName')}_{mode}_v{version}_{ts}.zip"
    log_and_print(f"STARTING {mode} ARCHIVE: {zip_name}")
    try:
        with zipfile.ZipFile(zip_name, 'w', zipfile.ZIP_DEFLATED) as zipf:
            if use_staging:
                if not os.path.exists(PUBLISH_DIR) or not os.listdir(PUBLISH_DIR):
                    log_and_print(f"ERROR: Staging directory empty.", "ERROR")
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
        log_and_print(f"SUCCESS: {mode} ZIP created.")
        return zip_name
    except Exception as e:
        log_and_print(f"ERROR: ZIP failed: {e}", "ERROR")
        return None

def handle_dev(version, auto_yes):
    current_branch = run("git rev-parse --abbrev-ref HEAD")
    if current_branch != DEV_BRANCH:
        log_and_print(f"Switching to {DEV_BRANCH}...")
        run(f"git checkout {DEV_BRANCH} -f")
    else:
        log_and_print(f"Active branch is {DEV_BRANCH}. Protecting workspace.")

    update_readme(version)
    run("git add .")
    
    if not run("git diff --cached --name-status") or run("git diff --cached --name-status") == "SUCCESS":
        log_and_print("No changes to sync. Workspace is clean.")
        return

    msg = "automatic dev sync" if auto_yes else input(f"Enter dev commit message (v{version}): ").strip()
    if not msg: 
        log_and_print("Sync aborted by user.")
        return
    
    run(f'git commit -m "v{version} | {msg}"')
    run(f"git push {cfg.get('DevRemote')} {DEV_BRANCH}")

def handle_release(version, auto_yes, do_zip, do_deploy):
    log_and_print(f"CRITICAL: Starting Public Release Process v{version}", "WARNING")
    if not auto_yes and input(f"Confirm PUBLIC RELEASE? (y/n): ").lower() != 'y': sys.exit("Aborted.")

    zip_path = None
    if do_zip or do_deploy:
        zip_path = create_zip(version, use_staging=True)

    run(f"git checkout {RELEASE_BRANCH} -f")
    for f in ["sync.py", "build.py", "config_sync.ini", "config_check.ini"]:
        if os.path.exists(f): os.remove(f)

    run(f"git merge {DEV_BRANCH} --squash -X theirs")
    run("git rm -rf build_staging/ build_archive/ logs/ Dependencies/ --ignore-unmatch")
    run("git rm *.py *.ini --ignore-unmatch")
    
    run(f'git commit -m "Release v{version}"')
    run(f"git push {cfg.get('ReleaseRemote')} {RELEASE_BRANCH} --force")

    if do_deploy and zip_path:
        repo_url = run(f"git remote get-url {cfg.get('ReleaseRemote')}")
        repo_name = repo_url.split("github.com/")[-1].replace(".git", "")
        run(f'gh release create v{version} "{zip_path}" --repo {repo_name} --title "Release v{version}" --notes "Automated release."')

    run(f"git checkout {DEV_BRANCH} -f")
    log_and_print(f"FINISH: Release v{version} complete.")

def print_param_table():
    table = """
| Parameter     | Description                                           |
| :------------ | :---------------------------------------------------- |
| (None)        | Standard sync: Updates README and pushes to Private Dev. |
| --full-backup | COMPLETE backup of everything to parent folder.       |
| --zip         | Creates Source ZIP (code only) in current folder.     |
| --release     | Squash merge to Master, strips tools, pushes to Origin. |
| --deploy      | Release + Auto ZIP Build + GitHub Release Upload.     |
| -y, --yes     | Skip confirmation prompts (Auto-pilot).               |
| -o, --open    | Opens the log file in VS Code after finish.           |
"""
    print("\n--- AVAILABLE PARAMETERS ---")
    print(table)

if __name__ == "__main__":
    VER = get_project_version()
    LOG_FILE_PATH = os.path.join(LOG_DIR, f"{datetime.now().strftime('%Y-%m-%d_%H%M%S')}_{VER}_sync.log")

    parser = argparse.ArgumentParser(description=f"MAMBA SYNC TOOL v{SCRIPT_VER}")
    parser.add_argument("--release", action="store_true")
    parser.add_argument("--zip", action="store_true")
    parser.add_argument("--deploy", action="store_true")
    parser.add_argument("--full-backup", action="store_true")
    parser.add_argument("-y", "--yes", action="store_true")
    parser.add_argument("-o", "--open", action="store_true")
    
    args = parser.parse_args()

    log_and_print(f"Mamba Sync Tool v{SCRIPT_VER} | Project Version: {VER}")

    if args.full_backup:
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