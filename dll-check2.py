import os
import sys
import clr
import configparser

# --- DYNAMIC CONFIGURATION LOADER ---
script_full_path = os.path.abspath(__file__)
script_dir = os.path.dirname(script_full_path)
script_base_name = os.path.splitext(os.path.basename(__file__))[0]
config_file = os.path.join(script_dir, f"{script_base_name}.ini")

config = configparser.ConfigParser()

def load_config():
    """ Loads configuration and ensures 'Dependencies' is the default path suffix """
    if not os.path.exists(config_file):
        # Auto-create path pointing to a 'Dependencies' folder in the script directory
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

# Load config globally at startup
cfg = load_config()

def print_help():
    help_text = f"""
.NET DLL INSPECTOR - HELP (v2.12)
=================================================
Analyzes .NET assemblies and extracts metadata.
Configuration: {os.path.basename(config_file)}

USAGE:
-----------
python {os.path.basename(__file__)} [--search KEYWORD] [--deep]

DEFAULT SETUP:
--------------
By default, the script looks for DLLs in:
{cfg['path']}

SWITCHES:
----------
--search, -s  : Keyword to find (Namespace, Class, Method, Prop).
--deep, -d    : Include Fields [F] and Events [E].
--help, -h    : Show this help text.

LEGEND:
-------
[P] : Property | [M] : Method | [F] : Field | [E] : Event
[ST]: Static member (accessible without instance)
=================================================
    """
    print(help_text)

def get_unique_filename(directory, base_name, extension):
    # Ensure log directory is relative to script location
    full_log_dir = os.path.join(script_dir, directory)
    if not os.path.exists(full_log_dir): 
        os.makedirs(full_log_dir)
    
    counter = 1
    filename = f"{base_name}.{extension}"
    while os.path.exists(os.path.join(full_log_dir, filename)):
        filename = f"{base_name}_{counter}.{extension}"
        counter += 1
    return os.path.join(full_log_dir, filename)

def inspect_dll(dll_path, search_term=None, deep_mode=False):
    results = []
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
            return ("Unknown", [])

        for t in types:
            try:
                if not t.IsPublic or not (t.IsClass or t.IsInterface): continue
                
                flags = (Reflection.BindingFlags.Public | Reflection.BindingFlags.Instance | 
                         Reflection.BindingFlags.Static | Reflection.BindingFlags.FlattenHierarchy)
                
                type_name_full = f"{t.Namespace}.{t.Name}"
                type_matches = not search_term or (search_term.lower() in type_name_full.lower())
                temp_items = []

                # 1. Properties
                for p in t.GetProperties(flags):
                    if not search_term or type_matches or (search_term.lower() in p.Name.lower()):
                        accessors = []
                        if p.CanRead: accessors.append("get")
                        if p.CanWrite: accessors.append("set")
                        acc_str = f" {{ {'; '.join(accessors)}; }}"
                        
                        is_static = any(m.IsStatic for m in p.GetAccessors(True))
                        static_tag = "[ST] " if is_static else ""
                        temp_items.append(f"  [P] {static_tag}{p.PropertyType.Name} {p.Name}{acc_str}")

                # 2. Methods
                for m in t.GetMethods(flags):
                    if m.IsSpecialName: continue
                    params = ", ".join([f"{p.ParameterType.Name} {p.Name}" for p in m.GetParameters()])
                    sig = f"{m.ReturnType.Name} {m.Name}({params})"
                    if not search_term or type_matches or (search_term.lower() in sig.lower()):
                        temp_items.append(f"  [M] {'[ST] ' if m.IsStatic else ''}{sig}")

                # 3. Deep Scan (Fields & Events)
                if deep_mode:
                    for f in t.GetFields(flags):
                        if not search_term or type_matches or (search_term.lower() in f.Name.lower()):
                            temp_items.append(f"  [F] {'[ST] ' if f.IsStatic else ''}{f.FieldType.Name} {f.Name}")
                    for e in t.GetEvents(flags):
                        if not search_term or type_matches or (search_term.lower() in e.Name.lower()):
                            temp_items.append(f"  [E] {e.EventHandlerType.Name} {e.Name}")

                if temp_items:
                    base = f" : {t.BaseType.Name}" if t.BaseType and t.BaseType.Name != "Object" else ""
                    results.append(f"[NS: {t.Namespace}] -> {t.Name}{base}")
                    results.extend(temp_items)
            except: continue
    except Exception as ex:
        if search_term: results.append(f"  [!] ERROR: {os.path.basename(dll_path)} - {str(ex)[:50]}")
    return (version, results)

def main():
    if "--help" in sys.argv or "-h" in sys.argv:
        print_help(); return

    search_term = None
    deep_mode = "--deep" in sys.argv or "-d" in sys.argv
    if "--search" in sys.argv or "-s" in sys.argv:
        try:
            idx = sys.argv.index("--search") if "--search" in sys.argv else sys.argv.index("-s")
            search_term = sys.argv[idx + 1]
        except: pass

    print(f"--- DLL Inspector v2.12 ---")
    print(f"Default search directory: {cfg['path']}")
    user_input = input(f"Enter path (Leave blank for default 'Dependencies' folder):\n").strip()
    
    target_dir = os.path.abspath(user_input if user_input else cfg['path'])

    if not os.path.isdir(target_dir):
        print(f"Error: Directory '{target_dir}' not found.\nPlease create it or update {os.path.basename(config_file)}."); return

    sys.path.append(target_dir)
    
    all_dlls = [f for f in os.listdir(target_dir) if f.lower().endswith('.dll')]
    dll_files = [f for f in all_dlls if any(k.strip().lower() in f.lower() for k in cfg['keywords'])] if cfg['keywords'] else all_dlls

    log_base = f"{cfg['log_name']}_search" if search_term else cfg['log_name']
    log_path = get_unique_filename(cfg['log_dir'], log_base, "txt")

    with open(log_path, "w", encoding="utf-8") as f:
        f.write(f"INSPECTION REPORT\nDeep Mode: {deep_mode}\nTarget: {target_dir}\n{'='*60}\n")
        for dll in dll_files:
            version, matches = inspect_dll(os.path.join(target_dir, dll), search_term, deep_mode)
            if matches:
                header = f"\nFILE: {dll} (v{version})"
                print(header)
                f.write(header + "\n" + "-"*len(header) + "\n")
                for line in matches:
                    print(line)
                    f.write(line + "\n")

    print(f"\nFinished! Log: {log_path}")

if __name__ == "__main__":
    main()