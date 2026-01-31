import os
import sys
import clr
import configparser
import re

# --- DYNAMIC CONFIGURATION LOADER ---
script_full_path = os.path.abspath(__file__)
script_dir = os.path.dirname(script_full_path)
script_base_name = os.path.splitext(os.path.basename(__file__))[0]
# config_file = os.path.join(script_dir, f"{script_base_name}.ini")
config_file = os.path.join(script_dir, f"check2.ini")

config = configparser.ConfigParser()

def load_config():
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

def get_unique_filename(directory, base_name, extension):
    full_log_dir = os.path.join(script_dir, directory)
    if not os.path.exists(full_log_dir): os.makedirs(full_log_dir)
    counter = 1
    filename = f"{base_name}.{extension}"
    while os.path.exists(os.path.join(full_log_dir, filename)):
        filename = f"{base_name}_{counter}.{extension}"
        counter += 1
    return os.path.join(full_log_dir, filename)

def inspect_dll(dll_path, search_term=None, member_filter=None, ext_mode=False, deep_mode=False):
    results = []
    version = "Unknown" 
    try:
        import System.Reflection as Reflection
        from System.Reflection import ReflectionTypeLoadException
        
        assembly = Reflection.Assembly.LoadFrom(os.path.abspath(dll_path))
        version = assembly.GetName().Version
        
        try:
            types = assembly.GetTypes()
        except ReflectionTypeLoadException as e:
            types = [t for t in e.Types if t is not None]
        except: return (version, [])

        for t in types:
            try:
                if not t.IsPublic or not (t.IsClass or t.IsInterface or t.IsValueType): continue
                
                type_name_full = f"{t.Namespace}.{t.Name}"
                # Ako imamo search_term, tip MORA odgovarati tome
                if search_term and search_term.lower() not in type_name_full.lower(): continue
                
                flags = (Reflection.BindingFlags.Public | Reflection.BindingFlags.Instance | 
                         Reflection.BindingFlags.Static | Reflection.BindingFlags.FlattenHierarchy)
                
                temp_items = []

                # Helper za filtriranje članova
                def match_filter(name):
                    if not member_filter: return True
                    return member_filter.lower() in name.lower()

                # 1. FIELDS
                if deep_mode:
                    for f in t.GetFields(flags):
                        if match_filter(f.Name):
                            prefix = "[ST] " if f.IsStatic else ""
                            temp_items.append(f"  [F] {prefix}{f.FieldType.Name} {f.Name}")

                # 2. METHODS
                for m in t.GetMethods(flags):
                    if m.IsSpecialName: continue
                    if match_filter(m.Name):
                        params = ", ".join([f"{p.ParameterType.Name} {p.Name}" for p in m.GetParameters()])
                        prefix = "[ST] " if m.IsStatic else ""
                        temp_items.append(f"  - {prefix}{m.ReturnType.Name} {m.Name}({params})")

                # 3. PROPERTIES
                if ext_mode or deep_mode:
                    for p in t.GetProperties(flags):
                        if match_filter(p.Name):
                            # Provjera statičnosti za property preko get metode
                            is_static = any(m.IsStatic for m in p.GetAccessors())
                            prefix = "[ST] " if is_static else ""
                            temp_items.append(f"  [P] {prefix}{p.PropertyType.Name} {p.Name}")

                if temp_items:
                    t_type = "Struct" if t.IsValueType else "Class"
                    results.append(f"\n[NS: {t.Namespace}] -> {t_type}: {t.Name}")
                    results.extend(temp_items)
            except: continue
    except: pass
    return (version, results)

def main():
    switches = ["--ext", "-e", "--deep", "-d", "--default", "-y", "--search", "-s", "--filter", "-f"]
    ext_mode = "--ext" in sys.argv or "-e" in sys.argv
    deep_mode = "--deep" in sys.argv or "-d" in sys.argv
    use_default = "--default" in sys.argv or "-y" in sys.argv
    
    search_term = None
    member_filter = None
    
    if "-s" in sys.argv or "--search" in sys.argv:
        idx = sys.argv.index("-s") if "-s" in sys.argv else sys.argv.index("--search")
        if idx + 1 < len(sys.argv) and sys.argv[idx+1] not in switches:
            search_term = sys.argv[idx+1]

    if "-f" in sys.argv or "--filter" in sys.argv:
        idx = sys.argv.index("-f") if "-f" in sys.argv else sys.argv.index("--filter")
        if idx + 1 < len(sys.argv) and sys.argv[idx+1] not in switches:
            member_filter = sys.argv[idx+1]

    print(f"--- DLL Inspector v2.24 ---")
    target_dir = os.path.abspath(cfg['path']) if use_default else input("Path: ").strip() or cfg['path']
    sys.path.append(target_dir)

    all_dlls = [f for f in os.listdir(target_dir) if f.lower().endswith('.dll')]
    dll_files = [f for f in all_dlls if any(k.strip().lower() in f.lower() for k in cfg['keywords'])] if cfg['keywords'] else all_dlls

    clean_s = re.sub(r'[^\w]', '', search_term) if search_term else "All"
    clean_f = f"_filter_{re.sub(r'[^\w]', '', member_filter)}" if member_filter else ""
    log_path = get_unique_filename(cfg['log_dir'], f"inspect_{clean_s}{clean_f}", "txt")

    print(f"[INFO] Search: {search_term} | Filter: {member_filter} | Log: {log_path}")

    with open(log_path, "w", encoding="utf-8") as f:
        for index, dll in enumerate(dll_files, start=1):
            print(f"\r[{index}/{len(dll_files)}] Analyzing {dll[:30]}...", end="")
            v, matches = inspect_dll(os.path.join(target_dir, dll), search_term, member_filter, ext_mode, deep_mode)
            if matches:
                f.write(f"\n{'='*60}\nFILE: {dll} (v{v})\n{'='*60}\n")
                f.write("\n".join(matches) + "\n")

    print(f"\n\nDONE! Check {log_path}")

if __name__ == "__main__":
    main()