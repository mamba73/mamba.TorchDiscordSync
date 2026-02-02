import os
import sys
import shutil
import zipfile
import subprocess
import xml.etree.ElementTree as ET
from datetime import datetime
import glob

# --- CONFIGURATION ---
PROJECT_NAME = "mamba.TorchDiscordSync"
PROJ_FILE = f"{PROJECT_NAME}.csproj"
MSBUILD_PATH = r"C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
OUT_DIR = os.path.join("bin", "Release", "net48")
PUBLISH_DIR = "build_staging"
ARCHIVE_DIR = "build_archive"
TARGET_DIR = r"d:\g\torch-server\Plugins"

def cleanup_old_zips():
    """Removes any leftover versioned ZIP files in the root directory."""
    # Pattern: ProjectName-v*.zip
    pattern = os.path.join(os.getcwd(), f"{PROJECT_NAME}-v*.zip")
    leftover_files = glob.glob(pattern)
    for f in leftover_files:
        try:
            os.remove(f)
            print(f"[CLEANUP] Removed leftover root file: {os.path.basename(f)}")
        except:
            pass

def get_version(auto_increment=False):
    manifest_path = 'manifest.xml'
    if not os.path.exists(manifest_path):
        return "1.0.0"

    try:
        ET.register_namespace('', "")
        tree = ET.parse(manifest_path)
        root = tree.getroot()
        v_element = root.find('Version')
        current_v = v_element.text.strip()
        
        parts = current_v.split('.')
        if len(parts) >= 3:
            parts[-1] = str(int(parts[-1]) + 1)
            suggested_v = ".".join(parts)
        else:
            suggested_v = current_v + ".1"

        if auto_increment:
            final_v = suggested_v
            print(f"[AUTO] Version: {current_v} -> {final_v}")
        else:
            print(f"\n--- VERSIONING ---")
            print(f"Current version: {current_v}")
            user_input = input(f"New version [{suggested_v}] (Enter to confirm): ").strip()
            final_v = user_input if user_input else suggested_v

        if final_v != current_v:
            v_element.text = final_v
            tree.write(manifest_path, encoding='utf-8', xml_declaration=True)
            print(f"[OK] Manifest updated to v{final_v}")
            
        return final_v
    except Exception as e:
        print(f"[ERROR] Manifest failed: {e}")
        return "1.0.0"

def run_build():
    print("\n--- MSBUILD START ---")
    if not os.path.exists(MSBUILD_PATH):
        print(f"[ERROR] MSBuild not found at: {MSBUILD_PATH}")
        return False
    
    cmd = [MSBUILD_PATH, PROJ_FILE, "/t:Restore;Rebuild", "/p:Configuration=Release", "/v:minimal"]
    result = subprocess.run(cmd)
    return result.returncode == 0

def prepare_staging():
    print("--- Preparing staging ---")
    if os.path.exists(PUBLISH_DIR):
        shutil.rmtree(PUBLISH_DIR)
    os.makedirs(PUBLISH_DIR)

    forbidden_size = 1691648
    excluded_prefixes = ("Torch", "VRage", "Sandbox")

    count = 0
    for root, dirs, files in os.walk(OUT_DIR):
        for file in files:
            if file.endswith((".dll", ".xml", ".config")):
                if file.lower() == "manifest.xml": continue
                src_path = os.path.join(root, file)
                if any(file.startswith(p) for p in excluded_prefixes): continue
                if os.path.getsize(src_path) == forbidden_size: continue
                
                shutil.copy(src_path, os.path.join(PUBLISH_DIR, file))
                count += 1
    
    shutil.copy("manifest.xml", os.path.join(PUBLISH_DIR, "manifest.xml"))
    print(f"[OK] Staging ready.")

def create_zip(version):
    zip_name = f"{PROJECT_NAME}-v{version}.zip"
    with zipfile.ZipFile(zip_name, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for file in os.listdir(PUBLISH_DIR):
            zipf.write(os.path.join(PUBLISH_DIR, file), file)
    return zip_name

def deploy_and_archive(temp_zip, version):
    if not os.path.exists(ARCHIVE_DIR):
        os.makedirs(ARCHIVE_DIR)
    
    ts = datetime.now().strftime("%Y-%m-%d_%H%M")
    archive_name = f"{ts}_{PROJECT_NAME}_(v{version}).zip"
    archive_path = os.path.join(ARCHIVE_DIR, archive_name)
    target_path = os.path.join(TARGET_DIR, f"{PROJECT_NAME}.zip")

    try:
        # Check if Target directory exists
        if not os.path.exists(TARGET_DIR):
            print(f"[!] Warning: Target directory {TARGET_DIR} not found. Skipping deployment.")
        else:
            shutil.copy(temp_zip, target_path)
            print(f"[SUCCESS] Deployed to Torch: {os.path.basename(target_path)}")

        # Move instead of copy to ensure root is cleaned
        shutil.move(temp_zip, archive_path)
        print(f"[OK] Archived build: {archive_name}")

    except PermissionError:
        print("[!] ERROR: File locked. Close Torch server or check permissions.")
        # If move failed, we don't want the build process to lie about root cleanup
    except Exception as e:
        print(f"[ERROR] Deployment/Archive failed: {e}")

    # Keep last 10
    backups = sorted([os.path.join(ARCHIVE_DIR, f) for f in os.listdir(ARCHIVE_DIR)], key=os.path.getmtime)
    while len(backups) > 10:
        os.remove(backups.pop(0))

if __name__ == "__main__":
    # 0. Initial cleanup of old artifacts
    cleanup_old_zips()

    auto = "-y" in sys.argv or "--yes" in sys.argv
    ver = get_version(auto_increment=auto)
    
    if run_build():
        prepare_staging()
        temp_zip = create_zip(ver)
        deploy_and_archive(temp_zip, ver)
    else:
        print("\n[FAILED] Build process stopped.")