import os
import clr
import sys

# --- KONFIGURACIJA ---
DEFAULT_PATH = r"d:\g\dev\csharp\mamba.TorchDiscordSync2\Dependencies"
# Filtriraj DLL-ove koji sadrže ove ključne riječi (prazna lista [] znači "skeniraj sve")
FILTER_KEYWORDS = ["Torch", "Sandbox", "VRage", "SpaceEngineers"]
LOG_FILE = "inspect_results.txt"
# ---------------------

def inspect_dll(dll_path, log_file):
    output = []
    output.append(f"\n{'='*60}\nANALIZA: {os.path.basename(dll_path)}\n{'='*60}")
    
    try:
        import System.Reflection as Reflection
        # LoadFile može imati problema s ovisnostima, LoadFrom je često bolji za SE/Torch
        assembly = Reflection.Assembly.LoadFrom(os.path.abspath(dll_path))
        types = assembly.GetTypes()
        
        for t in types:
            if t.IsPublic:
                output.append(f"\n[NS: {t.Namespace}] -> Klasa: {t.Name}")
                
                methods = t.GetMethods(Reflection.BindingFlags.Public | 
                                      Reflection.BindingFlags.Instance | 
                                      Reflection.BindingFlags.Static |
                                      Reflection.BindingFlags.DeclaredOnly)
                
                for m in methods:
                    if not m.IsSpecialName:
                        output.append(f"  - {m.Name}")
    except Exception as e:
        output.append(f"GREŠKA: {e}")
    
    # Zapiši u log
    log_file.write("\n".join(output) + "\n")
    return len(output) > 3 # Vrati true ako je nešto pronađeno

def main():
    print(f"Header: Zadana putanja: {DEFAULT_PATH}")
    user_input = input(f"Putanja (Enter za zadano): ").strip()
    target_dir = os.path.abspath(user_input if user_input else DEFAULT_PATH)

    if not os.path.isdir(target_dir):
        print(f"Greška: Putanja ne postoji.")
        return

    # Dohvati DLL-ove i filtriraj ih
    all_dlls = [f for f in os.listdir(target_dir) if f.lower().endswith('.dll')]
    
    if FILTER_KEYWORDS:
        dll_files = [f for f in all_dlls if any(k.lower() in f.lower() for k in FILTER_KEYWORDS)]
    else:
        dll_files = all_dlls

    print(f"Pronađeno: {len(all_dlls)} | Filtrirano za skeniranje: {len(dll_files)}")
    print(f"Rezultati će biti spremljeni u: {LOG_FILE}")

    with open(LOG_FILE, "w", encoding="utf-8") as f:
        f.write(f"DLL INSPECTION REPORT - {target_dir}\n")
        f.write(f"Filteri: {', '.join(FILTER_KEYWORDS)}\n")
        
        for i, dll in enumerate(dll_files):
            full_path = os.path.join(target_dir, dll)
            print(f"[{i+1}/{len(dll_files)}] Skeniram: {dll}...", end="\r")
            inspect_dll(full_path, f)

    print(f"\n\nGotovo! Otvorite {LOG_FILE} u VSCode-u za pregled.")

if __name__ == "__main__":
    main()
