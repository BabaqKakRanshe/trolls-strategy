# Референсы для капсул Steam

Всё для генерации капсул собрано в одной папке: кадры игры, персонажи и промпты.

## Что лежит в папке

### style/ — как выглядит мир

| Файл | Что показывает | Для каких промптов |
|---|---|---|
| `01_island_colony_close.png` | застроенный остров вблизи (вырезан из скриншота без HUD) | 1, 3 |
| `02_island_colony_wide.png` | тот же остров издали, вокруг облака и малые острова | 1, 2 |
| `03_sky_islands_clouds.png` | небо, облака, малые острова с елями и водопадом, остров ещё пустой | 2 |
| `04_materials_showcase.png` | ассеты отдельно: грани, фаски, материалы, баннер, ветряк | шаг 0, 1, 3, 4 |
| `05_buildings_level1.png` | все здания первого уровня | 2 |
| `06_buildings_level3.png` | все здания третьего уровня | 2 |
| `07_arena.png` | гексовая арена с синим и красным баннерами | 3 |

На `01` и `02` видны линии сетки, на `01` ещё и подпись «Кузница 2». Это интерфейс. Строка про референсы ниже велит нейросети его не копировать. На `07` по гексам стоят мелкие пиксельные бойцы, из этого кадра берите только арену.

Застроенного острова без сетки пока нет. Когда снимем такой кадр, он заменит `01` и `02`.

### characters/ — существа в игре

| Файл | Что это |
|---|---|
| `troll_ingame_x24.png` | игровой тролль: кадр 9×9 пикселей, увеличен в 24 раза без сглаживания |
| `goblin_ingame_x24.png` | игровой гоблин: кадр 7×6 пикселей, увеличен в 24 раза |
| `all_species_ingame.png` | все 28 существ игры в одном листе |

**Эти спрайты не загружайте в нейросеть.** Это Minifantasy, у бесплатной версии лицензия только некоммерческая (`assets/licenses.json`). В кадре 9×9 пикселей нейросеть не увидит ничего, кроме цвета. Спрайты лежат здесь для сверки: тролль на капсуле не должен спорить с троллем в игре по цвету и силуэту.

Цвета из спрайтов (они уже вписаны в промпты словами):

| Существо | Основной цвет | Тени | Свет |
|---|---|---|---|
| Тролль | `#236b52` тёмный сине-зелёный | `#1d5946`, `#18493a`, `#12372d` | `#2a7b5c` |
| Гоблин | `#76b846` светло-зелёный, лаймовый | `#69a640`, `#5f9b3c`, `#538d36` | `#87c64f` |

Силуэт тролля в игре: широкий, приземистый, голова сливается с плечами. Гоблин в игре маленький и круглый.

## Порядок работы

1. Сгенерировать листы тролля и гоблина (шаг 0), выбрать по одному. Положить их сюда в `characters/` как `troll_sheet.png` и `goblin_sheet.png`.
2. Генерировать капсулы, прикладывая референсы по таблице ниже.
3. Логотип нарисовать отдельно, вручную и в векторе. Нейросети портят буквы, поэтому во всех промптах сказано «без текста».

| Промпт | Референс стиля | Референс персонажа |
|---|---|---|
| Шаг 0 | `04` | — |
| 1. Тролль-носильщик | `01` или `02` + `04` | `troll_sheet.png` |
| 2. Остров в облаках | `02` + `03` (здания: `06`) | — |
| 3. Кирка или меч | `01` + `07` + `04` | `troll_sheet.png` |
| 4. Бригадир и тролль | `04` | `troll_sheet.png` + `goblin_sheet.png` |

**Как приложить**

- Midjourney: кадры игры — через `--sref` (берётся только стиль), лист персонажа — через `--oref`. Скриншот не ставьте основной картинкой промпта, иначе нейросеть повторит его ракурс и композицию.
- ChatGPT или Gemini: приложите картинки по порядку и допишите в конец промпта:

```
Image 1 is the art-style reference only (shapes, materials, palette, light). Image 2 is the character reference: keep the troll's proportions and colours. Do not copy composition, camera, UI, labels or grid from the references.
```

## Промпты

### Шаг 0а. Лист тролля

```
Character turnaround sheet of one game character: a big friendly troll worker in stylized low-poly 3D, chunky faceted shapes with soft rounded bevels, flat shading, matte materials. Hunched, broad shoulders, long heavy arms, short legs, small head with two short tusks, dark teal-green skin, leather work apron, bare feet. Front, three-quarter, side and back views side by side, neutral pose, plain light grey background, even studio light. No text, no letters. --ar 16:9
```

### Шаг 0б. Лист гоблина

```
Character turnaround sheet of one game character: a small skinny goblin foreman in stylized low-poly 3D, chunky faceted shapes with soft rounded bevels, flat shading, matte materials. Round head, big pointed ears, wide grin, light lime-green skin, leather cap, short tunic with a belt and a coin pouch. Front, three-quarter, side and back views side by side, neutral pose, plain light grey background, even studio light. No text, no letters. --ar 16:9
```

### 1. Тролль-носильщик

Основной вариант: в одном кадре и жанр, и тролль, которого нет у других игр про острова.

