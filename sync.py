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
config_file = os.path.join(script_dir, "config_sync.ini")

def load_config():
    config = configparser.ConfigParser()
    defaults = {
        'LogDir': 'logs',
        'VSCodePath': r"c:\dev\VSCode\bin\code.cmd",
        'ProjectName': 'mambaTorchDiscordSync',
        'DevRemote': 'private',
        'ReleaseRemote': 'origin',
        'KeepLogsDays': '7',
        'ScriptVersion': '1.0.7'
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
FILES_TO_ZIP = ["Plugin/", "mamba.TorchDiscordSync.csproj", "mamba.TorchDiscordSync.sln", "manifest.xml", "README.md"]

def log_and_print(message):
    print(message)
    ts = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    with open(LOG_FILE_PATH, "a", encoding="utf-8") as f:
        f.write(f"[{ts}] {message}\n")

def run(cmd):
    log_and_print(f"EXECUTING: {cmd}")
    result = subprocess.run(cmd, shell=True, text=True, capture_output=True)
    if result.returncode != 0:
        log_and_print(f"ERROR: {result.stderr.strip()}")
        return None
    return result.stdout.strip()

def get_project_version():
    """Parsira verziju direktno iz manifest.xml"""
    try:
        if not os.path.exists(MANIFEST_PATH):
            sys.exit(f"CRITICAL: {MANIFEST_PATH} not found!")
        tree = ET.parse(MANIFEST_PATH)
        root = tree.getroot()
        version_node = root.find('Version')
        if version_node is not None:
            return version_node.text.strip()
        else:
            sys.exit("CRITICAL: <Version> tag not found in manifest.xml")
    except Exception as e:
        sys.exit(f"CRITICAL: Error parsing manifest.xml: {e}")

def update_readme(version):
    """Preslikava verziju iz manifesta u README.md"""
    try:
        if not os.path.exists(README_PATH): return
        with open(README_PATH, 'r', encoding='utf-8') as f: content = f.read()
        
        # Regex koji hvata "Version: X.X.X", "**Version**: X.X.X" ili "version X.X.X" (case insensitive)
        pattern = r"(?i)(\*?\*?version\*?\*?[:\s]+)([0-9\.]+)"
        new_content = re.sub(pattern, r"\g<1>" + version, content)
        
        with open(README_PATH, 'w', encoding='utf-8') as f: f.write(new_content)
        log_and_print(f"SUCCESS: README updated to {version} (mirrored from manifest)")
    except Exception as e:
        log_and_print(f"WARNING: README update failed: {e}")

def create_zip(version):
    ts = datetime.now().strftime("%Y-%m-%d_%H%M%S")
    zip_name = f"{cfg.get('ProjectName')}_v{version}_{ts}.zip"
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
        return zip_name
    except Exception as e: 
        log_and_print(f"ERROR: ZIP failed: {e}")
        return None

def get_changes_summary():
    summary = run("git diff --cached --shortstat")
    files = run("git diff --cached --name-status")
    if not files: return ""
    return f"\n\nAuto-Summary:\n{summary}\nFiles:\n{files}"

def handle_dev(version, auto_yes):
    run(f"git checkout {DEV_BRANCH} -f")
    update_readme(version)
    run("git add .")
    change_details = get_changes_summary()
    if not change_details:
        log_and_print("INFO: No changes to sync.")
        return

    if auto_yes: msg = "automatic dev sync"
    else:
        msg = input(f"Enter dev commit message (v{version}): ").strip()
        if not msg: sys.exit("Aborted.")
    
    full_msg = f"v{version} | {msg}{change_details}"
    with open("temp_msg.txt", "w", encoding="utf-8") as f: f.write(full_msg)
    run(f'git commit -F temp_msg.txt')
    if os.path.exists("temp_msg.txt"): os.remove("temp_msg.txt")
    run(f"git push {cfg.get('DevRemote')} {DEV_BRANCH}")

def handle_release(version, auto_yes, do_zip, do_deploy):
    log_and_print(f"CRITICAL: Release process v{version}")
    if not auto_yes and input(f"Confirm PUBLIC RELEASE? (y/n): ").lower() != 'y': sys.exit("Aborted.")

    update_readme(version)
    run(f"git checkout {RELEASE_BRANCH} -f")
    
    # Brišemo alate s mastera prije merga
    for f in ["sync.py", "config_sync.ini"]:
        if os.path.exists(f): os.remove(f)

    log_and_print("INFO: Merging dev into master (Squash + Theirs)...")
    run(f"git merge {DEV_BRANCH} --squash -X theirs --allow-unrelated-histories")
    
    run("git rm -rf doc/ --ignore-unmatch")
    run("git rm --cached sync.py config_sync.ini --ignore-unmatch")
    run("git rm -rf build_archive/ build_staging/ Dependencies/ --ignore-unmatch")
    
    change_details = get_changes_summary()
    if not change_details:
        log_and_print("WARNING: No changes detected to commit on master.")
        run(f"git checkout {DEV_BRANCH} -f")
        return

    full_msg = f"Release v{version}{change_details}"
    with open("temp_msg.txt", "w", encoding="utf-8") as f: f.write(full_msg)
    run(f'git commit -F temp_msg.txt')
    if os.path.exists("temp_msg.txt"): os.remove("temp_msg.txt")
    
    # 1. PUSH na JAVNI MASTER (origin)
    run(f"git push {cfg.get('ReleaseRemote')} {RELEASE_BRANCH} --force")
    
    # 2. PUSH na PRIVATNI MASTER (private) - ako želiš i tamo imati master granu
    run(f"git push {cfg.get('DevRemote')} {RELEASE_BRANCH} --force")

    zip_path = None
    if do_zip or do_deploy:
        zip_path = create_zip(version)

    if do_deploy and zip_path:
        log_and_print(f"DEPLOY: Uploading ZIP ONLY to public origin...")
        # Dohvaćamo URL javnog repoa za GH CLI
        repo_url = run(f"git remote get-url {cfg.get('ReleaseRemote')}")
        repo_name = repo_url.split("github.com/")[-1].replace(".git", "")
        
        deploy_cmd = f'gh release create v{version} "{zip_path}" --repo {repo_name} --title "Release v{version}" --notes "Automated public release."'
        subprocess.run(deploy_cmd, shell=True)

    run(f"git checkout {DEV_BRANCH} -f")
    log_and_print(f"FINISH: Release v{version} complete. ZIP uploaded to origin.")

if __name__ == "__main__":
    if not os.path.exists(LOG_DIR): os.makedirs(LOG_DIR)
    
    # KLJUČNA IZMJENA: Verzija se uzima isključivo iz Manifesta
    VER = get_project_version()
    LOG_FILE_PATH = os.path.join(LOG_DIR, f"{datetime.now().strftime('%Y-%m-%d_%H%M%S')}_{VER}_git.log")

    parser = argparse.ArgumentParser(description=f"MAMBA SYNC TOOL v{SCRIPT_VER}")
    parser.add_argument("--release", action="store_true")
    parser.add_argument("--zip", action="store_true")
    parser.add_argument("--deploy", action="store_true")
    parser.add_argument("-y", "--yes", action="store_true")
    parser.add_argument("-o", "--open", action="store_true")
    
    args = parser.parse_args()

    if args.release or args.deploy:
        handle_release(VER, args.yes, args.zip, args.deploy)
    elif args.zip:
        create_zip(VER)
    else:
        handle_dev(VER, args.yes)

    if args.open:
        log_path = os.path.abspath(LOG_FILE_PATH)
        if os.path.exists(VS_CODE_PATH): subprocess.run([VS_CODE_PATH, log_path], shell=True)
        else: os.startfile(log_path)