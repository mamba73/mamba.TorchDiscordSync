import os
import zipfile
import xml.etree.ElementTree as ET
from datetime import datetime

# --- CONFIGURATION ---
# Set the path where the ZIP should be saved (empty "" means current directory)
OUTPUT_DIRECTORY = "" 
PROJECT_NAME = "mambaTorchDiscordSync"
FILES_TO_ZIP = [
    "Plugin/", 
    "mamba.TorchDiscordSync.csproj",
    "mamba.TorchDiscordSync.sln",
    "manifest.xml",
    "README.md"
]
# ---------------------

def get_version_from_manifest(manifest_path):
    """Extracts the version string from the XML manifest file."""
    try:
        tree = ET.parse(manifest_path)
        root = tree.getroot()
        version = root.find('Version').text
        return version
    except Exception as e:
        print(f"DEBUG: Error reading manifest: {e}")
        return "0.0.0"

def create_zip():
    # 1. Fetch version and current timestamp
    version = get_version_from_manifest("manifest.xml")
    timestamp = datetime.now().strftime("%Y-%m-%d_%H%M")
    
    # 2. Generate filename
    zip_filename = f"{timestamp}_PROJECT_{PROJECT_NAME}_(v{version}).zip"
    
    # Define absolute output path
    output_path = os.path.join(OUTPUT_DIRECTORY, zip_filename)
    
    print(f"DEBUG: Starting archive creation: {zip_filename}")
    print("-" * 50)

    try:
        with zipfile.ZipFile(output_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for item in FILES_TO_ZIP:
                if os.path.exists(item):
                    if os.path.isdir(item):
                        # Recursively add directory contents
                        for root, dirs, files in os.walk(item):
                            for file in files:
                                file_path = os.path.join(root, file)
                                # Maintain directory structure inside ZIP
                                print(f"DEBUG: Adding (dir_file) -> {file_path}")
                                zipf.write(file_path, file_path)
                    else:
                        # Add individual file
                        print(f"DEBUG: Adding (file)     -> {item}")
                        zipf.write(item)
                else:
                    print(f"DEBUG: WARNING - Item not found: {item}")

        print("-" * 50)
        print(f"SUCCESS: ZIP archive created successfully.")
        print(f"LOCATION: {os.path.abspath(output_path)}")
        
    except Exception as e:
        print(f"ERROR: Failed to create ZIP: {e}")

if __name__ == "__main__":
    create_zip()