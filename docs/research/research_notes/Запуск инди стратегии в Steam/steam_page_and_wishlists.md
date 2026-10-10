# Steam "Coming Soon" page best practices and wishlist benchmarks for a small indie strategy game (Troll Strategy)

Research date: 2026-10-05. The notes label every source by date. "Older" means pre-2024. Each claim is tagged **[Data]**, **[Rule]** (a Valve rule or spec), or **[Opinion]** (a rule of thumb from a practitioner). Lower-quality SEO blogs (steampageanalyzer.com, presskit.gg field guides, cinevva, tech-insider) appear only where no primary source turned up, and they are flagged when used.

## 1. Capsule art: effect on CTR, current asset sizes, what makes a capsule work, cost

### Takeaway
As of 2026 the required store capsules are the "2x" sizes Valve introduced in 2024: header 920x430, small 462x174, main 1232x706, vertical 748x896. Library assets are 600x900 capsule, 920x430 header, 3840x1240 hero and a 1280-wide logo. The only text allowed on them is the title. I found no rigorous public CTR data for capsules, only case studies and opinion. Practitioners agree it is the one marketing item worth paying for, at $250 to several thousand USD.

### Cited Findings
**Current store asset sizes and rules (Steamworks docs, fetched 2026-10-05)**
- [Rule] Header capsule 920x430 (top of store page, "Recommended for you", Big Picture). Small capsule 462x174, from which Steam auto-generates 120x45 and 184x69 (search results, top sellers, new releases). Main capsule 1232x706 (front-page carousel). Vertical capsule 748x896 (front page during seasonal sales, sale pages). Page background 1438x810 is optional and auto-generated from the last screenshot if missing. — [Steamworks: Standard store assets](https://partner.steamgames.com/doc/store/assets/standard)
- [Rule] Capsule text: "Do not include quotes or other strings of text beyond the title." The logotype "should be easily legible", and on the small capsule the "logo should nearly fill the small capsule" and stay readable "even at smallest size". — [Steamworks: Standard store assets](https://partner.steamgames.com/doc/store/assets/standard)
- [Rule] Screenshots: at least 5, minimum 1920x1080 (16:9), "show gameplay exclusively" (no concept art, pre-rendered cinematic stills or marketing copy). Mark at least 4 as "suitable for all ages" for maximum visibility. — [Steamworks: Standard store assets](https://partner.steamgames.com/doc/store/assets/standard)
- [Rule] Library assets: Library capsule 600x900 (half size 300x450). Library header 920x430. Library hero 3840x1240 (half 1920x620) with an 860x380 "safe area". Library logo 1280 px wide and/or 720 px tall on a transparent background. Same rule against extra text. — [Steamworks: Library assets](https://partner.steamgames.com/doc/store/assets/libraryassets)
- [Rule] Secondary sources say Steam started accepting these larger (2x) sizes in August 2024. Time-limited text (updates, events) is allowed only through "Artwork Overrides", which expire automatically. Award logos, review quotes and discount text are not allowed on base capsules. — [presskit.gg capsule guide (secondary)](https://presskit.gg/field-guides/steam-capsule-art-guide); the size change matches the current [Steamworks asset page](https://partner.steamgames.com/doc/store/assets/standard)
- [Data] The June 4, 2026 Steam store home refresh added higher-resolution, wider art. In some rows, hovering a tile now silently autoplays the game's trailer: "Mousing over certain tiles silently autoplays the game's trailer". — [PCGamesN, June 2026](https://www.pcgamesn.com/steam/store-refresh); [Massively OP, 2026-06-05](https://massivelyop.com/2026/06/05/steam-puts-out-a-refreshed-store-home-page-with-more-personalization-and-features/)

**Capsule impact / CTR**
- [Data, older case] Imagine Earth, a space colony/strategy game, replaced an amateur-looking capsule in June 2015, a year after its Early Access launch. Daily sales went from 0–3 to 40–60 copies, roughly 20x. No impression or CTR numbers were published. Zukowski's lesson: show genre markers (planet, glowing text, spacecraft), hire a capsule specialist, and budget $500–1,000. — [howtomarketagame, 2020-04-13](https://howtomarketagame.com/2020/04/13/how-one-new-image-increased-sales-by-20x/)
- [Opinion] "The capsule will be seen by more people than any other marketing object you create." A poor capsule undercuts every other marketing investment. — [howtomarketagame, 2022-03-08](https://howtomarketagame.com/2022/03/08/what-should-you-spend-money-on-if-you-have-a-small-marketing-budget/)
- [Opinion, low-quality source] Typical capsule CTR is claimed at 2–4%, with 7%+ "excellent" and most indies under 2%. No methodology is given. — [steampageanalyzer CTR benchmarks (SEO blog, unverified)](https://www.steampageanalyzer.com/blog/steam-ctr-benchmarks)

**What makes a capsule work**
- [Opinion] The logo must read at 120x45 px, where thin strokes and fine serifs disappear. The art has to signal the genre in about one second of scrolling. — [presskit.gg capsule guide (secondary)](https://presskit.gg/field-guides/steam-capsule-art-guide), consistent with Valve's small-capsule rule above.
- [Opinion, older] Zukowski's 2019 user research (n=7): shoppers want reassurance that a game looks like the games they already like ("preach to the choir"), so genre familiarity beats uniqueness. — [Game Developer / Zukowski, 2019-09-06](https://gamedeveloper.com/business/how-steam-users-see-your-game)

**Cost**
- [Opinion] "Expect to pay $250 minimum all the way up to multiple thousands for top of the line artists." Find artists on ArtStation or DeviantArt by searching "cover art" / "cover illustration". — [howtomarketagame, 2022-03-08](https://howtomarketagame.com/2022/03/08/what-should-you-spend-money-on-if-you-have-a-small-marketing-budget/)
- [Opinion, older] $500–1,000 recommended for a professional capsule. — [howtomarketagame, 2020](https://howtomarketagame.com/2020/04/13/how-one-new-image-increased-sales-by-20x/)
- [Data, marketplace] Fiverr listings for "Steam capsule art" run from about $10 to $500. — search summary of Fiverr listings (e.g. [Fiverr capsule gig](https://fiverr.com/steam_capsule/create-professional-steam-capsule-art-for-your-game)). Quality at the low end is unverified.

### Inferences
- Troll Strategy needs one key art piece that can be cropped and re-composed into 920x430, 462x174, 1232x706, 748x896, 600x900 (portrait) and 3840x1240 (hero, no logo) plus a separate transparent logo. Commissioning "one illustration + all crops + logo" as a single package is the usual way to keep the cost near the low end of the $250–$1,000+ range.
- For a logistics/strategy game, the genre signal on the capsule should be the thing that sets it apart: a troll hauling a cart of ore between buildings, a settlement seen at a 3/4 strategy angle. A lone character portrait would read as an action RPG.
- The June 2026 homepage now autoplays the trailer on hover in some rows. The capsule and the first seconds of the trailer now act as one impression, which raises the value of a gameplay-first trailer opening (see section 2).
- Capsule text cannot be localized beyond the title, but Steam allows per-language capsule uploads. The ~15 localized languages may justify a localized logo for zh/ja/ko if the title is translated (not verified here; see Gaps).

### Gaps
- No primary, methodologically sound CTR dataset for capsules was found. Valve publishes none; the only numbers are case studies and SEO blogs.
- The exact Valve announcement date for the 2x asset sizes (reported as August 2024) was not fetched from a primary Valve post.
- Whether Steamworks shows per-capsule impressions/CTR to developers (the "Traffic"/"Store page" report) was not verified in this pass.
- Current (2025–2026) capsule-artist price surveys were not found. Price ranges come from Zukowski's 2020–2022 posts and marketplace listings.

## 2. Screenshots, trailer, and GIF/video in "About this game"

### Takeaway
Valve's own guidance is gameplay first, quickly. Shoppers may give a trailer "less than 10 seconds", so it must work without sound, and logos and story belong later. Only the first two trailers show before the screenshots. The first ~4 screenshots carry the hover preview. Since August 2025 the description area is 780 px wide and accepts MP4/WebM clips instead of GIFs, with a per-image limit of 5 MB and a recommended total under 15 MB.

### Cited Findings
**Trailer (Valve)**
- [Rule/Opinion from Valve] The first trailer should be "primarily gameplay, showing the player what they will be doing in the game". Customers may spend "less than 10 seconds" on trailers in discovery contexts. Trailers should be designed so players "can learn about it without audio". — [Steamworks: Trailers](https://partner.steamgames.com/doc/store/trailer)
- [Rule] "The first two valid trailers will be shown to players before any screenshots"; the rest come after the screenshots. Categories: General/Cinematic, Teaser, Gameplay, Interview/Dev Diary. Specs: up to 1920x1080, 30 or 60 fps, 5,000+ Kbps, .mov/.wmv/.mp4, 16:9 preferred, H.264 + AAC. — [Steamworks: Trailers](https://partner.steamgames.com/doc/store/trailer)
- [Rule] Steam auto-generates 6-second looping "microtrailers" by "taking six 1-second clips from various points in the video". They are used in store hubs and sale pages and cannot be customized. — [Steamworks: Trailers](https://partner.steamgames.com/doc/store/trailer)
- [Rule/Opinion from Valve, 2023] The May 2023 change limited pre-screenshot trailers to two. Valve advised "give players a good look at the gameplay of your game in as short a time as possible" and to save "company logos or narrative storylines for further into your trailer once you've gotten the interest of a player". — [PCGamesInsider, 2023-05-04](https://www.pcgamesinsider.biz/news/73788/valve-tweaks-steam-trailer-guidelines/)
- [Data, 2026] On the refreshed home page (June 2026), hovering certain tiles "silently autoplays the game's trailer". — [PCGamesN](https://www.pcgamesn.com/steam/store-refresh)
- [Opinion] Zukowski's pre-page-launch checklist: the trailer must show in-game footage from the opening seconds. — [GameWorldObserver summarizing Zukowski, 2025-03-11](https://gameworldobserver.com/2025/03/11/steam-page-launch-guide-wishlists-zukowski)

**Screenshots**
- [Data, older, small sample] Zukowski's user study (2019, n=7 experienced Steam users): on hover, participants looked at the 4 rotating screenshots in about 5.5 seconds. Most skipped the trailer; one watched 13 seconds. Those who watched scrubbed for gameplay. Recommendation: the first 4 screenshots should "communicate how you play the game", showing the genre's typical UI and camera. — [Game Developer / Zukowski, 2019](https://gamedeveloper.com/business/how-steam-users-see-your-game)
- [Rule] At least 5 screenshots, gameplay only, 1920x1080 minimum; at least 4 marked all-ages. — [Steamworks: Standard store assets](https://partner.steamgames.com/doc/store/assets/standard)

**GIF/video in About This Game**
- [Rule] Description images must be "smaller than 5MB" each. Valve advises keeping "the total file size of all your screenshots and GIFs below 15MB". "Any text included in images will also need to be translated and re-uploaded when you localize your store page." No external links or QR codes, no fake Steam UI, no ads for other games. — [Steamworks: Description](https://partner.steamgames.com/doc/store/page/description)
- [Data] August 2025 store page update: pages widened from 940 px to 1,200 px and became responsive. The description column now defaults to 780 px. Developers "can now put up small videos inside their game descriptions" as MP4/WebM, with play/pause controls. Valve converted 292,000 existing GIFs to video. — [GamingOnLinux, Aug 2025](https://www.gamingonlinux.com/2025/08/steam-is-getting-wider-and-more-responsive-store-pages/)
- [Opinion, secondary] Older guidance put description banners at 616 px wide and said GIFs should be a few seconds long and loop cleanly. That width predates the 780 px layout. — [presskit.gg GIF guide (secondary)](https://presskit.gg/field-guides/game-marketing-gifs-guide)

### Inferences
- Trailer for Troll Strategy: open in second 0 on a readable shot of the core loop, such as ore carried from mine to smelter to forge to market with numbers ticking. Move to a squad being equipped and positioned, then the auto-battle, then a loss hitting the economy. The title card goes at the end. Keep it to about 60–90 s (an industry convention, not a Valve rule; see Gaps). Make every on-screen beat readable when muted, since home-page hover autoplay is silent.
- Because microtrailers are six random 1-second clips, a trailer with title cards, black frames or slow pans risks clips that show nothing. A dense gameplay trailer is safer.
- Screenshot order: (1) the full logistics chain on one screen, (2) a production-chain close-up with UI, (3) squad equip/position screen, (4) an auto-battle mid-fight. Then the settlement at scale, the goblin/troll hiring UI, and the world map or mission. Show real UI; the 2019 study found buyers check the genre's typical UI and camera.
- Description: use 780 px-wide MP4/WebM loops, one per feature (logistics, chains, hiring, battle, consequence). Any text inside a clip would need a version for each of the ~15 languages, so captions belong in the HTML text, not burned into the media.

### Gaps
- Valve gives no recommended trailer length. "60–90 s" and "no logo in the first 5 s" are practitioner conventions; I found no 2024–2026 dataset on them.
- File-size and length limits for the new MP4/WebM description clips were not found in Steamworks docs. The fetched description page still mentions only images/GIFs (5 MB each, 15 MB total).
- Exactly which homepage rows autoplay trailers on hover, and which trailer they use (the first one?), is not documented in what I fetched.

## 3. Tags: how Steam uses them and how to pick them

### Takeaway
Steam uses up to 20 tags. Weight and order matter for tag pages and some filters (the first 15). "More like this" works from overlap among the top 20, where order matters less. The top 5 should define the genre precisely. Generic tags like "Indie", "Action" and "Strategy" add little discovery value at the top.

### Cited Findings
- [Rule] At least 5 tags are required before launch and up to 20 are recommended. "The top 20 tags on your game determine the tag pages and similar browse views." "Some store filters prioritize the first 15 tags." — [Steamworks: Tags](https://partner.steamgames.com/doc/store/tags)
- [Rule] "The tags given the most weight govern your visibility more than those given less"; sort them "in order of relevance". "Your title's top 5 tags should paint a fairly clear picture of your game." — [Steamworks: Tags](https://partner.steamgames.com/doc/store/tags)
- [Rule] For "More Like This" and similar-games lists, Steam uses "the top 20 tags applied to your game", where "the order is less important than the overlap between multiple tags". Generic tags like "Action" matter "very little… because so many other games have 'Action' applied". — [Steamworks: Tags](https://partner.steamgames.com/doc/store/tags)
- [Rule] Players with non-limited accounts and Steam moderators can also apply tags, and community tagging raises a tag's weight. The Tag Wizard (Store Presence > Edit Store Page > Basic Info) has a "Suggest Prioritization" feature. — [Steamworks: Tags](https://partner.steamgames.com/doc/store/tags)
- [Opinion, older] Shoppers scan tags for genre confirmation and for "poison pills" (tags that put them off). Fill the top 5 with clear genre tags like "City Builder", not "Indie". — [Zukowski 2019 user study](https://gamedeveloper.com/business/how-steam-users-see-your-game); see also [howtomarketagame "Steam 101: how to tag your game", 2020](https://howtomarketagame.com/2020/11/12/steam-101-how-to-tag-your-game/)
- [Opinion, secondary] Copy the shared top tags of 5–10 successful games in your niche and put the most specific ones first. — [presskit.gg tags guide (secondary)](https://presskit.gg/field-guides/steam-tags-strategy)

### Inferences
- Candidate top-5 order for Troll Strategy, to check in the Tag Wizard against comparable titles: **Automation**, **Colony Sim** or **City Builder**, **Base Building**, **Resource Management**, **Auto Battler**. "Strategy", "Management", "Simulation", "RTS"-type tags, "Economy", "Fantasy", "3D", "Singleplayer", "Indie" and "Relaxing"/"Cute" (if they fit the tone) go into slots 6–20.
- "Auto Battler" in the top 5 links the game to a large audience whose expectations differ (PvP, roguelite drafting). That makes it a possible poison pill for colony/automation buyers and a mismatch for auto-battler fans. Keep it in the top 10–15 rather than the top 3 unless playtests show the battle is the main hook.
- Check that "Logistics" exists as a Steam tag before planning around it; it was not confirmed here. "Transportation" and "Resource Management" are the likely nearest tags.
- Since "More like this" depends on overlap, pick tags that match the specific games Troll Strategy should sit next to (for example small automation/colony sims with combat). Checking those games' tags on SteamDB is a concrete next step.

### Gaps
- Valve does not publish the exact weighting formula, and no 2024–2026 data quantifies how much the top-5 order changes impressions.
- Which tags the comparable games actually use (SteamDB lookup) was not done in this pass.

## 4. Short description, hook, genre clarity

### Takeaway
The short description is plain text of a few hundred characters (300 per secondary sources). Valve asks for the game's unique value and genre and no feature lists, dates or formatting. Practitioner research says shoppers skim for genre words and verbs.

### Cited Findings
- [Rule] The short description is "limited to a few hundred characters". Focus on the unique value proposition and genre. Avoid feature lists, narrative detail, time-based text ("Now Available", dates, "Prepurchase now"), formatting and line breaks. — [Steamworks: Description](https://partner.steamgames.com/doc/store/page/description)
- [Rule, secondary] The field is capped at 300 characters and won't save until the text fits. — [presskit.gg description guide (secondary)](https://presskit.gg/field-guides/steam-description-guide). CJK exports can overflow the cap, per [gamineai help note](https://gamineai.com/help/steam-short-description-300-char-limit-weblate-cjk-export-fix) (low-quality source).
- [Data, older, n=7] Participants spent little time on the short description and many skipped it. They looked for "verbs" that show what you do. Recommendation: lead with gameplay verbs and genre signals, not atmospheric prose. — [Zukowski 2019](https://gamedeveloper.com/business/how-steam-users-see-your-game)
- [Opinion, secondary] Formula: hook + genre + unique mechanic. Example: Balatro's "The poker roguelike…" — [presskit.gg description guide (secondary)](https://presskit.gg/field-guides/steam-description-guide); [Game Devs Journey: 4 types of short descriptions](https://gamedevsjourney.substack.com/p/4-types-of-steam-short-descriptions)

### Inferences
- A draft shape (not tested): "Build a troll settlement where every ore is carried by hand: mine → smelter → forge → market. Hire goblins and trolls, equip and place your squad, then watch the auto-battle — every loss costs you workers and goods." This puts the genre and the hook (visible logistics, losses hit the economy) in the first clause.
- Because the Russian text is the translation key in this project's pipeline, a 300-character Russian original can exceed the limit in German or Portuguese. Check every language against the cap before submission.

### Gaps
- No 2024–2026 dataset on how short-description wording affects wishlist conversion was found.

## 5. Store page localization (Simplified Chinese share) and its effect on wishlists

### Takeaway
In the September 2026 Steam survey Simplified Chinese was 31.4% of users and English 34.2%. The survey swings by several points month to month. The only before/after data on page localization is older (2021, Hooded Horse): about +50% country wishlists for FR/DE, about +100% for ES, and more than 90% of East Asian post-localization wishlists attributed to the localization.

### Cited Findings
- [Data] Steam Hardware & Software Survey, September 2026, language share: English 34.20% (−4.05), Simplified Chinese 31.39% (+7.42), Russian 9.05%, Spanish (Spain) 4.08%, Portuguese (Brazil) 3.70%, German 2.48%, Japanese 2.41%, French 2.08%, Polish 1.49%, Korean 1.37%, Traditional Chinese 1.27%, Turkish 1.09%. — [Steam HW&SW Survey](https://store.steampowered.com/hwsurvey/Steam-Hardware-Software-Survey-Welcome-to-Steam)
- [Data, secondary] Valve said at GDC 2025 that Simplified Chinese passed English as Steam's most-used language (33.7% vs 33.5%). — reported in search summaries (for example [Alconost 2026 guide](https://alconost.com/en/blog/steam-language-mix-indies)); primary GDC slide not verified.
- [Data, older 2021] Tim Bender (Hooded Horse) compared wishlists before and after localization: for French and German, "35–40% of post-announcement wishlists were because of localization", about +50% country wishlists. Spanish: "slightly over half", about +100%. East Asian languages: "over 90% were because of localization". — [GameDiscoverCo, 2021-06-23](https://newsletter.gamediscover.co/p/game-localization-for-discovery-its); [WN Hub, 2021-06-25](https://wnhub.io/news/stores-and-publishing/item-18823)
- [Data, older 2021] Chris Wright (Fellow Traveller): Chinese localization plus local PR moved China's share of units from "5% or less" to "20% or more". His ROI ranking: Simplified Chinese, Japanese (if on Switch), Traditional Chinese, Russian, Brazilian Portuguese. — [GameDiscoverCo, 2021](https://newsletter.gamediscover.co/p/game-localization-for-discovery-its)
- [Rule] Text inside description images must be translated and re-uploaded per language. — [Steamworks: Description](https://partner.steamgames.com/doc/store/page/description)
- [Data, unverified] "Pages localized beyond English earned a median of 62 wishlists/week vs 18 for English-only (3.4x)." This appeared in a search summary with no traceable primary source. — treat as unverified (see Gaps).

### Inferences
- Troll Strategy already ships ~15 languages including zh/ja/ko/de/pt/es/ru. Localizing the store page (short and long descriptions, capsule logo if the title is translated, captions) costs little compared with the game localization and reaches more than 60% of Steam users through English + Simplified Chinese alone.
- Hooded Horse's numbers come from a strategy-game publisher, so they apply well to the genre, but they are 2021 data and reflect page localization plus an announcement. The size of any 2026 effect is unknown.
- Russian (9%) is the third-largest language and matches the developer's home audience (DTF, Pikabu, Telegram). Russian-language posts can drive both Russian wishlists and UTM-tracked traffic.

### Gaps
- No 2024–2026 controlled data on wishlist lift from store page localization alone.
- The GDC 2025 Valve statistic could not be checked against the primary talk.
- No source on Chinese players' affinity for automation/colony/strategy versus other genres was found in this pass.

## 6. When to open the Coming Soon page: Steam Direct steps, fees, waits, review times

### Takeaway
Pay the $100 Steam Direct fee (recouped after $1,000 adjusted gross revenue). Release cannot happen until 30 days after payment, and the Coming Soon page must be public for at least 2 weeks. Store page review takes 3–5 business days; submit at least 7 business days ahead. Valve and Zukowski both say there is little downside to an early page once the genre and art direction are settled. Changing the genre or tone later is the real risk.

### Cited Findings
- [Rule] Fee: "$100 USD (or equivalent)… for each new app", recoupable "after your product has at least $1,000.00 Adjusted Gross Revenue". — [Steamworks: App fee](https://partner.steamgames.com/doc/gettingstarted/appfee)
- [Rule] "A 30-day waiting period between when you paid the app fee and when you can" launch. Paperwork: bank, tax and identity verification. A "publicly-visible 'coming soon' page for at least two weeks". Build review "takes between 1–5 days". — [Steam Direct](https://partner.steamgames.com/steamdirect)
- [Rule] "For new products, you must have a Coming Soon page up for at least two weeks before releasing." — [Steamworks: Coming Soon](https://partner.steamgames.com/doc/store/coming_soon)
- [Rule] Store page review "typically takes 3–5 business days… submit your page for review at least 7 business days before you want it live". Build review also "typically 3–5 business days", plan for at least 7. — [Steamworks: Review process](https://partner.steamgames.com/doc/store/review_process)
- [Opinion from Valve] Publish "as soon as you are ready to start talking publicly about your game", once art direction and core features are settled. "There doesn't appear to be a strong downside to having a store page up for a long time ahead of release." — [Steamworks: Coming Soon](https://partner.steamgames.com/doc/store/coming_soon)
- [Opinion] Zukowski: launch the page as soon as possible so any viral moment has a wishlist target. "Wishlists really don't get old. They aren't bread." Early wishlists fail to convert only if the game "changes drastically genres or style". Before launching the page: one genre, final art style, at least 3 distinct environments, a professional capsule, and a trailer with in-game footage from the first seconds. — [GameWorldObserver summarizing Zukowski, 2025-03-11](https://gameworldobserver.com/2025/03/11/steam-page-launch-guide-wishlists-zukowski)
- [Data] Video Game Insights (July 2025): most successful projects open their Steam pages 6–12 months before release, and "successful projects generally collect more than 70% of their wishlists in the four months leading up to launch". — [GameDev Reports summary of VGI, 2025](https://gamedevreports.substack.com/p/video-game-insights-steam-wishlists)
- [Rule] Seasonal sale eligibility: "any game released at least 30 days before the event start date". Upcoming: Winter 2026 Dec 17 – Jan 4, 2027; Spring 2027 Mar 18–25; Summer 2027 Jun 24 – Jul 8. — [Steamworks: Upcoming events](https://partner.steamgames.com/doc/marketing/upcoming_events)

### Inferences
- The 30-day wait counts to *release*, not to the Coming Soon page, so the two clocks can run in parallel (inferred from the "between when you paid… and when you can launch" wording). For Troll Strategy the binding constraint is the store page review (about 7 business days) plus having the assets ready.
- "Too early" here means before the capsule, a trailer with real gameplay, and at least 5 representative screenshots exist. Missing missions 2–7 and saves do not block a Coming Soon page as long as the genre and art direction are final.
- Next Fest Feb 22 – Mar 1, 2027 requires a public store page and a reviewed demo (section 7). Opening the page in Q4 2026 leaves 3–5 months to collect pre-fest wishlists.

### Gaps
- The Steam Direct page says build review takes 1–5 days, while the review-process doc says 3–5 business days and to plan for 7. Use the more conservative figure.
- No source confirms whether the Coming Soon page can be published before the 30-day period ends. This is inferred from wording and common practice.

## 7. Wishlist benchmarks: conversion, visibility thresholds, medians, velocity, deletions, followers, Next Fest, UTM

### Takeaway
The median first-week sales-to-wishlist ratio is about 0.15x for games with 25k+ wishlists (0.10x above $10). Results vary 10–20x between games, and data below 25k wishlists is thin. 66% of games have under 10k wishlists. The old "7k → Popular Upcoming" rule looks obsolete after the June 2026 store refresh narrowed Popular Upcoming to the most anticipated releases of the month; the oft-quoted ~80k new threshold is unverified. A small game going into Next Fest should aim for 2k+ wishlists. The Feb 2026 median Next Fest gain was 806 among survey respondents. Steam UTM tracking credits wishlists within 72 hours of a click and shows results per channel.

### Cited Findings
**Conversion (wishlists → sales)**
- [Data] GameDiscoverCo (2025-10-17): median wishlist-to-week-1-sales conversion is 0.15x for games with more than 25,000 wishlists at launch (Sep 2024 – Sep 2025). Under $10: 0.15x; over $10: 0.10x. Results vary "by 10–20x, not 10–20%". An earlier anonymous poll ranged from 0.017x to 1.7x. Games converting at about 0.07x had a median 7-day review score of 67% (Mixed), against 91% for top converters. "We don't think they are slipping": conversion is not falling, but wishlists are harder to get. — [GameDiscoverCo](https://newsletter.gamediscover.co/p/the-state-of-steam-wishlist-conversions); summary [GameDev Reports](https://gamedevreports.substack.com/p/gamediscoverco-the-state-of-steam)
- [Data] VGI (July 2025): "66% of games have fewer than 10,000 wishlists". "Only 6% of projects in the last 12 months reached over 100,000 wishlists". Only 9% (141 of 1,500) sold more copies than their launch wishlist count. Wishlists vs first-month sales correlation is about 0.7 (R² ≈ 0.49). Strategy is among the genres with higher median wishlists. — [GameDev Reports summary of VGI](https://gamedevreports.substack.com/p/video-game-insights-steam-wishlists)
- [Data, 2023 releases] GameDiscoverCo long tail: median first-year to first-week revenue ratio 2.64x (2.69x for games selling 1,000+ in week 1; 3.6x for 10k–50k week-1 sellers). — [GameDev Reports summary of GameDiscoverCo](https://gamedevreports.substack.com/p/gamediscoverco-games-long-tail-revenue)
- [Opinion/Data, 2024] Zukowski (2024-01-29): observed conversions range from 1% to 40%. Recommends tracking wishlists per year: about 15,000/year suggests "good chance at being hot", under 4,000/year suggests soft interest. After launch, Steam's visibility depends on revenue, not wishlists. — [howtomarketagame](https://howtomarketagame.com/2024/01/29/do-wishlists-matter-any-more/)
- [Data, secondary, unverified] Conversion by wishlist band (<5k ≈ 15%, 5k–40k ≈ 20%, 40k–100k ≈ 23%, 100k+ ≈ 25%), attributed to Simon Carless and dev surveys with no period stated. — [presskit.gg (secondary)](https://presskit.gg/field-guides/how-many-wishlists-to-launch). This conflicts with GameDiscoverCo's 0.10–0.15x first-week medians and may describe a longer window; treat with caution.

**Visibility thresholds ("Popular Upcoming", 7k/10k)**
- [Opinion, pre-2026] Zukowski (Jan 2024): aim for 7,000–10,000 wishlists before launch for Popular Upcoming. Being featured there brought about 750–2,000 wishlists per day. — [howtomarketagame, 2024-01-29](https://howtomarketagame.com/2024/01/29/do-wishlists-matter-any-more/)
- [Data, Valve statement] Valve's Erik Peterson: "I'm here to tell you that that's just simply not the case" that a set number of wishlists is needed for the front page. — quoted in [howtomarketagame, 2024-01-29](https://howtomarketagame.com/2024/01/29/do-wishlists-matter-any-more/)
- [Data, 2026] Since the June 4, 2026 refresh, "Popular Upcoming" has been updated "to better capture the most anticipated releases of the coming month". Niche upcoming games are pushed to the new personalized calendar, which shows recommended upcoming and recent games over about two weeks, based on play history. — [PCGamesN](https://www.pcgamesn.com/steam/store-refresh); [Massively OP](https://massivelyop.com/2026/06/05/steam-puts-out-a-refreshed-store-home-page-with-more-personalization-and-features/)
- [Unverified claim] "Effective" Popular Upcoming threshold rose "from roughly 7,000 to around 80,000" wishlists. The only source is tech-insider citing Inkl/Creative Bloq; no methodology was given and the Inkl page returned 403. — [tech-insider (low-quality)](https://tech-insider.org/steam-store-redesign-2026/); [Creative Bloq](https://www.creativebloq.com/3d/video-game-design/is-steams-more-modern-redesign-good-or-bad-for-indie-game-developers)

**Followers and deletions**
- [Data, 2023, older] GameDiscoverCo (2023-06-21): unreleased games' wishlists median 12x followers (average 12.37x, typical range 7–20x), up from 9.6x in Feb 2021. Strategy-type games have lower multipliers (4X 7.5x, turn-based strategy 9.0x, survival 9.5x) than story-rich (13.2x), relaxing (15.7x) and puzzle (15.9x). Lower ratios indicate more pre-release hype and better post-release revenue. — [GameDiscoverCo](https://newsletter.gamediscover.co/p/why-your-steam-follower-to-wishlist)
- [Data, unverified] Median lifetime wishlist deletion rate 7.75% (average 9.8%). This came from a search summary and no primary source was found. — see Gaps.

**Next Fest**
- [Rule] Next Fest dates: Oct 19–26, 2026; **Feb 22 – Mar 1, 2027**; **Jun 14–21, 2027**. — [Steamworks: Upcoming events](https://partner.steamgames.com/doc/marketing/upcoming_events)
- [Rule] Eligibility: a game may join only ONE Next Fest, the base-game store page must be public, a playable demo must be live by the start, and the game cannot release before the fest ends. Demo review: submit 5 weeks before if releasing before the press preview, 3 weeks before at the latest. The demo must be live at least 30 minutes before the start. Livestreams are optional. — [Steamworks: Next Fest](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest)
- [Data] Zukowski's Feb 2026 Next Fest survey (post 2026-04-13): median 806 wishlists gained (30th percentile 382, 70th 1,839, 95th 13,461, max 57,074). Earlier medians: Feb 2025 1,079; Oct 2024 916; Jun 2024 884. Demos released more than 1 month before the fest "did much better". Of the 15k+ earners, 5 released demos months early, 4 one month before, and 1 days before. — [howtomarketagame, 2026-04-13](https://howtomarketagame.com/2026/04/13/making-sense-of-the-february-2026-steam-next-fest/)
- [Data] Zukowski's Feb 2025 Next Fest benchmarks (post 2025-03-26): median gain by pre-fest wishlists: 0–999 → 322; 1,000–9,999 → 1,006; 10,000–99,999 → 5,215; 100,000+ → 12,882. ">2000 wishlists… more possibility your game will really do well". Advice: "Release your demo way before Steam Next Fest". — [howtomarketagame, 2025-03-26](https://howtomarketagame.com/2025/03/26/benchmarks-how-many-wishlists-can-i-get-from-steam-next-fest/)
- [Data, secondary] Feb 2026 Next Fest had more than 3,500 demos (+51% vs Feb 2025). Pre-fest wishlists correlated with fest gains at Spearman r = 0.825. GameDiscoverCo estimated the median demo across all entrants at only about 200 wishlists; survey respondents self-select upward. — [cinevva guide (secondary; no primary links)](https://app.cinevva.com/guides/steam-next-fest-strategy)
- [Data, case] Parcel Simulator went from 7,000 to 42,000 wishlists around its demo. — [howtomarketagame, 2025-08-26](https://howtomarketagame.com/2025/08/26/)

**Steam UTM analytics**
- [Rule] UTM reports show Total Visits, Trusted Visits (bots/crawlers excluded) and Tracked Visits (logged-in users). Conversions: Wishlist, Purchase, Activation (F2P license or key redemption). A conversion counts if it happens "within 72 hours of clicking through your UTM link". Visits update hourly; conversions finalize "4 days after the visit". Parameters: utm_source, utm_medium, utm_campaign, utm_content, utm_term. Steam hides combinations below minimum thresholds, and some users go untracked because of browser/cookie settings. — [Steamworks: UTM analytics](https://partner.steamgames.com/doc/marketing/utm_analytics)

### Inferences
- Plan with realistic small-indie numbers: about 2/3 of games launch under 10k wishlists. At 0.10x (over $10) a 7k-wishlist launch would be about 700 week-1 units, and the 2.64x revenue long-tail median (2023 data) suggests first-year revenue near 2.6x week 1. These are planning ranges only; actual results vary 10–20x.
- After June 2026, Popular Upcoming is unlikely to be a lever for Troll Strategy at any plausible indie wishlist count. The levers left are the personalized calendar and recommendations (driven by tags and play-history overlap), Next Fest, and outside traffic. The 7–10k target still makes sense as a launch-readiness heuristic, but not as a visibility switch.
- Next Fest Feb 22 – Mar 1, 2027: the demo must be submitted for review by about Feb 1, 2027 (3 weeks prior). The data favors a public demo more than 1 month before the fest, so live by about Jan 22, 2027, and ideally the itch.io → Steam demo funnel earlier. The goal is 2,000+ wishlists going in. The registration deadline of January 10, 2027 given in the brief was not confirmed in Steamworks docs. A game can enter only one Next Fest, so entering in Feb 2027 with under 1k wishlists spends the one shot at the 0–999 band median (about 322). That trade-off favors June 14–21, 2027 if the page and demo cannot build momentum by January.
- UTM per channel: give each channel and post its own utm_source/utm_campaign (reddit, dtf, pikabu, telegram, itch, shorts, youtube). Avoid over-splitting with utm_content, because small combinations are hidden below Steam's thresholds. Compare wishlists per trusted visit by channel after the 4-day finalization. The 72-hour window misses people who come back later, so UTM numbers understate total channel impact.
- Followers are visible on SteamDB, so the follower multiplier works as a rough wishlist estimate for comparable strategy games. Strategy games skew toward lower multipliers (about 7.5–9x in 2023 data).

### Gaps
- No primary source found for wishlist deletion rates (the 7.75% median is unverified).
- The ~80k "Popular Upcoming" threshold after June 2026 is unverified, and no GameDiscoverCo or Zukowski primary analysis of the refresh's threshold effect was found. Search results also mentioned a Zukowski "launch benchmark" tiers update from June 2026 (5k/8k/50k/90k); it was not verified and presskit.gg showed different (revenue-based) tiers, so it should not be relied on.
- No reliable 2024–2026 conversion medians for games with under 25k wishlists, which is the band Troll Strategy will most likely be in.
- No data on typical wishlist velocity in the first months of a new Coming Soon page for a small indie with no audience.
- The Next Fest Feb 2027 registration deadline was not found in Steamworks docs.

## 8. How strategy / automation / colony sim / base building games perform on Steam

### Takeaway
The broad "management/simulation" space produces many hits, but specific subgenres have low hit rates under Zukowski's strict single-genre count. Automation was 1.19%, Colony Sim 1.0%, City Builder 0.5% and RTS 0.9% of 2025 releases reaching 1,000 reviews, against a 2.99% Steam average. Any-tag counts from a weaker source look far better (automation 8.4%) because of survivorship and tag accretion. Supply is exploding: automation-tagged releases went from 298 in 2024 to 450 in 2025 and 822 by September 2026. Strategy audiences show more pre-release "hype" per follower and favor deeper, higher-priced games.

### Cited Findings
- [Data] Zukowski's 2025 review (post 2026-01-27): 20,282 games released in 2025; 608 reached 1,000+ reviews ("hit", about $150k+ gross), 2.99%. Per genre (hits / released / rate): Automation 5/420/1.19%; Colony Sim 2/193/1.0%; City Builder 2/397/0.5%; RTS 4/442/0.9%; Management 19/549/3.4%; Simulation 43/1,048/4.1%; Tower Defense 9/511/1.76%; Idle/Incremental 27/965/2.79%; Open World Survival Craft 15/72/20.8%; Horror 39/1,208/3.2%. Method: "I try to assign the game 1 and only 1 genre" by manual review. — [howtomarketagame, 2026-01-27](https://howtomarketagame.com/2026/01/27/what-the-hell-happened-in-2025/)
- [Data, weaker source, different method] Steam's "Automation" tag (any position, community-applied), cohort Jan 2024 – Jun 2025: 8.4% reached 1,000 reviews vs 4.2% for all of Steam, with a median of 27 reviews vs 11. 450 automation-tagged releases in 2025 vs 298 in 2024, and 822 by 2026-09-27 (+170% on the same weeks of 2025). By price: <$5 1.2%, $5–9.99 5.9%, $10–14.99 5.3%, $20+ 52% reached 1,000 reviews. Neighboring niches: Base Building 22.4%, Auto Battler 11.4%, Colony Sim 8%, City Builder 4%. — [steampageanalyzer automation niche report, Sep 2026](https://www.steampageanalyzer.com/niches/automation). A sister page gives Colony Sim 13.7% (255 games, median 45 reviews) — [colony sim niche](https://www.steampageanalyzer.com/niches/colony-sim). **Caveat:** any-tag counting inflates success rates, because successful games collect more community tags. This conflicts with Zukowski's single-genre numbers.
- [Data, 2023] Follower-to-wishlist multipliers are lower (more "hype") for 4X (7.5x) and turn-based strategy (9.0x). These are "deeper, mechanically complex PC games where players expect regular updates". — [GameDiscoverCo, 2023-06-21](https://newsletter.gamediscover.co/p/why-your-steam-follower-to-wishlist)
- [Data] VGI 2025: strategy is among the genres with higher median wishlists. — [GameDev Reports summary of VGI](https://gamedevreports.substack.com/p/video-game-insights-steam-wishlists)
- [Opinion] GameDiscoverCo (2025-07-29) on the new Steam menu/browse: you still "need to have some momentum to get somewhere on Steam", but the changes "may amplify interest in specific genres & styles". — [GameDiscoverCo](https://newsletter.gamediscover.co/p/steams-new-store-interface-what-you)
- [Data, older case] Imagine Earth, a colony/strategy title, got about 20x daily sales from a genre-clear capsule. — [howtomarketagame, 2020](https://howtomarketagame.com/2020/04/13/how-one-new-image-increased-sales-by-20x/)

### Inferences
- Two readings fit the data. Strict-genre automation/colony games rarely become hits (about 1%). Games that *also* carry automation/base-building tags do better, probably because hits pick up those tags. For Troll Strategy, being a well-tagged hybrid (automation + base building + squad battles) is likely more useful than being "another colony sim".
- The price-band data (weak source) and the "deep PC game" profile suggest strategy/automation buyers accept $15–20+ for depth. That pushes conversion toward GameDiscoverCo's 0.10x (over $10) median and makes the 30–40-minute demo more important as proof of depth.
- Supply growth (automation releases up 170% year on year in 2026) means more competition in "More like this" lists. Visible logistics and losses that hit the economy are the differentiators to put in the capsule, the first screenshot and the first trailer seconds.

### Gaps
- No primary 2024–2026 data on wishlist-to-sales conversion by genre for strategy/automation specifically. GameDiscoverCo's 2025 report did not break this out in the fetched summary.
- Zukowski's 2025 table has no rows for Base Building, Strategy, Auto Battler or 4X.
- No direct data on audience expectations for "auto battler + economy" hybrids; the comparable-titles analysis has not been done.
