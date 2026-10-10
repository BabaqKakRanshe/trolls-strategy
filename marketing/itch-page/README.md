# Страница itch.io: три варианта

Выбран вариант 2 «Цепочка» (2026-10-09). Макеты всех трёх страниц и обложек — на канвасе: https://claude.ai/artifact/7YTbgLRwQfh6fqJJGoLSu3

Картинки в `images/` вырезаны из кадров игры скриптом `make_images.py` (запуск: `python make_images.py <repo> <out>`). Логотипа в них нет: название «Trollstead» на макетах набрано шрифтом Lilita One как заглушка. Название взято с макетов капсул Steam; на itch оно свободно (поиск 2026-10-09).

## Что работает на itch (исследование 2026-10-09)

- **Игра в браузере — главное.** У браузерных игр в браузере играет в среднем 37 % зашедших, у скачиваемых — 6–7 % скачивают (опрос 169 разработчиков). [howtomarketagame.com](https://howtomarketagame.com/2025/05/12/benchmark-itch-io-traffic/)
- **Скриншоты при встроенной игре по умолчанию скрыты.** Настройка Screenshots стоит в Auto, а Auto прячет колонку на странице с embed. Ставить Sidebar явно и всё равно класть GIF в описание. [itch.io/docs/creators/design](https://itch.io/docs/creators/design)
- **GIF вместо статики.** Минимум 4 скриншота или GIF; хотя бы один GIF должен продавать игру сам. Нарезать по 3–6 секунд из трейлера. [itch.io/blog/34049](https://itch.io/blog/34049)
- **Своя тема, а не чёрное на белом.** Баннер itch называет главной картинкой страницы: он заменяет заголовок, может быть прозрачным PNG или GIF. Шрифты — любые Google Fonts, цвета: BG, BG2 (с прозрачностью), Text, Link, Buttons, Headers. Колонка 960 px.
- **Обложка 630×500**, минимум 315×250. Она уходит в каталог, теги и джемы; в сетке видна в половину размера. Обрезается от центра.
- **Трейлер** (YouTube/Vimeo) встаёт над скриншотами и добавляет кнопку «Watch trailer» в сетке каталога.
- **Альфа честно.** Статус In development, блоки «что в сборке / что дальше / как дать отзыв». Обещать уют, когда в игре бои и гибель бойцов, вредно: у Tiny Kingdom несоответствие обещания испортило запуск. Образцы: [Pathipelago](https://onebitkid.itch.io/pathipelago), [Minami Lane](https://doottinygames.itch.io/minami-lane), [Songs of Syx](https://songsofsyx.itch.io/songs-of-syx), [Tiny Kingdom](https://neltile.itch.io/tiny-kingdom), [Gourdlets](https://aunty-games.itch.io/gourdlets).
- **Языки:** английский текст первым, русский ниже на той же странице. Отдельные страницы делят оценки и загрузки. В поле Languages — только языки, которые есть в игре: все 15 переводов полные (`validate.py`: 971 ключ, 0 ошибок).
- **Теги:** до 10, из существующих, без движка (Unity идёт в Made with). Жанр — отдельным полем.
- **Steam:** бейджа нет, ссылка идёт в Links. Страница только со ссылкой на Steam без сборки нарушает правила.
- **Фичеринг** выбирают люди, правил нет. Помогают девлоги и пост в форуме Release Announcements. Нарушение quality guidelines (спам тегами, неверная классификация) убирает игру из поиска.
- **SharedArrayBuffer не включать:** он переносит игру на другой домен (теряются сохранения в localStorage) и ломает iframe на странице, в том числе YouTube.

## Как писать текст и «Coming next» (исследование 2026-10-09)

**Текст страницы**
- **Первые 10 слов — жанр.** Игрок пробегает описание глазами в поисках слов о жанре и о том, что он будет делать. Сюжет и пейзаж он пропускает. Глаголы механик лучше общих «сражайся со злом». Назвать игру-ориентир: игрок всё равно сравнит с главной игрой жанра. [Зуковски, чеклист](https://howtomarketagame.com/wp-content/uploads/2020/03/SteamPageChecklistv1.pdf)
- **Подзаголовки — шаги цикла**, каждый начинается с глагола, между ними GIF; список фич — в самом низу. «Verbs are gameplay; nouns are lore» ([60 Mistakes](https://howtomarketagame.com/wp-content/uploads/2023/05/Zukowski_60MistakesEbookV1.pdf)). Образец — подзаголовки [Captain of Industry](https://store.steampowered.com/app/1594320/).
- **Без пустых слов:** immersive, unique, epic, stunning, amazing, addictive, «experience» как глагол, journey, adventure. Вместо них — деталь или число.
- **Альфу описывать по текущему состоянию.** Правила раннего доступа Steam: игрок решает по тому, что есть сейчас, а о поломке сохранений говорят заранее. [Steamworks: Early Access](https://partner.steamgames.com/doc/store/earlyaccess)
- **Призыв к действию — в конце и ясный:** подписка ради девлогов, отзыв в комментариях, Discord. «Wishlist on Steam» — только когда страница в Steam откроется.

**Дорожная карта**
- **Статусы вместо дат:** Done / Now / Next / Later, по 3–5 пунктов, каждый — что получит игрок, а не задача разработчика («колония переживёт закрытие вкладки», а не «сериализация»). [ProdPad: Now/Next/Later](https://www.prodpad.com/blog/invented-now-next-later/)
- **Оговорка и дата:** Steam советует не обещать того, что зависит от продаж и отзывов, и лучше обещать меньше, чем потом переписывать. [Steamworks: Roadmap](https://partner.steamgames.com/doc/store/roadmap) Captain of Industry пишет «for transparency, not a guarantee». [coigame.com/roadmap](https://coigame.com/roadmap)
- **Сделанное остаётся видно** (Captain of Industry, «The journey so far»), а список «Not planned» задаёт границы.
- **Устаревшая карта хуже никакой:** на страницах Private Military Manager и Fool King до сих пор висят прошедшие сроки и «скоро в Steam». Обновлять примерно раз в три недели, иначе страница выглядит заброшенной (Зуковски).
- **Текстом, а не картинкой:** картинку пришлось бы перерисовывать под каждый язык и каждое обновление. Статусы как подзаголовки читаются в любом редакторе — так сделано у [Pixel Colony](https://pelzergames.itch.io/pixel-colony).

## Общее для всех вариантов

**Деньги — No payments.** Существа — бесплатный Minifantasy, лицензия только некоммерческая (`assets/licenses.json`, `commercialReleaseBlocked`). «Плати сколько хочешь» или донаты делают альфу коммерческой. Включать оплату только после покупки коммерческой лицензии или замены спрайтов.

**Загрузки:**
- `Builds/TrollStrategy-itch-webgl.zip` (меню `TrollStrategy/Build WebGL Player (itch.io)`) — «This file will be played in the browser». Embed in page, 960×600, Fullscreen button включить, Mobile friendly выключить (телефоны всё равно запускают во весь экран), SharedArrayBuffer выключить.
- Windows-сборка zip — галочка Windows.

**Поля:**

| Поле | Значение |
|---|---|
| Classification | Games |
| Kind of project | HTML (Windows-сборка — второй загрузкой) |
| Release status | In development |
| Genre | Strategy (вторым — Simulation, если даст) |
| Made with | Unity |
| Average session | About a half-hour |
| Languages | English, Russian, Ukrainian, Kazakh, German, French, Spanish, Portuguese, Italian, Polish, Czech, Turkish, Japanese, Korean, Chinese, Hindi |
| Inputs | Mouse, Keyboard |
| Accessibility | Interactive tutorial (проверить точное название пункта в форме) |
| Links | Discord, Telegram, YouTube; Steam — когда откроется страница |
| Community | Comments включить |

**Проверенные теги** (2026-10-09, число игр): City Builder 3 227, Management 8 125, Economy 1 880, Automation 1 326, Auto Battler 1 269, Cozy 13 418, Low-poly 27 405, Fantasy 40 987; свободные: colony-sim 90, base-building 272, troll 164. Не брать: Singleplayer и Pixel Art (переполнены, Pixel Art не про нас), Unity.

**Титры** обязательны: Minifantasy — Krishna Palacio; музыка YannZ — CC BY 4.0; xDeviruchi — если его треки остались в сборке.

**Управление** (сверено с `Hotkeys.cs` и `IslandCameraRig.cs`): левая кнопка — выбор, рамка — группа; правая — приказы; WASD, стрелки или курсор у края — камера; колесо — зум; C — каталог, E — работа, H — перенос, V — арена, L — земля, K — справочник, Q — задание, Esc — меню.

## Вариант 1. Остров в облаках

Кому: любителям уютных строителей. Мир целиком, светлая тема в цветах игры, игра сразу под баннером, справа скриншоты.

| Тема | |
|---|---|
| Layout | две колонки, Screenshots: Sidebar |
| BG | `#CFE0F2`, фон `images/bg-sky.jpg`, по центру сверху, без повтора |
| BG2 | `#FFFFFF` |
| Text / Link / Buttons | `#1E2A40` / `#2F5BAE` / `#F6C343` |
| Шрифты | заголовки Lilita One, текст Nunito |
| Баннер | `images/header-island.jpg` + логотип |
| Обложка | `images/cover-island.jpg` + логотип |
| Embed: фон до запуска | `images/shot-island.jpg` с затемнением |

**Short description:** A cozy colony of trolls and goblins on a floating island.

**Теги:** City Builder, Cozy, Low-poly, Fantasy, Management, Economy, Auto Battler, colony-sim, base-building, troll.

**Текст:**

> **Grow a little colony of trolls and goblins on an island above the clouds.**
>
> Goblins dig ore. Trolls haul it to the market. Every coin in your treasury came out of the ground on somebody's back. And when the barracks fill up, the same workers put on armour and go to the arena.
>
> *[GIF: goblins carry ore from the mine to the market]*
>
> ## How it plays
> - **Hire** goblins and trolls. Each one does one job at a time: works, carries or fights.
> - **Build** a mine, a market, a smelter, a forge, fields and farms.
> - **Draw routes.** Pick a source, pick what to carry, click where it goes. Then watch the goods walk.
> - **Sell or keep.** Ore sells right away. Ingots and swords sell for more, or go on your fighters.
> - **Fight.** Pick a squad, dress it, place it on the hexes. The battle plays itself.
>
> ## This is an alpha
> You get the tutorial campaign: nine quests and your first arena fights, about 20 minutes. Progress is not saved yet. If something breaks, press F8 and send the logs, or leave a comment below. I read every one.
>
> ## Controls
> Left click: select; drag to select a group. Right click: orders. WASD or arrows: move the camera (or push the mouse to the screen edge). Wheel: zoom. Every button shows its key: C build, E work, H haul, V arena, Esc menu.
>
> In 16 languages: English, Русский, Українська, Қазақша, Deutsch, Français, Español, Português, Italiano, Polski, Čeština, Türkçe, 日本語, 한국어, 中文, हिन्दी.

## Вариант 2. Цепочка — выбран 2026-10-09

Кому: любителям логистики и автоматизации. Тёмная тема; текст идёт по шагам цикла, полная игра — отдельным честным блоком, дорожная карта по статусам.

| Тема | |
|---|---|
| Layout | две колонки, Screenshots: Sidebar |
| BG / BG2 | `#121A27` / `#1E2A40` |
| Text / Link / Buttons / Headers | `#E8EEF7` / `#F6C343` / `#F6C343` (текст кнопки `#1E2A40`) / `#F6C343` |
| Шрифты | заголовки Lilita One, текст Nunito |
| Баннер | `images/header-chain.jpg` + логотип слева, затемнение к левому краю |
| Обложка | `images/cover-chain.jpg` + логотип (или островная `images/cover-island.jpg`: силуэт лучше читается в сетке) |
| Картинка в описании | схема 960×220: шахта → руда → плавильня → слиток → кузница → меч → рынок / арена (иконки из атласов `resources-icons` и `buildings-icons`) |

**Short description:** A cozy incremental colony sim: goblins and trolls haul every ore by hand, then fight in an auto-battle arena.

**Теги** (на itch с 2026-10-09; только те, которым игра соответствует по определениям itch, самые крупные из них): 3D (181 тыс. игр), Short (154 тыс., пока альфа на 20 минут — убрать с полной игрой), Fantasy (79 тыс.), Casual (75,5 тыс.), Low-poly (37,6 тыс.), Cozy (17 тыс.), Management (8,1 тыс.), Incremental (7,1 тыс.: цель — золото, покупка и улучшение зданий и существ), City Builder (3,2 тыс.), Auto Battler (1,3 тыс.). Не ставить: Idle и Clicker (без игрока колония стоит), Pixel Art (мир low-poly), No AI.

**Текст** (детектор humanizer: 0 ошибок, 0 предупреждений, ритм cv 0,6):

> **Trollstead is a cozy incremental colony builder with an auto-battle arena, in the spirit of The Settlers.**
>
> Hire goblins and trolls, dig ore and haul it to the market by hand. Sell it, or use what you earn to arm the same workers and send them to fight. Nothing teleports here: every ore crosses the island on somebody's back.
>
> This is a free alpha: the tutorial campaign, about 20 minutes, in your browser or on Windows. There are no saves yet, so a closed tab means a fresh island.
>
> ## Mine it and carry it
> Put goblins in the mine, then draw a route: pick where to load, what to carry and where to drop it. The haulers walk it there one load at a time. A goblin is cheap and quick. A troll digs harder and carries more, but walks slower. When the market is far or the haulers are busy, the gold slows down, and finding out why is the game.
>
> *[GIF: haulers on the mine to market route]*
>
> ## Sell it, then arm your squad
> Ore sells for quick gold at the market, and the gold hires more hands. Trophies won in the arena travel the same way: haulers carry them to the armory, and from there they go onto your fighters.
>
> ## Send them to the arena
> Pick your fighters, give them the gear you have and place them on the hexes. The battle plays itself, so who you sent and what they wear decides it. A win pays gold and trophies. A fighter who falls is gone for good, along with everything they wore, and their job at the mine stands empty.
>
> *[GIF: an arena battle at 2x, then the reward reel]*
>
> ## Built, not in the alpha yet
> The rest of the game is built and being balanced:
> - 16 buildings with three levels each, among them a smelter, a forge, a tavern and an enchanter
> - 11 kinds of workers: nine join after arena wins, each faster in its favourite buildings
> - 30 arena levels, with a champion on every fifth
> - 34 quests
>
> *[image: the production chain]*
>
> ## Coming next
> As of October 2026. These are plans, not promises, and your comments move them around.
>
> ### Done
> The tutorial island, the first arena fights, 16 languages and bug reports from inside the game.
>
> ### Now
> Saves, with an autosave and four slots. Until they arrive, every update starts a fresh colony.
>
> ### Next
> - A layout that fits a phone screen.
> - Creatures you can tell apart at work: dwarves are faster in the mine, hobbits on the farm.
> - Goods your colony uses itself instead of only selling them.
>
> ### Later
> - New arenas in the forest, the swamp, the graveyard and the snow.
> - A demo on Steam.
>
> ### Not planned
> Steering fighters during a battle. The fight is decided before it starts.
>
> ## Help shape it
> Tell me in the comments where you got stuck and what you made your trolls do. If something breaks, press F8 and send the logs. Follow the game here to get each devlog.
>
> ## Controls
> Left click selects, drag selects a group, right click gives orders. WASD, the arrow keys or the screen edge move the camera, and the wheel zooms. Every button shows its key.
>
> Languages: English, Russian (the original) and 14 more.
>
> Made in Unity. Creatures: Minifantasy by Krishna Palacio. Music: YannZ (CC BY 4.0).
>
> ## По-русски
> Колония троллей и гоблинов на парящем острове, в духе The Settlers. Гоблины добывают руду, носильщики несут её на рынок, а на заработанное те же работники надевают броню и идут на арену. Бой идёт сам: исход решают состав и снаряжение. Погибший боец не вернётся.
>
> В альфе — обучение: девять заданий, около 20 минут. Сохранений пока нет. Если что-то сломалось, нажмите F8 → «Отправить логи» или напишите в комментариях. Игра написана на русском.

**Откуда факты.** В альфе (обучение до «В броне на арену», `docs/GDD.md` §9.1, §9.3) есть шахта, склад, рынок, бараки, склад экипировки, гоблины и тролли; плавильни и кузницы нет, поэтому они только в блоке «Built, not in the alpha yet». 16 зданий — атлас `buildings-icons`; 11 видов и любимые здания — `specs/005-creature-roles`; 30 уровней и чемпионы на вехах — GDD §6.4; 34 задания — GDD §9.2. Пока идёт бой, колония стоит (GDD §6.1), поэтому работник пропадает с работы только погибнув. «Coming next»: сохранения — решение 2026-10-09; телефон — `specs/003-mobile-ui`; гномы и хоббиты — `specs/005-creature-roles`; товары для колонии — `specs/004-colony-consumers`; биомы — GDD §6.4; «Not planned» — опора №4 из GDD §3.

**Подтвердить до публикации:** сравнение с The Settlers (это ориентир для игрока, Зуковски советует называть его); порядок «Next / Later»; «about 20 minutes» — медиана ботов 16 минут, у людей дольше.

**Как вести «Coming next»:**
- Обновлять с каждым девлогом, не реже раза в 3–4 недели, и менять дату «As of».
- Сделанное переносить в Done, а не удалять.
- Дат у отдельных пунктов не ставить, обещаний, зависящих от продаж, не давать.
- Когда появятся сохранения, писать в каждом девлоге, переносятся ли они в новую сборку.

## Вариант 3. Тролль и дневник

Кому: тем, кто приходит за характером и следит за разработкой. Одна колонка как дневник, все GIF в тексте, дорожная карта, вопросы игрокам и русский блок внизу.

| Тема | |
|---|---|
| Layout | одна колонка, Screenshots: Hidden (всё в описании) |
| BG / BG2 | `#18493A` (цвет тролля) / `#FFFDF8` |
| Text / Link / Buttons / Headers | `#1E2A40` / `#1D5946` / `#F6C343` / `#236B52` |
| Шрифты | заголовки Lilita One, текст Nunito |
| Баннер | `images/header-troll.jpg` + логотип + реплика «Ore again?» |
| Обложка | `images/cover-troll.jpg` + логотип |

**Short description:** Your trolls dig by day and fight in the arena. Lose one, lose a worker.

**Теги:** Auto Battler, City Builder, Management, Economy, Cozy, Fantasy, Low-poly, troll, colony-sim, base-building.

**Текст:**

> **A colony sim about the people who do the carrying.** Goblins and trolls mine, haul and forge on a floating island, and the same crew goes to the arena when the gold runs low.
>
> ## Meet the crew
> *[goblin]* **Goblin.** Cheap, quick, always in a hurry. Hauls ore to the market, and in the arena throws stones from three hexes away.
>
> *[troll]* **Troll.** Strong and slow. Digs harder, carries more, and in the arena stands in front so the goblins don't have to.
>
> A worker at the arena is a worker away from the mine. A fighter who falls is gone for good, along with the sword you forged for them.
>
> *[GIF: a troll carries ore to the market]*
>
> ## What's in this alpha
> - The tutorial island: nine quests, from your first goblin to an armoured squad. About 20 minutes.
> - The mine, the market, the barracks, the armory and the first arena fights.
> - 16 languages, including the original Russian.
> - No saves yet: close the tab and the colony is gone.
>
> ## Coming next
> - **Done:** the tutorial and the first arena fights
> - **In work:** saves, an autosave and four slots
> - **Next:** the rest of the campaign, 34 quests from the smelter to the enchanter
> - **Next:** new arenas: forest, swamp, graveyard and snow
> - **Later:** a demo on Steam
>
> *[GIF: an armoured squad on the hexes]*
>
> ## Tell me
> Three things I'd love to read in the comments:
> 1. Where did you get stuck?
> 2. What did you give the trolls to do?
> 3. Did the first fight feel fair?
>
> If something broke, press F8 and send the logs. They reach me with your build number.
>
> ## Follow along
> Follow the game here for devlogs. Discord · Telegram · YouTube. The Steam page is coming: the devlog will tell you when it opens.
>
> ## Controls
> Left click selects, drag selects a group, right click gives orders. WASD or arrows move the camera, the wheel zooms. Every button shows its key.
>
> ## Small print
> Made in Unity. Creatures: Minifantasy by Krishna Palacio. Music: YannZ (CC BY 4.0).
>
> ## По-русски
> Колония троллей и гоблинов на парящем острове. Гоблины добывают руду, тролли носят её на рынок, а когда золота мало, те же работники надевают броню и идут на арену.
>
> В альфе — обучение: девять заданий, около 20 минут. Сохранений пока нет. Если что-то сломалось, нажмите F8 → «Отправить логи» или напишите в комментариях. Игра написана на русском.

Пункты «Coming next» взяты из `docs/GDD.md` (34 задания, биомы арены §6.4) и решения о сохранениях от 2026-10-09; обновлять с каждым девлогом.

## Что сделать до публикации

1. Логотип «Trollstead» в векторе, затем баннер и обложку выбранного варианта с ним.
2. Три-пять GIF по 3–6 секунд в английском интерфейсе (подписи у плейсхолдеров на макетах). Все кадры с HUD сейчас на русском — переснять на английском.
3. Трейлер 30–60 секунд на YouTube, геймплей с первых секунд.
4. Фон окна игры до запуска (Embed: background image) — кадр острова с затемнением.
5. Первый девлог в день публикации и пост в Release Announcements.
