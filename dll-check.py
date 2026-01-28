import os
import sys
import clr  # Dio 'pythonnet' paketa

# --- KONFIGURACIJA ---
DEFAULT_PATH = r"d:\g\dev\csharp\mamba.TorchDiscordSync2\Dependencies"
FILTER_KEYWORDS = ["Torch", "Sandbox", "VRage", "SpaceEngineers"]
LOG_DIR = "doc"
LOG_BASE_NAME = "inspect_results"
# ---------------------

def print_help():
    help_text = f"""
DLL INSPECTOR - POMOĆ na HRVATSKOM
=================================================
Ova skripta analizira .NET DLL datoteke i ispisuje
njihove Namespace-ove, klase i metode u log datoteku.

POTREBNE INSTALACIJE:
---------------------
Prije prvog pokretanja instalirajte 'pythonnet' biblioteku:
    pip install pythonnet

KORIŠTENJE:
-----------
1. Pokrenite: python {os.path.basename(__file__)}
2. Pritisnite ENTER za zadani direktorij:
   {DEFAULT_PATH}
3. Ili unesite novu putanju (apsolutnu ili relativnu).

REZULTAT:
---------
Izvještaji se spremaju u mapu: ./{LOG_DIR}/
Svako pokretanje stvara novu datoteku (increment).
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

def inspect_dll(dll_path, log_file):
    output = []
    output.append(f"\n{'='*80}\nANALIZA: {os.path.basename(dll_path)}\n{'='*80}")
    try:
        import System.Reflection as Reflection
        assembly = Reflection.Assembly.LoadFrom(os.path.abspath(dll_path))
        types = assembly.GetTypes()
        for t in types:
            if t.IsPublic:
                output.append(f"\n[NS: {t.Namespace}] -> Klasa: {t.Name}")
                flags = (Reflection.BindingFlags.Public | Reflection.BindingFlags.Instance | 
                         Reflection.BindingFlags.Static | Reflection.BindingFlags.DeclaredOnly)
                methods = t.GetMethods(flags)
                for m in methods:
                    if not m.IsSpecialName:
                        params = ", ".join([f"{p.ParameterType.Name} {p.Name}" for p in m.GetParameters()])
                        output.append(f"  - {m.ReturnType.Name} {m.Name}({params})")
    except Exception as e:
        output.append(f"INFO: Preskočeno (nije .NET ili nema pristupa): {e}")
    log_file.write("\n".join(output) + "\n")

def main():
    # Provjera za --help argument
    if "--help" in sys.argv or "-h" in sys.argv:
        print_help()
        return

    print(f"--- DLL Inspector za Torch/SE (upišite --help za upute) ---")
    user_input = input(f"Putanja do DLL-ova (Enter za zadano): ").strip()
    target_dir = os.path.abspath(user_input if user_input else DEFAULT_PATH)

    if not os.path.isdir(target_dir):
        print(f"Greška: Putanja '{target_dir}' ne postoji.")
        return

    log_path = get_unique_filename(LOG_DIR, LOG_BASE_NAME, "txt")
    all_dlls = [f for f in os.listdir(target_dir) if f.lower().endswith('.dll')]
    dll_files = [f for f in all_dlls if any(k.lower() in f.lower() for k in FILTER_KEYWORDS)] if FILTER_KEYWORDS else all_dlls

    print(f"Skeniram {len(dll_files)} datoteka u: {log_path}")

    with open(log_path, "w", encoding="utf-8") as f:
        f.write(f"REPORT: {target_dir}\nFiltrirano: {', '.join(FILTER_KEYWORDS)}\n" + "="*40 + "\n")
        for i, dll in enumerate(dll_files):
            print(f"[{i+1}/{len(dll_files)}] {dll}", end="\r")
            inspect_dll(os.path.join(target_dir, dll), f)

    print(f"\n\nGotovo! Log se nalazi na: {log_path}")

if __name__ == "__main__":
    main()
