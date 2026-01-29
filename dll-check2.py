import os
import sys
import clr
import configparser
import re  # Added for filename cleaning

# --- DYNAMIC CONFIGURATION LOADER ---
script_full_path = os.path.abspath(__file__)
script_dir = os.path.dirname(script_full_path)
script_base_name = os.path.splitext(os.path.basename(__file__))[0]
config_file = os.path.join(script_dir, f"{script_base_name}.ini")

config = configparser.ConfigParser()

def load_config():
    """ Loads settings from INI file. Creates default if missing. """
    if not os.path.exists(config_file):
        default_dep_path = os.path.join(script_dir, "Dependencies")
        config['SETTINGS'] = {
            'DefaultPath': default_dep_path,
            'FilterKeywords': "Torch,Sandbox,VRage,SpaceEngineers",
            'LogDir': "doc",
            'LogBaseName': "inspect_results"
        }
        with open(config_file, 'w') as f:
            config.write(f)
    else:
        config.read(config_file)
    
    return {
        'path': config.get('SETTINGS', 'DefaultPath'),
        'keywords': config.get('SETTINGS', 'FilterKeywords').split(','),
        'log_dir': config.get('SETTINGS', 'LogDir'),
        'log_name': config.get('SETTINGS', 'LogBaseName')
    }

cfg = load_config()

def print_help():
    help_text = f"""
.NET DLL INSPECTOR - HELP (v2.17)
=================================================
Analyzes .NET assemblies and extracts metadata.
Config file: {os.path.basename(config_file)}

USAGE:
-----------
python {os.path.basename(__file__)} [OPTIONS]

OPTIONS:
----------
--search, -s  : Keyword to find (Namespace, Class, Method, etc.).
--ext, -e     : Extended mode (Include Properties [P]).
--deep, -d    : Deep mode (Include Fields [F] and Events [E]).
--help, -h    : Show this help text.

LEGEND:
-------
[M] : Method (Default)
[P] : Property (Requires --ext or --deep)
[F] : Field (Requires --deep)
[E] : Event (Requires --deep)
=================================================
    """
    print(help_text)

def get_unique_filename(directory, base_name, extension):
    full_log_dir = os.path.join(script_dir, directory)
    if not os.path.exists(full_log_dir): 
        os.makedirs(full_log_dir)
    counter = 1
    filename = f"{base_name}.{extension}"
    while os.path.exists(os.path.join(full_log_dir, filename)):
        filename = f"{base_name}_{counter}.{extension}"
        counter += 1
    return os.path.join(full_log_dir, filename)

def inspect_dll(dll_path, search_term=None, ext_mode=False, deep_mode=False):
    results = []
    version = "Unknown" 
    try:
        import System.Reflection as Reflection
        from System.Reflection import ReflectionTypeLoadException
        
        abs_path = os.path.abspath(dll_path)
        assembly = Reflection.Assembly.LoadFrom(abs_path)
        version = assembly.GetName().Version
        
        try:
            types = assembly.GetTypes()
        except ReflectionTypeLoadException as e:
            types = [t for t in e.Types if t is not None]
        except:
            return (version, [])

        for t in types:
            try:
                if not t.IsPublic or not (t.IsClass or t.IsInterface): continue
                
                flags = (Reflection.BindingFlags.Public | Reflection.BindingFlags.Instance | 
                         Reflection.BindingFlags.Static | Reflection.BindingFlags.FlattenHierarchy)
                
                type_name_full = f"{t.Namespace}.{t.Name}"
                type_matches = not search_term or (search_term.lower() in type_name_full.lower())
                temp_items = []

                # 1. METHODS
                for m in t.GetMethods(flags):
                    if m.IsSpecialName: continue
                    params = ", ".join([f"{p.ParameterType.Name} {p.Name}" for p in m.GetParameters()])
                    sig = f"{m.ReturnType.Name} {m.Name}({params})"
                    if not search_term or type_matches or (search_term.lower() in sig.lower()):
                        temp_items.append(f"  - {m.ReturnType.Name} {m.Name}({params})")

                # 2. PROPERTIES
                if ext_mode or deep_mode:
                    for p in t.GetProperties(flags):
                        if not search_term or type_matches or (search_term.lower() in p.Name.lower()):
                            temp_items.append(f"  [P] {p.PropertyType.Name} {p.Name}")

                # 3. FIELDS & EVENTS
                if deep_mode:
                    for f in t.GetFields(flags):
                        if not search_term or type_matches or (search_term.lower() in f.Name.lower()):
                            temp_items.append(f"  [F] {f.FieldType.Name} {f.Name}")
                    for e in t.GetEvents(flags):
                        if not search_term or type_matches or (search_term.lower() in e.Name.lower()):
                            temp_items.append(f"  [E] {e.EventHandlerType.Name} {e.Name}")

                if temp_items:
                    results.append(f"\n[NS: {t.Namespace}] -> Class: {t.Name}")
                    results.extend(temp_items)
            except: continue
    except Exception as ex:
        if search_term: results.append(f"  [!] ERROR: {os.path.basename(dll_path)} - {str(ex)[:50]}")
    
    return (version, results)

def main():
    if "--help" in sys.argv or "-h" in sys.argv:
        print_help(); return

    search_term = None
    clean_search_term = ""
    ext_mode = "--ext" in sys.argv or "-e" in sys.argv
    deep_mode = "--deep" in sys.argv or "-d" in sys.argv
    
    if "--search" in sys.argv or "-s" in sys.argv:
        try:
            idx = sys.argv.index("--search") if "--search" in sys.argv else sys.argv.index("-s")
            search_term = sys.argv[idx + 1]
            # Strip invalid filename characters using regex
            clean_search_term = re.sub(r'[^\w\s-]', '', search_term).strip().replace(' ', '_')
        except: pass

    # Header
    print(f"--- DLL Inspector v2.17 ---")
    print(f"Default search directory: {cfg['path']}")
    print("Available switches: --ext (show Properties), --deep (show Fields/Events), --search <term>")
    
    user_input = input(f"Enter path (Leave blank for default 'Dependencies' folder):\n").strip()
    target_dir = os.path.abspath(user_input if user_input else cfg['path'])

    if not os.path.isdir(target_dir):
        print(f"Error: Directory '{target_dir}' not found."); return

    sys.path.append(target_dir)
    
    all_dlls = [f for f in os.listdir(target_dir) if f.lower().endswith('.dll')]
    dll_files = [f for f in all_dlls if any(k.strip().lower() in f.lower() for k in cfg['keywords'])] if cfg['keywords'] else all_dlls

    # Construct log filename with the search term
    log_base = cfg['log_name']
    if clean_search_term:
        log_base = f"inspect_search_{clean_search_term}"
    
    log_path = get_unique_filename(cfg['log_dir'], log_base, "txt")

    with open(log_path, "w", encoding="utf-8") as f:
        f.write(f"REPORT: {target_dir}\n" + "="*40 + "\n")
        if search_term:
            f.write(f"SEARCH FILTER: {search_term}\n" + "="*40 + "\n")

        for dll in dll_files:
            version, matches = inspect_dll(os.path.join(target_dir, dll), search_term, ext_mode, deep_mode)
            if matches:
                f.write(f"\n{'='*60}\nFILE: {dll} (v{version})\n{'='*60}\n")
                for line in matches:
                    f.write(line + "\n")

    print(f"\nDONE! Inspection file created: {log_path}")

if __name__ == "__main__":
    main()