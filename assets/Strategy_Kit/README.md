# Vitaria: low-poly набор

50 ассетов, остров с дорожками, 4 комплекта «здание + предметы» и сцена из них (172 объекта). Исходный набор сгенерирован скриптом `Blender/build_vitaria.py`, после ручных правок экспорт делает `Blender/export_vitaria.py`.

## Что внутри

```
Blender/
  vitaria_kit_fixed.blend  твоя правка сцены: комплекты в коллекции Scene_Sets, ассеты набора восстановлены (Blender 5.0)
  export_vitaria.py        экспорт ТЕКУЩЕГО .blend в Unity, ручные правки не трогает
  build_vitaria.py         генератор набора с нуля: перезапуск затрёт ручные правки в сцене
Unity/Assets/Vitaria/
  Models/<Категория>/*.fbx по одному FBX на объект
  Models/Sets/Set_*.fbx    комплекты: шахта, склад, плавильня, рынок вместе со своими предметами
  Models/Vitaria_Scene.fbx вся сцена одним файлом (объекты раздельные), на всякий случай
  Textures/                палитра 256x256 + карта эмиссии (огонь плавильни, фонари)
  Layout/vitaria_layout.json  позиции/повороты всех объектов сцены + камера + солнце, уже в координатах Unity
  Editor/VitariaTools.cs   меню Tools > Vitaria
Previews/                  рендеры сцены и листы ассетов
asset_list.json            ассеты: категория, треугольники, габариты в метрах
```

## Импорт в Unity

1. Скопируй папку `Unity/Assets/Vitaria` в `Assets/` проекта.
2. Tools > Vitaria > 1. Setup Material: создаёт `Vitaria_Palette.mat` (URP Lit или Standard) и переназначает на него все модели.
3. Tools > Vitaria > 2. Create Prefabs (по желанию): prefab variant на каждую модель, с BoxCollider (у острова MeshCollider, у травы и дорожек коллайдера нет).
4. Tools > Vitaria > 3. Build Scene From Layout: расставляет сцену из Blender, группы по категориям. Если префабы есть, берёт их.
5. Tools > Vitaria > 4. Setup Lighting: солнце с мягкими тенями под тем же углом, что на превью, ambient-градиент, в URP-ассете дистанция теней 45 и shadowmap 2048.
6. Tools > Vitaria > 5. Align Main Camera To Preview: ракурс, FOV и фон как на превью (если у камеры свой контроллер, этот пункт можно пропустить).

Вручную, пара кликов, чтобы было как на превью:
- URP Renderer > Add Renderer Feature > Screen Space Ambient Occlusion (контактные тени под объектами дают больше всего «объёма»);
- Project Settings > Player > Color Space: Linear;
- Volume с Tonemapping Neutral и чуть Bloom, чтобы огонь плавильни и фонари светились.

## Договорённости

- 1 юнит = 1 м. Пивот внизу по центру. Фасад смотрит в +Z (forward в Unity).
- Центр острова (примерно 14x9 м) плоский, z = 0. Сетка и размещение зданий работают без подгонки по высоте. Бугры только у края, под деревьями.
- Один материал на всё: UV каждой грани указывает в свою ячейку палитры. Static batching и SRP Batcher работают сразу, ничего настраивать не нужно.
- Габариты зданий (ширина x глубина, высота): Mine 3.8x4.1, 2.0; House 3.9x3.5, 3.6; Market 3.7x2.4, 2.75; Smelter 2.0x2.1, 2.8.
- Комплекты `Set_*` стоят пивотом в точке здания (внизу по центру), предметы вокруг внутри того же меша. Отдельные бочки, ящики и прочее по-прежнему лежат в Props/Resources.
- Минионы остаются спрайтами, их в наборе нет.

## Экспорт после ручных правок

Открой .blend (например `vitaria_kit_fixed.blend`), вкладка Scripting > Open > `export_vitaria.py` > Run Script. Файл должен быть сохранён. Скрипт обновит `Unity/Assets/Vitaria` рядом с папкой Blender и `asset_list.json`. Из консоли: `blender -b vitaria_kit.blend --python export_vitaria.py`.

Как скрипт понимает сцену:
- ассеты набора лежат в коллекциях `Vitaria_Kit/Kit_<Категория>`, сцена в `Vitaria_Scene/Scene_<Категория>`;
- объект сцены с тем же мешем, что у объекта набора, считается его копией и в Unity ставится тот же префаб;
- объект сцены со своим мешем экспортируется отдельной моделью под именем меша в папку своей категории. Так сделаны комплекты в `Scene_Sets`.

Важно перед Ctrl+J: у копий из набора меш общий с оригиналом. Если объединять в копию, геометрия попадёт и в оригинал набора. Сначала Object > Relations > Make Single User > Object & Data, потом Ctrl+J. Если это всё же случилось, скрипт напишет в консоли `WARNING: kit asset ... changed` (сравнивает с прошлым `asset_list.json`).

После нового экспорта в Unity: замени папку `Assets/Vitaria` (кроме своих Materials/Prefabs, если уже сделал), удали старый объект `Vitaria_Scene` и снова запусти Tools > Vitaria > 2 и 3.

## Изменить или добавить

Генератор, `build_vitaria.py` (пересоздаёт набор с нуля):
- цвета: список `PALETTE` (hex), градиенты травы и земли: `RAMPS`;
- новый ассет: функция `build_xxx(a)` из примитивов `p_box / p_cyl / p_ico / p_prism` и строка в `KIT`;
- расстановка: `BUILDINGS`, `PROPS`, дорожки: `PATHS`.

Запуск: `blender --background --python build_vitaria.py -- --out ./Vitaria_Kit` (или `--render` для превью). В GUI Blender: открыть скрипт в Text Editor пустого файла и нажать Run Script. Результат по умолчанию ложится в `~/Vitaria_Kit`.
