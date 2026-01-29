import os
import sys
import clr  # Part of 'pythonnet' package

# --- CONFIGURATION ---
# DEFAULT_PATH = r"d:\g\dev\csharp\mamba.TorchDiscordSync2\Dependencies"
DEFAULT_PATH = r"g:\\dev\\SE\\csharp\\mamba.TorchDiscordSync\\Dependencies"

FILTER_KEYWORDS = ["Torch", "Sandbox", "VRage", "SpaceEngineers"]
LOG_DIR = "doc"
LOG_BASE_NAME = "inspect_results"
# ---------------------

def print_help():
    help_text = f"""
DLL INSPECTOR - HELP (v2.6 - Standard Deep Search)
=================================================
This script analyzes .NET DLL files and finds all classes/methods
matching your keyword across multiple files.

USAGE:
-----------
1. STANDARD SCAN: python {os.path.basename(__file__)}
2. SEARCH MODE:   python {os.path.basename(__file__)} --search "Keyword"

RESULTS:
---------
- Results are printed to console AND saved to {LOG_DIR}/
- Search logs use a '_search' prefix with auto-increment.
=================================================
    """
    print(help_text)

def get_unique_filename(directory, base_name, extension):
    if not os.path.exists(directory):
        os.makedirs(directory)
    counter = 1
    filename = f"{base_name}.{extension}"
    while os.path.exists(os.path.join(directory, filename)):
        filename = f"{base_name}_{counter}.{extension}"
        counter += 1
    return os.path.join(directory, filename)

def inspect_dll(dll_path, search_term=None):
    results = []
    try:
        import System.Reflection as Reflection
        from System.Reflection import ReflectionTypeLoadException
        
        abs_path = os.path.abspath(dll_path)
        # Koristimo standardni LoadFrom jer je on najpouzdaniji za hvatanje svega
        assembly = Reflection.Assembly.LoadFrom(abs_path)
        
        try:
            types = assembly.GetTypes()
        except ReflectionTypeLoadException as e:
            # Ako VRage ili neka druga datoteka fali, uzmi ono što je uspjelo učitati
            types = [t for t in e.Types if t is not None]
        except:
            return []

        for t in types:
            try:
                if not (t.IsClass or t.IsInterface): continue
                if not t.IsPublic: continue
                
                # BindingFlags: Isključujemo DeclaredOnly kako bismo vidjeli Attach/Detach
                flags = (Reflection.BindingFlags.Public | 
                         Reflection.BindingFlags.Instance | 
                         Reflection.BindingFlags.Static |
                         Reflection.BindingFlags.FlattenHierarchy)
                
                type_full_name = f"{t.Namespace}.{t.Name}"
                
                # Provjera odgovara li klasa ili namespace pojmu
                type_matches = search_term and (search_term.lower() in type_full_name.lower())
                
                methods = t.GetMethods(flags)
                temp_methods = []
                
                for m in methods:
                    if m.IsSpecialName: continue
                    
                    # Provjera odgovara li metoda pojmu
                    method_matches = search_term and (search_term.lower() in m.Name.lower())
                    
                    if not search_term or type_matches or method_matches:
                        try:
                            params = ", ".join([f"{p.ParameterType.Name} {p.Name}" for p in m.GetParameters()])
                            method_sig = f"  - {m.ReturnType.Name} {m.Name}({params})"
                            temp_methods.append(method_sig)
                        except:
                            temp_methods.append(f"  - {m.Name} (Parameters hidden)")

                if temp_methods:
                    results.append(f"[NS: {t.Namespace}] -> Class: {t.Name}")
                    results.extend(temp_methods)
            except:
                continue
                
    except Exception as e:
        if search_term:
            # Ispisujemo grešku samo ako je kritična za taj specifični DLL
            results.append(f"  [!] ERROR: {os.path.basename(dll_path)} - {str(e)[:50]}")
            
    return results

def main():
    search_term = None
    if "--help" in sys.argv or "-h" in sys.argv:
        print_help()
        return
    
    if "--search" in sys.argv or "-s" in sys.argv:
        try:
            idx = sys.argv.index("--search") if "--search" in sys.argv else sys.argv.index("-s")
            search_term = sys.argv[idx + 1]
        except IndexError:
            print("Error: Enter keyword after --search")
            return

    print(f"--- DLL Inspector v2.6 ---")
    user_input = input(f"Path to DLLs (Press Enter for default): ").strip()
    target_dir = os.path.abspath(user_input if user_input else DEFAULT_PATH)

    if not os.path.isdir(target_dir):
        print(f"Error: Path not found.")
        return

    # Pomaže .NET-u da nađe zavisne DLL-ove u istom folderu
    sys.path.append(target_dir)

    all_dlls = [f for f in os.listdir(target_dir) if f.lower().endswith('.dll')]
    dll_files = [f for f in all_dlls if any(k.lower() in f.lower() for k in FILTER_KEYWORDS)] if FILTER_KEYWORDS else all_dlls

    if search_term:
        log_name = f"{LOG_BASE_NAME}_search"
        log_path = get_unique_filename(LOG_DIR, log_name, "txt")
        print(f"Searching for '{search_term}' in {len(dll_files)} files...")
        
        found_any = False
        with open(log_path, "w", encoding="utf-8") as f:
            f.write(f"SEARCH REPORT: {search_term}\nLocation: {target_dir}\n" + "="*40 + "\n")
            
            for dll in dll_files:
                matches = inspect_dll(os.path.join(target_dir, dll), search_term)
                if matches:
                    header = f"\nFILE: {dll}"
                    print(header)
                    f.write(header + "\n")
                    for line in matches:
                        print(line)
                        f.write(line + "\n")
                    found_any = True
        
        if not found_any:
            print("No matches found.")
        else:
            print(f"\nDone! Results saved in: {log_path}")
            
    else:
        log_path = get_unique_filename(LOG_DIR, LOG_BASE_NAME, "txt")
        print(f"Full Scan Mode...")

        with open(log_path, "w", encoding="utf-8") as f:
            f.write(f"FULL REPORT: {target_dir}\n" + "="*40 + "\n")
            for i, dll in enumerate(dll_files):
                print(f"[{i+1}/{len(dll_files)}] {dll}", end="\r")
                results = inspect_dll(os.path.join(target_dir, dll))
                f.write(f"\nFILE: {dll}\n" + "-"*40 + "\n")
                f.write("\n".join(results) + "\n")
        
        print(f"\n\nDone! Full log: {log_path}")

if __name__ == "__main__":
    main()