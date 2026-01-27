import os
import shutil
import zipfile
import subprocess
import xml.etree.ElementTree as ET
from datetime import datetime

# --- CONFIGURATION ---
PROJECT_NAME = "mamba.TorchDiscordSync"
PROJ_FILE = f"{PROJECT_NAME}.csproj"
MSBUILD_PATH = r"C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
OUT_DIR = os.path.join("bin", "Release", "net48")
PUBLISH_DIR = "build_staging"
ARCHIVE_DIR = "build_archive"
TARGET_DIR = r"d:\g\torch-server\Plugins" # Path to your Torch server

def get_version_and_prompt_increment():
    """Reads version from manifest.xml, prompts for increment, and updates file."""
    manifest_path = 'manifest.xml'
    if not os.path.exists(manifest_path):
        print(f"[!] {manifest_path} not found!")
        return "1.0.0"

    try:
        # Register namespace to avoid 'ns0' prefixes in XML output
        ET.register_namespace('', "")
        tree = ET.parse(manifest_path)
        root = tree.getroot()
        
        v_element = root.find('Version')
        current_v = v_element.text.strip()
        
        # Automatic increment logic (n.n.X)
        parts = current_v.split('.')
        if len(parts) >= 3:
            parts[-1] = str(int(parts[-1]) + 1)
            suggested_v = ".".join(parts)
        else:
            suggested_v = current_v + ".1"

    

        print(f"\n--- DEV2PROD VERSIONING ---")
        print(f"Current version: {current_v}")
        user_input = input(f"New version [{suggested_v}] (Enter to confirm): ").strip()
        
        final_v = user_input if user_input else suggested_v

        if final_v != current_v:
            v_element.text = final_v
            # Write back to manifest with XML declaration
            tree.write(manifest_path, encoding='utf-8', xml_declaration=True)
            print(f"[OK] Manifest updated to v{final_v}")
            
        return final_v
    except Exception as e:
        print(f"[ERROR] Manifest processing failed: {e}")
        return "1.0.0"

def run_build():
    """Calls MSBuild for Release configuration."""
    print("\n--- MSBUILD START ---")
    if not os.path.exists(MSBUILD_PATH):
        print(f"[ERROR] MSBuild not found at: {MSBUILD_PATH}")
        return False
    
    cmd = [MSBUILD_PATH, PROJ_FILE, "/t:Restore;Rebuild", "/p:Configuration=Release", "/p:UseSharedCompilation=false", "/v:minimal"]
    result = subprocess.run(cmd)
    return result.returncode == 0

def prepare_staging():
    """Cleans and prepares files for ZIP (avoids Torch dependency bloat)."""
    print("--- Preparing staging folder ---")
    if os.path.exists(PUBLISH_DIR):
        shutil.rmtree(PUBLISH_DIR)
    os.makedirs(PUBLISH_DIR)

    forbidden_size = 1691648  # Exact size of the problematic Torch.dll
    excluded_prefixes = ("Torch", "VRage", "Sandbox")

    count = 0
    for root, dirs, files in os.walk(OUT_DIR):
        for file in files:
            # We want DLLs, XML configs, and .config files
            if file.endswith(".dll") or file.endswith(".xml") or file.endswith(".config"):
                # Skip the root manifest.xml (we handle it at the end)
                if file.lower() == "manifest.xml": continue
                
                src_path = os.path.join(root, file)
                
                # SECURITY FILTERS
                if any(file.startswith(p) for p in excluded_prefixes): continue
                if os.path.getsize(src_path) == forbidden_size:
                    print(f"[!] Ignoring suspicious DLL (Size match): {file}")
                    continue
                
                # Flattening: copy everything into the root of staging
                shutil.copy(src_path, os.path.join(PUBLISH_DIR, file))
                count += 1
    
    # Copy the updated manifest.xml into staging
    shutil.copy("manifest.xml", os.path.join(PUBLISH_DIR, "manifest.xml"))
    print(f"[OK] Staging prepared with {count} files.")

def create_zip(version):
    """Packages staging into a versioned ZIP file."""
    zip_name = f"{PROJECT_NAME}-v{version}.zip"
    print(f"--- Creating package: {zip_name} ---")
    
    with zipfile.ZipFile(zip_name, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for file in os.listdir(PUBLISH_DIR):
            zipf.write(os.path.join(PUBLISH_DIR, file), file)
    return zip_name

def deploy(zip_name):
    """Archives old build and deploys the new one to Torch."""
    if not os.path.exists(ARCHIVE_DIR):
        os.makedirs(ARCHIVE_DIR)
    
    # 1. Archive current production build
    prod_zip_name = f"{PROJECT_NAME}.zip"
    target_path = os.path.join(TARGET_DIR, prod_zip_name)
    
    if os.path.exists(target_path):
        ts = datetime.now().strftime("%Y%m%d_%H%M")
        archive_path = os.path.join(ARCHIVE_DIR, f"{PROJECT_NAME}_backup_{ts}.zip")
        shutil.move(target_path, archive_path)
        print(f"[OK] Archived old build to: {archive_path}")

    # 2. Deploy new version (Torch expects a fixed name)
    shutil.copy(zip_name, target_path)
    print(f"[SUCCESS] Version deployed to Torch Plugins!")

if __name__ == "__main__":
    # 1. Versioning step
    ver = get_version_and_prompt_increment()
    
    # 2. Compile step
    if run_build():
        # 3. Filtering and Staging step
        prepare_staging()
        # 4. Packaging step
        new_zip = create_zip(ver)
        # 5. Delivery and History step
    
        deploy(new_zip)
    else:
        print("\n[FAILED] Build failed. Please check MSBuild errors.")