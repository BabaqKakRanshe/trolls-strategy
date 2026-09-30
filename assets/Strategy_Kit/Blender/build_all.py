"""
Сборка кита Vitaria одной командой. Каждый шаг — свой скрипт в отдельном процессе Python с bpy
(как запускать по одному — в их шапках); все они открывают один vitaria_kit.blend.

    python build_all.py [--blend vitaria_kit.blend] [--only buildings,arena,colony,isle,icons] [--previews]
                        [--blender "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe"]

Python — тот, где есть модуль bpy (pip install bpy); или --blender: тогда каждый шаг идёт как
blender -b --python <шаг> -- <аргументы>.

Шаги по порядку (порядок важен: колония и иконки берут здания из .blend, который сохранил первый шаг):
  buildings  add_buildings.py   палитра, 12 производственных зданий по контракту -> Models/Buildings
  arena      build_arena.py     арена «Луг» -> Models/Arena, Layout/arena_meadow_layout.json
  colony     build_colony.py    «Долина», казарма и склад, фон, детали префабов -> Models/Colony, Models/Buildings,
                                Layout/colony_meadow_layout.json (с --previews ещё превью 16:9, 21:9, 4:3 и «отстроенная»)
  isle       build_isle.py      остров колонии, растущий блоками -> Models/Isle, Layout/isle_layout.json
                                (с --previews ещё кадры этапов start, mid, max)
  icons      render_icons.py    атласы иконок зданий и ресурсов -> Icons/
После шагов:
  * проверка: имя каждой модели в Unity/Assets/Vitaria/Models встречается один раз (сборщики в Unity
    ищут модели по имени), каждая модель из раскладок есть на диске;
  * Unity/Assets/Vitaria/kit_version.json: версия кита (build_vitaria.KIT_VERSION), дата, шаги.
Любая ошибка останавливает сборку с ненулевым кодом выхода.
"""
import os, sys, json, subprocess, time, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, ".."))
UNITY = os.path.join(ROOT, "Unity", "Assets", "Vitaria")
STEP_NAMES = ["buildings", "arena", "colony", "isle", "icons"]


def parse_args():
    argv = sys.argv[1:]
    o = {"blend": os.path.join(HERE, "vitaria_kit.blend"), "only": STEP_NAMES, "previews": False, "blender": None}
    i = 0
    while i < len(argv):
        if argv[i] == "--blend" and i + 1 < len(argv):
            o["blend"] = os.path.abspath(argv[i + 1]); i += 1
        elif argv[i] == "--only" and i + 1 < len(argv):
            o["only"] = [x for x in argv[i + 1].split(",") if x]; i += 1
        elif argv[i] == "--previews":
            o["previews"] = True
        elif argv[i] == "--blender" and i + 1 < len(argv):
            o["blender"] = argv[i + 1]; i += 1
        i += 1
    bad = [x for x in o["only"] if x not in STEP_NAMES]
    if bad:
        sys.exit("неизвестные шаги: %s (есть %s)" % (", ".join(bad), ", ".join(STEP_NAMES)))
    return o


def steps(o):
    b = o["blend"]
    colony = ["build_colony.py", "--blend", b, "--export", "--save"]
    if o["previews"]:
        colony += ["--render", "--shots", "hero,wide,ipad"]
    out = [("buildings", ["add_buildings.py", "--blend", b]),
           ("arena", ["build_arena.py", "--blend", b]),
           ("colony", colony)]
    if o["previews"]:
        out.append(("colony", ["build_colony.py", "--blend", b, "--render", "--shots", "hero", "--preview", "grown",
                               "--tag", "_grown"]))
    isle = ["build_isle.py", "--blend", b, "--export"]
    if o["previews"]:
        isle += ["--render", "--stages", "start,mid,max"]
    out.append(("isle", isle))
    out.append(("icons", ["render_icons.py", "--blend", b]))
    return [(n, cmd) for n, cmd in out if n in o["only"]]


def check_models():
    """Имена моделей уникальны по всему Models/, модели из раскладок существуют."""
    models = collections.defaultdict(list)
    for d, _, files in os.walk(os.path.join(UNITY, "Models")):
        for f in files:
            if f.lower().endswith(".fbx"):
                models[os.path.splitext(f)[0]].append(os.path.relpath(os.path.join(d, f), UNITY))
    dup = {k: v for k, v in models.items() if len(v) > 1}
    missing = set()
    lay_dir = os.path.join(UNITY, "Layout")
    for f in sorted(os.listdir(lay_dir)) if os.path.isdir(lay_dir) else []:
        if not f.endswith("_layout.json"):
            continue
        lay = json.load(open(os.path.join(lay_dir, f), encoding="utf-8"))
        for it in lay.get("objects", []):
            if it.get("asset") and it["asset"] not in models:
                missing.add("%s: %s" % (f, it["asset"]))
        for b in lay.get("blocks", []):                       # остров: модель каждого блока
            if b.get("model") and b["model"] not in models:
                missing.add("%s: block %s" % (f, b["model"]))
        for role in (lay.get("fx") or {}).values():
            for n in role or []:
                if n not in models:
                    missing.add("%s: fx %s" % (f, n))
    return dup, sorted(missing), len(models)


def main():
    o = parse_args()
    py = sys.executable
    done = []
    t0 = time.time()
    for name, cmd in steps(o):
        print("\n=== %s: %s" % (name, " ".join(cmd)))
        t = time.time()
        full = [o["blender"], "-b", "--python", cmd[0], "--"] + cmd[1:] if o["blender"] else [py] + cmd
        r = subprocess.run(full, cwd=HERE)
        if r.returncode != 0:
            sys.exit("шаг %s упал (код %d)" % (name, r.returncode))
        done.append({"step": name, "seconds": round(time.time() - t, 1)})
    dup, missing, n = check_models()
    for k, v in sorted(dup.items()):
        print("!! модель %s лежит %d раза: %s" % (k, len(v), ", ".join(v)))
    for m in missing:
        print("!! в раскладке нет модели на диске —", m)
    if dup or missing:
        sys.exit("сборка кита: %d дублей имён, %d пропусков" % (len(dup), len(missing)))
    sys.path.insert(0, HERE)
    kit_version = "?"
    try:
        import re
        kit_version = re.search(r'KIT_VERSION = "([^"]+)"', open(os.path.join(HERE, "build_vitaria.py"), encoding="utf-8").read()).group(1)
    except Exception:
        pass
    info = {"kitVersion": kit_version, "built": time.strftime("%Y-%m-%d %H:%M"), "models": n, "steps": done}
    with open(os.path.join(UNITY, "kit_version.json"), "w", encoding="utf-8") as f:
        json.dump(info, f, indent=1, ensure_ascii=False)
    print("\nкит %s: %d моделей, шаги %s, %.0f с" % (kit_version, n, ", ".join(d["step"] for d in done), time.time() - t0))


if __name__ == "__main__":
    main()
