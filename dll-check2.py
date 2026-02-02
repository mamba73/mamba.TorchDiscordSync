

import os
import sys
import clr
import configparser
import re
import subprocess

# --- CONFIG ---
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
        with open(config_file, 'w') as f: config.write(f)
    else: config.read(config_file)
    return {
        'path': config.get('SETTINGS', 'DefaultPath'), 
        'keywords': config.get('SETTINGS', 'FilterKeywords').split(','), 
        'log_dir': config.get('SETTINGS', 'LogDir'), 
        'log_name': config.get('SETTINGS', 'LogBaseName')
    }

cfg = load_config()

def format_type_name(t):
    """ Cleans List`1[...] or Int64 into readable C# format """
    if t is None: return "void"
    name = t.Name
    # Mapping fundamental .NET types to C# keywords
    mappings = {
        "Int64": "long", "UInt64": "ulong", "Int32": "int", 
        "UInt32": "uint", "Single": "float", "Double": "double", 
        "Boolean": "bool", "String": "string"
    }
    if name in mappings: return mappings[name]
    
    # Generic type handling (e.g. List`1 -> List<T>)
    if '`' in name:
        base_name = name.split('`')[0]
        try:
            gen_args = t.GetGenericArguments()
            args_names = [format_type_name(a) for a in gen_args]
            return f"{base_name}<{', '.join(args_names)}>"
        except: return name
    return name

def inspect_dll(dll_path, search_term=None, member_filter=None, ext_mode=False, deep_mode=False):
    results = []
    version = "Unknown"
    try:
        import System.Reflection as Reflection
        from System.Reflection import ReflectionTypeLoadException
        assembly = Reflection.Assembly.LoadFrom(os.path.abspath(dll_path))
        version = assembly.GetName().Version
        try: types = assembly.GetTypes()
        except ReflectionTypeLoadException as e: types = [t for t in e.Types if t is not None]
        except: return (version, [])

        for t in types:
            try:
                # Filtering for Public Classes, Interfaces, and Structs
                if not t.IsPublic or not (t.IsClass or t.IsInterface or t.IsValueType): continue
                
                type_name_full = f"{t.Namespace}.{t.Name}"
                if search_term and search_term.lower() not in type_name_full.lower(): continue
                
                flags = (Reflection.BindingFlags.Public | Reflection.BindingFlags.Instance | 
                         Reflection.BindingFlags.Static | Reflection.BindingFlags.FlattenHierarchy)
                
                temp_items = []
                def match_filter(name): return not member_filter or member_filter.lower() in name.lower()

                # 1. FIELDS (Deep Mode)
                if deep_mode:
                    for f in t.GetFields(flags):
                        if match_filter(f.Name):
                            prefix = "[ST] " if f.IsStatic else ""
                            temp_items.append(f"  [F] {prefix}{format_type_name(f.FieldType)} {f.Name}")
                
                # 2. METHODS
                for m in t.GetMethods(flags):
                    if m.IsSpecialName: continue
                    if match_filter(m.Name):
                        params = ", ".join([f"{format_type_name(p.ParameterType)} {p.Name}" for p in m.GetParameters()])
                        prefix = "[ST] " if m.IsStatic else ""
                        temp_items.append(f"  - {prefix}{format_type_name(m.ReturnType)} {m.Name}({params})")
                
                # 3. PROPERTIES (Extended/Deep Mode)
                if ext_mode or deep_mode:
                    for p in t.GetProperties(flags):
                        if match_filter(p.Name):
                            is_static = any(m.IsStatic for m in p.GetAccessors())
                            prefix = "[ST] " if is_static else ""
                            temp_items.append(f"  [P] {prefix}{format_type_name(p.PropertyType)} {p.Name}")

                if temp_items:
                    t_type = "Struct" if t.IsValueType else "Class"
                    # Include Base Class info if it exists and isn't just 'Object'
                    base_info = f" : {t.BaseType.Name}" if t.BaseType and t.BaseType.Name != "Object" else ""
                    results.append(f"\n[NS: {t.Namespace}] -> {t_type}: {t.Name}{base_info}")
                    results.extend(temp_items)
            except: continue
    except: pass
    return (version, results)

def main():
    switches = ["-s", "--search", "-f", "--filter", "-e", "--ext", "-d", "--deep", "-y", "--default", "-o", "--open", "-h", "--help"]
    if "-h" in sys.argv or "--help" in sys.argv:
        print("OPTIONS: -s (Search term), -f (Member filter), -e (Show Props), -d (Deep scan), -y (Skip path prompt), -o (Open result in VS Code)")
        return

    ext_mode = "-e" in sys.argv or "--ext" in sys.argv
    deep_mode = "-d" in sys.argv or "--deep" in sys.argv
    use_default = "-y" in sys.argv or "--default" in sys.argv
    open_vscode = "-o" in sys.argv or "--open" in sys.argv
    
    search_term = None
    if "-s" in sys.argv:
        idx = sys.argv.index("-s")
        if idx + 1 < len(sys.argv) and sys.argv[idx+1] not in switches:
            search_term = sys.argv[idx+1]

    member_filter = None
    if "-f" in sys.argv:
        idx = sys.argv.index("-f")
        if idx + 1 < len(sys.argv) and sys.argv[idx+1] not in switches:
            member_filter = sys.argv[idx+1]

    print(f"--- .NET DLL Inspector v2.26 ---")
    print(f"Switches: -s <term>, -f <member>, -e (Props), -d (Deep), -y (DefaultPath), -o (Open VSCode)")
    
    if use_default:
        target_dir = os.path.abspath(cfg['path'])
    else:
        user_input = input(f"Path (Enter for default {os.path.basename(cfg['path'])}): ").strip()
        target_dir = os.path.abspath(user_input if user_input else cfg['path'])

    if not os.path.isdir(target_dir):
        print(f"Error: Directory '{target_dir}' not found.")
        return

    sys.path.append(target_dir)
    all_dlls = [f for f in os.listdir(target_dir) if f.lower().endswith('.dll')]
    dll_files = [f for f in all_dlls if any(k.strip().lower() in f.lower() for k in cfg['keywords'])] if cfg['keywords'] else all_dlls

    # Generate log name based on search/filter parameters
    clean_s = re.sub(r'[^\w]', '', search_term) if search_term else "All"
    clean_f = f"_filter_{re.sub(r'[^\w]', '', member_filter)}" if member_filter else ""
    log_dir_full = os.path.join(script_dir, cfg['log_dir'])
    if not os.path.exists(log_dir_full): os.makedirs(log_dir_full)
    
    log_path = os.path.join(log_dir_full, f"inspect_{clean_s}{clean_f}.txt")

    print(f"[INFO] Search: {search_term} | Filter: {member_filter} | Log: {log_path}")

    with open(log_path, "w", encoding="utf-8") as f:
        f.write(f"REPORT: {target_dir}\n")
        f.write(f"SEARCH: {search_term} | FILTER: {member_filter}\n" + "="*40 + "\n")
        for index, dll in enumerate(dll_files, start=1):
            print(f"\r[{index}/{len(dll_files)}] Analyzing {dll[:30].ljust(30)}", end="", flush=True)
            v, matches = inspect_dll(os.path.join(target_dir, dll), search_term, member_filter, ext_mode, deep_mode)
            if matches:
                f.write(f"\nFILE: {dll} (v{v})\n" + "="*40 + "\n" + "\n".join(matches) + "\n")

    print(f"\n\nDONE! Results: {log_path}")
    if open_vscode:
        # Opens the log file in the currently active VS Code instance
        subprocess.run(["code", log_path], shell=True)

if __name__ == "__main__":
    main()