```
Steam store key art for a cozy strategy game about a colony of trolls and goblins on a floating sky island. Stylized low-poly 3D render: chunky faceted shapes with soft rounded bevels, flat shading, clean matte materials, bright clear daylight, soft ambient occlusion, miniature diorama feel.
Foreground, right third: one big friendly troll in three-quarter view, hunched, broad shoulders, long heavy arms, small head with two short tusks, dark teal-green skin, leather work apron. The troll grins with effort and looks at the viewer while hauling on its back a wooden crate overflowing with grey ore and glowing violet crystals. Bold, clear silhouette.
Midground, left: a floating island seen from a high oblique strategy camera: lime-green grass top, layered sandy-tan rock underside, a compact village of timber-framed houses with plain royal-blue roofs, a stone mine entrance, a smelter with a smoking chimney, a market stall with a blue-and-white awning. Tiny goblins carry sacks and ingots along ochre dirt paths between the buildings.
Background: a sea of soft white clouds, two small floating islands with tiered teal pines, a thin waterfall spilling off an edge, sky deepening to saturated blue at the top.
Keep the upper-left quarter as clean empty sky for the title. One focal point, strong value contrast, uncluttered.
No text, no letters, no logo, no UI, no watermark. --ar 16:9
```

### 2. Остров в облаках

Мир на первом плане, существа мелкие, как в игре. Подходит для вертикальных капсул. Для library capsule повторите с `--ar 2:3`, для header — с `--ar 16:9`.

```
Steam store key art, vertical. A single floating island colony hovering above a sea of soft white clouds. Stylized low-poly 3D render: chunky faceted shapes with soft rounded bevels, flat shading, clean matte materials, miniature diorama feel, warm late-afternoon sun from the left with long soft shadows.
The island fills the middle of the frame, seen from a high three-quarter strategy camera: lime-green grass top, thick layered sandy-tan rock underside with hanging roots, a waterfall pouring off one edge into the clouds. On top, a busy compact village: timber-framed houses with plain royal-blue roofs, a grey stone mine entrance with an ore cart, a smelter with orange glow and smoke, a warehouse with stacked crates, a market stall with a blue-and-white awning, and at one edge a small hexagon-tiled arena with one blue and one red banner. Small dark teal-green trolls and light-green goblins carry ore, ingots and planks along ochre dirt paths.
Sky gradient from pale blue near the clouds to deep blue at the top; the top third is clean sky for the title.
No text, no letters, no logo, no UI, no watermark. --ar 5:6
```

### 3. Кирка или меч

Одни и те же существа и работают, и воюют. Привлекает любителей автобоёв, но кадр рискует выйти перегруженным.

```
Steam store key art for a strategy game where the same trolls and goblins mine, craft and then fight. Stylized low-poly 3D render: chunky faceted shapes with soft rounded bevels, flat shading, clean matte materials, bright daylight.
Centre: one big troll standing on the edge of a floating island, facing the viewer with a sly grin; dark teal-green skin, small head with short tusks, hunched broad build. Half worker, half warrior: leather work apron and a dented iron helmet, a pickaxe in the left hand, an iron sword in the right, a round wooden shield on the back.
Left half of the background, warm: the colony, with a stone mine entrance, ore carts, a smelter glowing orange with smoke, timber houses with plain royal-blue roofs, goblins carrying ore.
Right half of the background, cool: a grassy ledge with a hexagon-tiled arena, one blue banner and one red banner, a small squad of goblins in mismatched armour waiting to fight.
Soft white clouds below the island, deep blue sky at the top with clean space for the title above the troll.
No text, no letters, no logo, no UI, no watermark. --ar 16:9
```

### 4. Бригадир и тролль

Крупный план и юмор. Лучше всех читается в маленьком размере, из неё выйдет хорошая small-капсула.

```
Steam store key art, character close-up. Stylized low-poly 3D render: chunky faceted shapes with soft rounded bevels, flat shading, clean matte materials, bright daylight with a warm rim light.
A big good-natured troll fills the lower right half of the frame, seen from slightly below: dark teal-green skin, small head with two short tusks, heavy jaw, leather work apron, pushing a wooden mine cart heaped with ore and glowing violet crystals toward the viewer. On the troll's shoulder sits a small skinny goblin foreman with light-green skin, big ears and a leather cap, holding a coin pouch in one hand and pointing ahead with the other, shouting orders. Gold coins bounce out of the pouch.
Behind them, softly out of focus: a floating island village with plain royal-blue roofs and a smoking chimney, white clouds, deep blue sky.
Large expressive faces readable at thumbnail size; clean empty sky in the upper left for the title.
No text, no letters, no logo, no UI, no watermark. --ar 16:9
```

## Перед тем как ставить в Steam

- Проверить капсулу в 120×45, в оттенках серого и в сетке из 10 капсул конкурентов на тёмном фоне Steam.
- Все размеры брать из одного направления. Из основного кадра 16:9 вырезаются Main 1232×706 и Header 920×430. Small 462×174 — это логотип и голова тролля. Library hero 3840×1240 — панорама острова без текста, всё важное в центре 860×380. Vertical 748×896 и Library 600×900 генерируются отдельно.
- Если в капсуле осталось сгенерированное, отметить ИИ в анкете о контенте Steamworks.
