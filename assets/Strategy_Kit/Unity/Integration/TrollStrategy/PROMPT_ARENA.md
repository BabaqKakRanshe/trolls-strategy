Подключи новый вид арены «Луг» из кита Vitaria (выгрузка от 01.10). Арена теперь парящий остров, как колония: озера нет, обрыв плато — те же пласты, что у острова колонии, водопады и река падают в облака, под островом облака и голубая дымка. Поле, клетки, препятствия, лагеря, мельница, мост, замок, деревня, кадр камеры боя и эффекты — прежние, на тех же местах.

Задача: бой должен выглядеть как колония — то же низкое тёплое солнце, ровный амбиент, туман, дымка IslandHaze и грейдинг. Сейчас бой гасит туман (BattleArenaLighting: RenderSettings.fog = false), ставит свой trilight и тень .8, а ColonyVolume на время боя выключен (IslandAtmosphere). Без этой работы новая арена будет бледной и плоской.

Если у тебя есть доступ к редактору (Unity MCP), сам смотри Console, запускай Play Mode и снимай Game view. Если нет — говори мне, что сделать и что прислать.

## Что изменилось в ките

Assets/Vitaria (перезаписывает выгрузка кита, руками не правь):
- **Models/Arena/Meadow:**
  - перевыгружены Arena_Meadow_Ground, _Cliffs, _Roads, _River, _Falls, _Scatter;
  - новый **Arena_Meadow_Sky** — облака вокруг и под островом и четыре дальних островка, одним мешем (28 тыс. треугольников; вся арена — 130, было 103 тыс.);
  - **Arena_Meadow_Water** и **Arena_Meadow_Foam** больше не выгружаются: удали их вместе с .meta.
- Остальные модели Models/Arena (плитки, препятствия, реквизит, FX, Backdrop, мельница, мост) не менялись.
- **Layout/arena_meadow_layout.json:**
  - objects — прежние, кроме воды: озера и пены нет, Arena_Meadow_Sky в группе "Sky";
  - sockets — без "mist": водопады уходят в облака; дым и искры прежние;
  - sun — солнце колонии: forward, color, intensity 1.8 и новое shadowStrength 0.92;
  - ambient — новое поле color, ровный амбиент колонии; sky, equator и ground равны ему, поэтому нынешний код даёт тот же цвет;
  - background — цвет неба колонии;
  - новые блоки **fog, post, haze** — тот же формат и те же числа, что в isle_layout.json. В ките они берутся из одной функции (Blender/build_isle.py, game_look), поэтому вид колонии и боя не разойдётся. Глубины haze считаются вниз от уровня поля, как у колонии от газона;
  - framing, board, tiles, fx — без изменений.

Strategy_Kit\Blender: твои числа вида колонии в build_isle.py (солнце, амбиент, небо, туман, пост, дымка) перенесены без изменений в функцию game_look — блоки isle_layout.json из неё выходят те же.

Эталон вида — Strategy_Kit\Previews:
- arena_meadow_hero_isle.png — 16:9, arena_meadow_wide_isle.png — 21:9;
- colony_isle_start.png — колония, для сравнения тона;
- arena_meadow_hero.png и _wide.png — прежняя арена.

## Шаги

1. **Импорт.**
   - Дождись импорта. ArenaPrefabBuilder сам пересоберёт Assets/Game/Prefabs/Arenas/Arena_Meadow.prefab: раскладка поменялась.
   - Удали Arena_Meadow_Water.fbx и Arena_Meadow_Foam.fbx (с .meta) из Assets/Vitaria/Models/Arena/Meadow.
   - В Console нет «missing»; Arena_Meadow_Sky — в группе Sky; River на Vitaria_Water, Falls на Vitaria_Waterfall.

2. **Вид арены в префабе (ArenaPrefabBuilder).**
   - Читай из раскладки sun.shadowStrength, ambient.color, fog, post, haze. Классы и сборку профиля не копируй: вынеси из IslandEnvironmentBuilder общий код — данные (IsleSun, IsleFog, IslePost, IsleHaze) и заполнение профиля по post и haze (EnsureVolumeProfile) — и зови его из обоих билдеров.
   - Профиль Assets/Game/Prefabs/Arenas/Arena_Meadow_Volume.asset — те же компоненты, что у Colony_Isle_Volume: Tonemapping (Neutral), Color Adjustments, White Balance, Lift Gamma Gain, Bloom, Depth Of Field (Gaussian), Vignette, IslandHaze.
   - Высоты IslandHaze считай от 0: корень арены стоит на уровне поля.
   - На корне префаба — глобальный Volume с этим профилем, на том же слое, что ColonyVolume: камера боя копирует volumeLayerMask камеры колонии. Префаб живёт только в сцене боя, поэтому вид арены действует только в бою.
   - Рендереры группы Sky — без теней (ShadowCastingMode.Off), как небо колонии в IslandEnvironmentBuilder.
   - BattleArenaSet: добавь поля вида и метод для билдера:
     - есть ли вид;
     - ровный амбиент и сила тени солнца;
     - цвет тумана, fogStartPerDistance, fogEndPerDistance;
     - dofStartPerDistance, dofEndPerDistance;
     - ссылка на Volume арены.

     Старая раскладка без fog — бой как раньше.
   - Подними BuilderVersion, чтобы префаб пересобрался новым кодом.

3. **Свет боя (BattleArenaLighting).**
   - Apply, если у арены есть вид:
     - ambient Flat с цветом ambient.color, как у колонии;
     - тень солнца — shadowStrength из раскладки вместо .8;
     - вместо RenderSettings.fog = false — линейный туман цветом fog.color; дистанции ставит камера (шаг 4).
   - Restore возвращает режим, цвет и обе дистанции тумана и амбиент колонии. Это обязательно: IslandAtmosphere не переставляет туман после боя, если камера колонии не двигалась.
   - Без вида арены — как сейчас.

4. **Камера (BattleSceneController.FrameCamera)** — после расчёта distance:
   - IslandAtmosphere.Apply(distance, volume.profile, fogStartPerDistance, fogEndPerDistance, dofStartPerDistance, dofEndPerDistance) — готовый статический метод. volume.profile — копия профиля у Volume арены; ассет не трогай.
   - На узких экранах камера отъезжает — туман и резкость отъезжают вместе с ней.
   - При 16:9 distance 27 м: туман 23–81 м, резко до 38 м, полное размытие с 54 м. Поле резкое, замок и деревня чуть мягче.
   - Сейчас BattleBoardView ставит корень арены на высоту поля, y = 0. Если это изменится — сдвинь fogStart и fogFull IslandHaze на эту высоту, на той же копии профиля.

5. **Проверка.**
   - Снимки боя 16:9 и 21:9 сравни с превью кита:
     - пласты обрыва под полем уходят вниз в голубую дымку;
     - слева водопады падают в расщелину и облака, справа река срывается с кромки (видно в 21:9);
     - дальняя часть (замок, деревня) в лёгкой дымке, поле резкое;
     - тени длинные, к левому верхнему углу кадра, как в колонии;
     - фон камеры нигде не проглядывает дырой.
   - Поле читается как раньше: клетки, синие и красные зоны, препятствия, бойцы, подсветка, всплывающие числа, эффекты.
   - После боя колония как до него: туман, амбиент, солнце, ColonyVolume, дымка. Два боя подряд выглядят одинаково.
   - Замерь FPS боя до и после (на iPad, если есть).

## Ограничения
- FBX, раскладки, текстуры и материалы в Assets/Vitaria не правь. Замечания к арту — списком в отчёт.
- Правки скриптов кита (Island*.cs, IslandHaze*, DioramaSurfaceSetup.cs, ThreeDSceneSetup.cs, VitariaTools.cs) и новый общий код рядом с IslandEnvironmentBuilder повтори в Strategy_Kit\Unity\Integration\TrollStrategy и Strategy_Kit\Unity\Assets\Vitaria\Editor.
- Вид колонии и боя меняй в ките, в build_isle.py (game_look), и выгружай обе сцены: build_isle.py --export, затем build_arena.py. Если подбираешь числа в Unity — перенеси их в game_look.
- Domain и Application не трогай.

## Отчёт
- Что поменял: файл и суть.
- Снимки боя 16:9 и 21:9 рядом с превью кита и где расходятся.
- Значения профиля арены, если подбирал отдельно от колонии.
- FPS боя до и после.
- Замечания к арту.
