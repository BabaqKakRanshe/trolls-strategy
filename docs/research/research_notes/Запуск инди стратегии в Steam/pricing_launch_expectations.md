# Pricing, release timing, Early Access vs 1.0, launch mechanics and realistic outcomes for a small indie strategy game on Steam (as of 2026-10-05)

Context assumed throughout: Troll Strategy, a Windows 3D settlement/logistics strategy with production chains and auto-battles, solo/small Russian-speaking developer, no existing audience, mission 1 of 7 playable, no saves yet. Candidate Next Fests: Feb 22–Mar 1 2027 or Jun 14–21 2027.

Source-quality legend used below: **[Valve]** = Steamworks primary documentation; **[Data]** = dataset-based analysis (GameDiscoverCo, Gamalytic, VG Insights/Sensor Tower, Zukowski's datasets); **[Practitioner]** = consultant/developer opinion or single-game anecdote (survivorship bias likely); **[Aggregator]** = secondary blog/news summary, not verified against the primary.

---

## 1. Price: typical price points, price vs content, launch discount, Valve discount rules, regional pricing

### Takeaway
The working band for a polished single-player indie strategy/management game is $14.99–$19.99 (top-seller median launch prices fell to ~$15.6 by unit sales and ~$20.4 by revenue in Oct 2025, driven by cheap viral co-op hits, not by sims/strategy); Valve's hard rules are: launch discount ≤40% for 7–14 days, no other discount for 30 days after release or after any price increase, 30 days between discounts (seasonal sales exempt), and any discount ≥20% emails wishlisters.

### Cited Findings
**Market price levels [Data]**
- Median launch price of the top-50 new Steam releases by units fell from $19.50 (Feb 2023) to $15.64 (Oct 2025), −20%; top-50 by revenue fell from $23.70 to $20.35, −14%. Average prices barely moved ($21.80→$21.41 by units; $26.20→$25.76 by revenue). The drop is attributed to "the growing number of popular low-priced projects in the $5–15 range"; in Sept 2025 there were no top performers in the $25–55 band; the $25–60 "AA" segment is squeezed — [GameDiscoverCo via GameDev Reports](https://gamedevreports.substack.com/p/gamediscoverco-people-are-spending)
- Older (Oct 2023) Gamalytic data: 77% of all Steam games are priced under $10; only 5% above $20. Median revenue of recent paid releases was $700, rising to $4k when games under $5 are excluded and $17k when games under $10 are excluded (correlation, not proof that price causes revenue) — [Gamalytic via GameDev Reports](https://gamedevreports.substack.com/p/gamalytic-67-of-games-on-steam-earned)
- Price lowers unit conversion of wishlists: median first-week wishlist conversion 0.15× overall vs 0.10× for games priced above $10 (2024–25 sample of games with 25k+ wishlists) — [GameDiscoverCo via GameDev Reports](https://gamedevreports.substack.com/p/gamediscoverco-the-state-of-steam)
- Guidance that $14.99–$19.99 "hits the best balance" for polished indie titles and that sims/single-player games with good production values "continue to perform at $14.99–$19.99", while short (<4 h) or viral co-op games succeed at $7.99–$9.99 (PEAK $7.99, R.E.P.O.) — [Aggregator/Practitioner: gtstu.com 2026 guide](https://gtstu.com/steam-indie-game-pricing-strategy/)

**Comparable price points (management/colony/settlement builders) [Data: store facts]**
- Town to City (cozy city builder, Kwalee/Galaxy Grove), Early Access Sept 2025 at $24.99, with a campaign of two maps, sandbox with five maps, eight city stages, 30+ quests — [CogConnected](https://cogconnected.com/2025/09/town-to-city-builds-onto-steam-early-access-today/)
- Folklands (cozy settlement builder, small indie trio), Early Access March 24, 2025 at $14.99 with a 15% launch discount — [Gamespress](https://gamespress.westeu-v2.propressroom.com/tr/Folklands-a-Cozy-Settlement-Builder-from-a-Trio-of-Brothers-Launches-T)

**Valve discount rules (current Steamworks text) [Valve]** — [Steamworks: Discounting](https://partner.steamgames.com/doc/marketing/discounts)
- Launch discount: "cannot exceed 40%"; "cannot run shorter than 7 days or longer than 14 days"; must be set up before release.
- Regular discounts: minimum 10%, maximum 95%; duration 1–14 days.
- "A product cannot be discounted within 30 days of another discount" (extended from 28 to 30 days on Jan 1, 2023). Seasonal sales are exempt from this 30-day discount cooldown and from cooldowns created by other discounts (Valve example: launch discount ends Dec 15 → can join Winter Sale starting Dec 17).
- "A product cannot be discounted for 30 days following its release" and "for 30 days following a price increase in *any* currency… There are no exceptions to this rule." "Any product on a 30-day Release Cooldown or a 30-day Price Increase cooldown cannot join a Seasonal Sale."
- "Decreasing a product's base price doesn't generate a 30-day cooldown on future discounts."
- "Any discount set to 20% or greater will automatically trigger email notifications to players with your game on their wishlist."
- EA→1.0: "You may run a launch discount when transitioning from Early Access to fully released, unless your Early Access began less than 30 days or you increased your product's base price within the last 30 days."
- Deal formats: Weeklong Deals start Mondays 10am PT for 7 days; Daily Deals rotate daily at 10am PT (discount runs 7–14 days); Midweek Deals Mon 10am–Thu 10am PT.

**Price-change rules [Valve]** — [Steamworks: Pricing](https://partner.steamgames.com/doc/store/pricing)
- Price changes wait until 30 days after release; each change goes through Valve review ("one or two business days"); minimum base price equals the Multi-Variable conversion of the $0.99 tier.

**Launch discount size in practice [Practitioner/Aggregator — no primary dataset found]**
- "Most indies launch at 10–20% off" — [Bugnet blog](https://bugnet.io/blog/steam-launch-discounts-how-deep-should-you-go); a claim that a 10–15% launch discount yields "20–40% more" week-one wishlist conversions appears in marketing blogs without a dataset — treat as unverified ([search-surfaced aggregators, e.g. presskit.gg](https://presskit.gg/field-guides/steam-discounting-strategy)).
- Folklands used 15% at EA launch (see above); Parcel Simulator ran a launch discount June 20–July 10, 2025 (20 days, overlapping the Summer Sale) after "successfully request[ing] participation" — longer than the standard 14-day maximum, so treat as a Valve-granted exception — [howtomarketagame](https://howtomarketagame.com/2025/08/26/the-demo-effect-from-7000-wishlists-to-42000/)

**Regional pricing [Valve + news]**
- Steam now offers three conversion methods: exchange rate only, purchasing power only, and a "multi-variable" method combining "local purchasing power, the expected cost of comparable entertainment goods, and exchange rate" — [Steamworks: Pricing](https://partner.steamgames.com/doc/store/pricing); the new tools were reported on March 28, 2026; prices "won't change unless you manually submit and publish new prices" — [PCGamesN](https://www.pcgamesn.com/steam/regional-pricing-tools-2026)
- Last big published recommendation shift with numbers (older, Oct 25, 2022): recommended prices for a $59.99 game rose 75% in Russia, 97% in Kazakhstan, 61% in Ukraine, 85% India, 80% Indonesia, ~450–485% Turkey/Argentina — [WN Hub](https://wnhub.io/news/marketing/item-481). Valve pledged to update recommendations "on a much more regular cadence" — [WN Hub](https://wnhub.io/news/analytics/item-436)
- Russia: Russian bank cards cannot top up Steam directly in 2026; players use gift codes or intermediaries — e.g. Sberbank Online top-ups with 6% commission and a 15,000 RUB cap — [DTF 2026 how-to](https://dtf.ru/howto/4737491-popolnenie-steam-v-2026-godu); [Habr](https://habr.com/ru/amp/publications/771724)
- China: Simplified Chinese was 23.97% of Steam survey respondents in Aug 2026 (+1.45 pts) — [GamingOnLinux](https://www.gamingonlinux.com/2026/09/steam-linux-user-share-dips-below-4-percent-for-august-2026/); Feb 2026 showed an anomalous 54.60% spike likely to be corrected — [GamingOnLinux](https://www.gamingonlinux.com/2026/03/steam-survey-for-february-2026-shows-a-big-swing-to-simplified-chinese). For Backpack Battles (indie auto-battler), China was the top country by sales — [Game World Observer](https://gameworldobserver.com/2024/04/25/backpack-battles-sales-640k-copies-china-top-country)
- Valve: "Steam will be more likely to show your game to players that speak a language supported by your game" — [Steamworks: Visibility](https://partner.steamgames.com/doc/marketing/visibility)

### Inferences
- For Troll Strategy, $14.99 (EA) or $14.99–$19.99 (1.0 with 7 missions) fits the market band; going above $20 without an audience is against the 2025 trend, and below $10 lowers revenue per unit without the viral co-op hook that justifies it.
- If the plan is EA at a lower price and a higher 1.0 price, raise the price ≥30 days before 1.0, otherwise the price-increase cooldown blocks the 1.0 launch discount (follows from Valve's rules above).
- A launch less than ~30 days before a seasonal sale cannot join that sale (release cooldown); the launch discount (7–14 days) is the only discount in that window. To use a seasonal sale for a second wave, launch ≥30 days before it, or plan the launch discount to end right before the sale (Valve's own example).
- Simplified Chinese localization likely matters more for reach than RUB pricing, given Chinese share (~24%) vs. payment friction in Russia; Russian text is native for this developer, so it is free reach.
- Valve's regional recommendations are the safe default; deviating mainly affects Russia/CIS/Turkey/Argentina-type regions where key resellers arbitrage.

### Gaps
- Current exact Valve-recommended RUB, KZT, UAH and CNY prices for the $14.99/$19.99 tiers were not found in public sources (the table is in the Steamworks pricing tool behind login, or SteamDB).
- No primary dataset found on how launch-discount size (0/10/15/20%) changes week-one revenue; only practitioner claims.
- Whether a ≥20% launch discount triggers an additional wishlist email beyond the standard release email is not stated in the docs.
- A reported Steam RUB exchange-rate change to 96 RUB/USD on Feb 11–12, 2025 surfaced only in a search summary; primary source not verified.
- Russian-language share of Steam survey (one summary claimed 9.6% in June 2026) could not be verified against a primary page.

---

## 2. Release date: relation to Next Fest, seasonal sales, big releases, day of week; Valve launch visibility and its triggers

### Takeaway
Launch quality is set by wishlists built over the preceding months, not by the exact date; don't launch in the week right after Next Fest (crowding), don't launch inside the 30 days before a seasonal sale unless you accept no sale participation, and know that Valve says visibility is driven by sales and playtime, not wishlists, conversion or page traffic — while Popular Upcoming now needs ~100k wishlists, so a small game will not get it.

### Cited Findings
**2027 calendar [Data: Valve announcement, reported Jul 16, 2026]** — [GamingOnLinux](https://www.gamingonlinux.com/2026/07/valve-announced-the-themed-sale-events-for-the-first-half-of-2027/); confirmed by [Insider Gaming](https://insider-gaming.com/2027-steam-sales-revealed-for-first-half-of-the-year/)
- Shop Keeper Fest Jan 25–Feb 1; Sheep Fest Feb 4–8; Couch Co-Op Fest Feb 8–15; **Next Fest Feb 22–Mar 1**; Rhythm Fest Mar 8–15; **Spring Sale Mar 18–25**; Dinos vs. Robots Fest Mar 29–Apr 5; Racing Fest Apr 12–19; Witch Fest Apr 22–26; Fighting Fest Apr 26–May 3; **Real-Time Strategy Fest May 10–17**; Mountaineering Fest May 31–Jun 3; **Next Fest Jun 14–21**; **Summer Sale Jun 24–Jul 8**.

**Next Fest rules [Valve]** — [Steamworks: Next Fest](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest)
- Game must not be released before the edition ends; "titles may only participate in ONE Next Fest"; public demo must be live at the start; submit the demo for review ≥3 weeks before (5 weeks if releasing before the press preview); registration generally closes 5+ weeks before. Next Fest runs Oct 2026, Feb 2027, Jun 2027.

**Timing after Next Fest [Data + Practitioner]**
- June 18–26, 2024 (between Next Fest and Summer Sale): 483 games released; high-hype releases crowded the 10-slot Popular Upcoming and limited New & Trending; median hype-to-sales conversion in that "gap" was 0.184 vs ~0.15 industry median, i.e., no meaningful advantage. Zukowski: launch success is "determined by the amount of wishlists you build up in the months and months… leading up to your launch. NOT the one week before" — [howtomarketagame, Jul 2024](https://howtomarketagame.com/2024/07/08/should-you-launch-your-game-immediately-after-you-appear-in-steam-next-fest/)
- Zukowski's 2024 recap repeats: avoid launching immediately after Next Fest because of saturation; seasonal-sale-adjacent launches can work — [howtomarketagame, Dec 2024](https://howtomarketagame.com/2024/12/31/7-things-i-learned-about-steam-in-2024/)
- Counter-anecdote: Parcel Simulator (solo dev) launched June 20, 2025, 6 days before the Summer Sale, and credits charting on New & Trending during the sale plus a Valve front-page carousel feature — [howtomarketagame](https://howtomarketagame.com/2025/08/26/the-demo-effect-from-7000-wishlists-to-42000/) [Practitioner, single case]
- Release volume keeps rising: 20,282 Steam releases in 2025 — [howtomarketagame, Jan 2026](https://howtomarketagame.com/2026/01/27/what-the-hell-happened-in-2025/); 2,685 releases in August 2026 alone, +49% YoY — [WN Hub (RU)](https://wnhub.io/ru/news/stores-and-publishing/item-51960)

**Day of week [Practitioner]**
- Zukowski (2023, older): Valve blocks Saturday/Sunday launch dates (no support staff); avoid Tuesday (Steam maintenance); he preferred Monday so a game on Popular Upcoming collects weekend wishlists — [howtomarketagame](https://howtomarketagame.com/2023/02/14/whats-the-best-day-to-release-an-indie-game/). This reasoning depends on Popular Upcoming, which a small game no longer reaches (see below).
- Claims that Thursday 10am PT launches get "3–4× more weekend visibility" come from a tool vendor blog with no published dataset — [steamdata.ai](https://steamdata.ai/blog/why-thursday-10am-could-make-or-break-your-steam-launch) [Aggregator, unverified]. Steam deals rotate at 10am PT — [Steamworks: Discounting](https://partner.steamgames.com/doc/marketing/discounts)

**How launch visibility works [Valve]** — [Steamworks: Visibility](https://partner.steamgames.com/doc/marketing/visibility)
- Every release enters the New Releases Queue (prioritizing titles with the fewest views since release) and the All New Releases list.
- New & Trending: the title "may appear… if the title is doing well" and "may be bumped off this list quickly if many other popular products are releasing at the same time."
- Top Sellers on home, genre and tag pages if the game achieves sales rank; personalized recommendations by user taste; tags are essential for classification.
- Signal Valve uses: "Are people buying and playing your game?"
- Explicitly **not** factors: "Wishlists are not a factor in your game's algorithmic visibility" (they matter through launch notifications); store-page traffic; store-page conversion rate; review score as long as it is ≥40% (Mixed or above) — below 40% the game is less likely to be featured.
- Conflict: several marketing blogs state that wishlists "directly influence platform algorithms" — [fungies.io](https://fungies.io/indie-game-marketing/) — which contradicts Valve's own documentation; Valve's text should be preferred.

**Popular Upcoming and the new Personal Calendar [Practitioner, reporting Valve changes]**
- Valve raised the bar for Popular Upcoming from ~7,000 wishlists to "somewhere around 100,000"; Valve added a "Personal Calendar" widget giving upcoming games more front-page time and wishlists before launch — [howtomarketagame, Jun 25, 2026](https://howtomarketagame.com/2026/06/25/how-the-steam-personal-calendar-affects-your-launch/)

**The "10 reviews" threshold [Practitioner, older]**
- Zukowski (Jan 2022) reported one game's users/sessions growing ~1,927%/1,732% after it reached 10 reviews and described a Valve rule that games get little visibility until 10 reviews from real buyers (free keys don't count) — [howtomarketagame](https://howtomarketagame.com/2022/01/25/why-your-first-10-reviews-are-the-most-important/); [WN Hub summary](https://wnhub.io/news/analytics/item-4325). Valve's current visibility doc does not mention a review-count threshold; it only says review score ≥40% is not a factor — [Steamworks: Visibility](https://partner.steamgames.com/doc/marketing/visibility). Treat "10 reviews" as an older observation, not a documented rule.
- Zukowski (Mar 2025): 250 reviews in the first month is his minimum for "success"; 1,000 reviews is the next milestone with algorithmic boost; successful releases generate ~$150k gross in 6–9 months; 74% of titles earn the majority of their revenue in the first 3 months — [WN Hub summary](https://wnhub.io/news/stores-and-publishing/item-47399)

**Wishlist timing [Data]**
- Successful projects typically collect "more than 70% of their wishlists in the four months" before launch; most top projects keep the Steam page live 6–12 months before release; wishlist count explains ~49% of first-month sales variance (r≈0.7) — [VG Insights via GameDev Reports, Jul 2025](https://gamedevreports.substack.com/p/video-game-insights-steam-wishlists)

### Inferences
- **Option A (Feb 2027 Next Fest):** demo due for review ~Feb 1, registration ~mid-Jan 2027 — ~4 months from today, with the demo needing to be stable. Launch would then reasonably fall after the Spring Sale (late March) — e.g. April or early May 2027 — at least 30 days before the Summer Sale so the game can join it. The RTS Fest (May 10–17) is a tag-dependent themed fest; a game launched in early-to-mid April would be out of its 30-day release cooldown by then (see gap on whether themed fests are cooldown-exempt).
- **Option B (June 2027 Next Fest):** gives 8 more months for missions 2–7 and saves; launch after the Summer Sale (mid-July to September 2027), avoiding the June 22–July 8 crowd, and ≥30 days before the autumn sale.
- Because Popular Upcoming now needs ~100k wishlists, a small game's launch visibility will come from New & Trending/Top Sellers via day-one sales from wishlist emails. That makes the size of the wishlist pool at launch the key controllable input; the day of week is a second-order choice (avoid weekends, which Valve blocks, and Tuesdays).
- Supporting more languages (Russian native + English + Simplified Chinese) is one of the few levers Valve states directly affects visibility.

### Gaps
- Whether themed fests (e.g., RTS Fest) are exempt from the 30-day discount cooldown like seasonal sales, and their exact tag eligibility, was not confirmed in the docs fetched.
- No current (2025–26) dataset found on day-of-week performance; only the 2023 Zukowski post and an unsourced vendor blog.
- The specific number of wishlists or day-one sales needed to enter New & Trending is not published by Valve; no reliable third-party threshold found.
- The October 2026 Next Fest is too close to be usable (demo review needed ~3 weeks prior); not analysed further.

---

## 3. Early Access vs full release for strategy/management games

### Takeaway
EA is not a fix for a weak launch: in 2025 only 20% of EA games earned more in their 1.0 month than in their first EA month (median 1.0 month = 40% of the EA month), and Valve says EA titles don't get full launch visibility until 1.0. EA fits a "crafty-buildy" strategy game only when it already has a solid, bug-tested 10–20 h loop and the team can ship monthly updates — which a game with 1 of 7 missions and no saves does not yet have.

### Cited Findings
- 2025 EA graduations: only 20% earned more in their first 30 days of 1.0 than in their first 30 days of EA; median 1.0-month revenue ≈40% of first EA month; excludes games with <5,000 EA sales. Examples: Supermarket Simulator −95%, Backpack Battles −87%, Slime Rancher 2 −85%; News Tower, Mars First Logistics and Escape the Backrooms more than doubled. Simon Carless: the idea that a weak EA launch can be "fixed by releasing version 1.0" is "increasingly out of touch with reality"; 1.0 works mainly as a notification to wishlisters — [WN Hub summary of GameDiscoverCo, Dec 10, 2025](https://wnhub.io/news/stores-and-publishing/item-49521)
- 2026 update: of 91 games that left EA in 2026 so far, 21% earned more in the 1.0 month; strategy titles cited as doing well at 1.0 include Timberborn, shapez 2 and Terra Invicta (growing systems rather than changing the core) — reported in search results summarising [Niche Gamer](https://nichegamer.com/new-data-shows-only-21-of-early-access-games-perform-better-after-full-release/) (page returned 403; not directly verified)
- Zukowski's 2024 criteria for EA: 7,000+ wishlists, 6+ months of marketing, a crafty-buildy game with 10–20 hours of content, bug-tested gameplay, regular monthly updates and an engaged community; his median 1.0 launch sold ~0.70× the EA launch (different sample/method from GameDiscoverCo's 0.40×) — [howtomarketagame, Dec 2024](https://howtomarketagame.com/2024/12/31/7-things-i-learned-about-steam-in-2024/)
- Parcel Simulator's developer: "the best way is to skip EA so you can get into New & Trending" — [howtomarketagame](https://howtomarketagame.com/2025/08/26/the-demo-effect-from-7000-wishlists-to-42000/) [Practitioner]

**Valve rules and guidance on EA [Valve]** — [Steamworks: Early Access](https://partner.steamgames.com/doc/store/earlyaccess)
- "Don't launch in Early Access without a playable game. If you have a tech demo, but not much gameplay yet, then it's probably too early." Do not use EA "solely to fund development"; "Do not make specific promises about future events."
- Visibility: EA titles appear in All New Releases (EA section), wishlisters get emails, they "may appear in New and Trending if performing well" and in top sellers, but they "will not receive your full launch visibility until you are fully released."
- If 12+ months pass without a build update or an update event, Steam shows a notice that the game hasn't been updated recently; abandoned games can be removed.
- Release to 1.0 is one-way (can't go back to EA); EA price must not exceed other stores.
- Conflict: Zukowski states games move to New & Trending after Popular Upcoming "if it is not an Early Access Game" — [howtomarketagame](https://howtomarketagame.com/2026/06/25/how-the-steam-personal-calendar-affects-your-launch/) — while Valve's doc says EA titles "may appear in New and Trending if performing well". Both agree EA gets reduced launch visibility.

**Strategy EA case from a Russian team [Data: developer-reported]**
- Diplomacy is Not an Option (Door 407; RTS + tower defense + city-building): EA launch Feb 9, 2022 sold ~60,000 copies and earned >$1M in six days, plus ~130,000 new wishlists after launch — [WN Hub](https://wnhub.io/news/stores-and-publishing/item-19894); left EA in 2024 ([Rutab](https://rutab.net/b/video-games/2024/10/05/igra-diplomacy-is-not-an-option-vyshla-iz-rannego-dostupa.html)) with >100,000 copies sold in the release month and >1M lifetime wishlists; 93% Steam rating — [WN Hub (RU), Dec 18, 2024](https://wnhub.io/ru/news/stores-and-publishing/item-46573). A rare strategy example where the 1.0 month beat the EA launch window in units.

### Inferences
- For Troll Strategy as of today (1 mission, no saves), EA would violate the spirit of Valve's "playable game" guidance for a management game where progress persistence is expected; saves plus enough content for ~10–20 h (or an endless/sandbox mode) look like the minimum EA bar by Zukowski's criteria.
- EA spends the one-time "new release" moment at reduced visibility; with the 2025 data showing 1.0 typically earning ~40% of the EA month, a small game with few wishlists risks spending its best moment on an unfinished build. EA makes more sense if the game is systems-driven and replayable (production chains, auto-battles) and can show monthly updates; a linear 7-mission campaign fits a 1.0 release better.
- A hybrid: release a free demo (mission 1) for Next Fest, keep building missions 2–7 + saves, and launch 1.0 — or EA only once the campaign is ≥50% done and saves exist.

### Gaps
- No subgenre-specific EA success-rate dataset (strategy/management only) was found; the strategy exceptions are anecdotal.
- No data on review-score risk specifically attributable to EA (e.g., share of EA games dropping below 70% positive).
- Update-cadence benchmarks beyond "monthly" (Zukowski) and Valve's 12-month abandonment notice were not found.

---

## 4. Post-launch: updates, sales calendar, bundles, long tail; review count → revenue (Boxleiter)

### Takeaway
Most indie revenue lands in the first 3 months (74% of titles), so the post-launch plan is mainly: seasonal sales (exempt from cooldowns), 30-day-spaced discounts with ≥20% to re-email wishlisters, tag-matching themed fests, and updates; use roughly 30–60 copies per review for estimates (recent releases ~30, $20+ games ~60).

### Cited Findings
- 74% of titles earn the majority of their revenue in the first three months; Beltmatic (factory automation) is a noted slow-burn exception (51 reviews in 3 months, 1,000 after 9 months), which Zukowski calls "not the real Steam" — [WN Hub summary of Zukowski, Mar 2025](https://wnhub.io/news/stores-and-publishing/item-47399) [Practitioner/Data]
- Discount mechanics (cooldowns, 20% wishlist email, seasonal exemption, Weeklong/Daily/Midweek deals) — [Steamworks: Discounting](https://partner.steamgames.com/doc/marketing/discounts) [Valve]
- Weekend Deals need roughly 20,000 reviews; Daily Deals roughly $150,000 revenue (practitioner estimates, not Valve rules) — [howtomarketagame, Dec 2024](https://howtomarketagame.com/2024/12/31/7-things-i-learned-about-steam-in-2024/)
- Valve: demos now get front-page featuring for demo launches, wishlist notifications and demo reviews; separate demo pages allow demo reviews without hurting conversion — [howtomarketagame, Dec 2024](https://howtomarketagame.com/2024/12/31/7-things-i-learned-about-steam-in-2024/)
- EA abandonment notice after 12 months without an update — [Steamworks: Early Access](https://partner.steamgames.com/doc/store/earlyaccess)

**Review-to-sales ratio (Boxleiter) [Data]**
- Gamalytic (Jul 2023): ratio has "decreased ever so slightly every year" since 2019, when Steam started prompting reviews (which roughly halved the ratio); at 90%+ positive ~30 sales per review, ~70% positive ~60, under 60% ~30; heavily discounted games and free games have higher ratios; "audience is perhaps the most significant factor" — [Gamalytic](https://gamalytic.com/blog/a-deep-dive-into-the-steam-review-ratio)
- VG Insights: ~93 copies per review for 2013 releases vs ~32 for 2023 releases; for paid games priced over $20 the median ratio is around 60; average discounts up to 20% show ~34–35 copies/review, discounts of 30%+ show 40–57 — reported in search results summarising [VG Insights](https://wp.vginsights.com/further-analysis-into-steam-reviews-to-sales-ratio-how-to-estimate-video-game-sales/2) (not fetched directly)
- Common working range 20–60 sales per review, starting point 30× for indies; margin of error 30–50% — [Aggregator: Steam Page Analyzer](https://www.steampageanalyzer.com/blog/boxleiter-method-explained)

### Inferences
- For a $15–20 strategy game released in 2027: units ≈ reviews × 30–60; e.g., Zukowski's 250-review "success" bar ≈ 7,500–15,000 units in the first month; gross ≈ units × price × (1 − average discount/regional effect). Revenue estimates should be shown as ranges.
- Post-launch cadence that fits the rules: launch discount (7–14 days) → no discount for 30 days → next seasonal sale (exempt) → a 20%+ discount at least 30 days apart to re-trigger wishlist emails; pair each discount with a content update.
- A campaign game benefits from shipping missions as free updates after an EA or 1.0 launch only if each update is paired with a visibility event (update news + discount), since 1.0/updates mostly notify existing wishlisters (GameDiscoverCo).

### Gaps
- No 2024–26 data found on bundle performance (Steam bundles, Humble, Fanatical) for small strategy games.
- No 2025–26 updated Boxleiter figures by genre (strategy vs other); Gamalytic says genre matters less than audience.
- No quantified data found on update-driven revenue bumps for small sims.

---

## 5. Realistic outcomes: revenue distribution, wishlist conversion, subgenre comparison

### Takeaway
The median Steam release earns low hundreds to low thousands of dollars; roughly 40–66% of 2025 releases made under $1,000 and ~8% reached $100k; only ~3% got 1,000+ reviews. First-week sales are typically 0.10–0.15× launch wishlists (0.10× for games above $10). Simulation/management are among the more reliable genres by count of hits, and colony sims/4X/city builders had historically high median revenue — but those medians are survivorship-biased.

### Cited Findings
**Revenue distribution [Data]**
- 2025 releases (Gamalytic, as of Oct 2025, cited by Artur Smiarowski): ~13,000 games released by then; 8% reached $100,000; 40% failed to earn the $1,000 needed to recoup the $100 Steam Direct fee; average playtime, average revenue per game and total revenue across new projects fell vs 2024 — [WN Hub, Oct 22, 2025](https://wnhub.io/news/stores-and-publishing/item-49120)
- Conflicting aggregator figures for 2025: 8,388 (65.9%) of 2025 releases earned under $1,000; median indie revenue $249 ($174 after Valve's cut); 47.4% sold under 100 copies and 28% sold 100–1,000 — [Yahoo/Aggregator](https://tech.yahoo.com/gaming/articles/over-5-000-games-released-152604830.html); [Steam Page Analyzer](https://www.steampageanalyzer.com/blog/median-steam-game-2026) (not verified against Gamalytic; the 40% vs 65.9% gap likely reflects different cut-off dates and definitions)
- 2025: 20,282 releases; 608 reached 1,000+ reviews (579 AA/indie) = 2.99%, highest in four years; by count of 1,000-review games: Simulation 43 (#2), Management 19 (#10); highest per-genre success rates: Open World Survival Craft 20.8%, Farming 8.3%, Roguelike Deckbuilder 5.1% — [howtomarketagame, Jan 27, 2026](https://howtomarketagame.com/2026/01/27/what-the-hell-happened-in-2025/)
- Steam ~$17.7B revenue in 2025 (+15%), indie ~$4.5B (~25%); ~300 of ~20,000 new games earned >$1M — [Notebookcheck](https://www.notebookcheck.net/Indie-games-accounted-for-25-of-Steam-s-revenue-in-2025.1189429.0.html) [Aggregator]
- August 2026 releases: 2,685 titles (+49% YoY); 120 earned >$100k and 23 (0.85%) >$1M (early-window numbers) — [WN Hub (RU)](https://wnhub.io/ru/news/stores-and-publishing/item-51960)
- Older (Oct 2023) Gamalytic, releases of the previous 3 years: 76.5% under $5k, 13.4% $5k–50k, 4.5% $50k–200k, 5.6% over $200k; highest-median genres: open-world survival craft, colony sims, 4X, city builders, management — [Gamalytic via GameDev Reports](https://gamedevreports.substack.com/p/gamalytic-67-of-games-on-steam-earned)
- Older (2021) tag medians of estimated net revenue: Colony Sim $44k (292 games), 4X $35k, City Builder $22k, Grand Strategy $19k vs Action RPG $4.3k — [GameDiscoverCo, Mar 3, 2021](https://newsletter.gamediscover.co/p/which-genre-should-your-next-pc-game) (older data; multi-tag and survivorship caveats noted by the author)

**Wishlists and conversion [Data]**
- Median first-week conversion 0.15× launch wishlists (games with 25k+ wishlists, 2024–25); 0.10× for games priced >$10; ~0.14× excluding NSFW; no long-term decline in conversion, but wishlists are harder to gather. Underperformers had a median 67% (Mixed) first-week score and ~411 days on Steam pre-release vs 91% and ~214 days for top performers — [GameDiscoverCo via GameDev Reports](https://gamedevreports.substack.com/p/gamediscoverco-the-state-of-steam)
- 66% of games have under 10,000 wishlists; 6% reached 100,000+ in the last 12 months; only 9% (141 of 1,500) sold more copies than their launch wishlist count; wishlists explain ~49% of first-month sales variance — [VG Insights via GameDev Reports, Jul 2025](https://gamedevreports.substack.com/p/video-game-insights-steam-wishlists)
- Games with 100k+ wishlists had a 71% probability of strong first-month sales vs 17% for those below — [Sensor Tower/VG Insights report summary](https://gameindustrylibrary.com/documents/the-importance-of-wishlists-2025) [Aggregator summary]
- Strategy/RPG/action have higher median wishlists than other genres (VG Insights) — [GameDev Reports](https://gamedevreports.substack.com/p/video-game-insights-steam-wishlists)

### Inferences
- With no existing audience, the base-rate expectation for a first Steam game is under $5k gross; reaching the top ~8% ($100k+) generally requires tens of thousands of launch wishlists. Using 0.10× (price >$10): 10k wishlists → ~1,000 first-week units; 30k → ~3,000; Zukowski's 250-review month (~7.5–15k units) implies roughly 30–60k+ wishlists, or a viral/creator boost like Parcel Simulator's.
- Strategy/management is a comparatively favourable lane (big, loyal audience; Simulation and Management among top genres by count of hits), but the high historical medians come from games that already had reviews; they do not lift the floor for a game without marketing.
- First-week review score matters to conversion (underperformers median 67%): shipping a stable demo and polished first hour is part of the commercial plan.

### Gaps
- No 2025–26 per-subgenre revenue medians (automation, colony sim, city builder, auto-battler) from Gamalytic/VG Insights were accessible without subscriptions.
- No dataset found that isolates games from Russian/CIS solo developers.
- First-month/first-year multiples of first-week sales were not found in the fetched sources.

---

## 6. Comparable games and postmortems (automation / colony / logistics / auto-battler; incl. Russian/CIS)

### Takeaway
Public numbers exist mostly for successes (survivorship bias). The repeatable lessons: a demo plus one creator video can multiply wishlists (Parcel Simulator 7k→42k), full 1.0 launches can win New & Trending, auto-battlers sell well in China, and Russian/CIS failures on DTF show that a few thousand wishlists convert to very little.

### Cited Findings
- **Parcel Simulator** (solo UK dev, management/simulation, 1.0 June 20, 2025): 7,000 wishlists over 640 days before a demo (~11/day); after the Feb 24, 2025 demo, 362 wishlists/day for 116 days; Feb Next Fest gave ~10,000 wishlists (expected ~1,500); one Real Civil Engineer video (~2.3M views) brought ~7k wishlists; 42,000 wishlists at launch; 10k units in 24 h, 15k in 48 h, 50k in 2 weeks; no EA; launched 6 days before the Summer Sale; Valve front-page carousel; Keymailer ($25/month) for creator outreach — [howtomarketagame, Aug 26, 2025](https://howtomarketagame.com/2025/08/26/the-demo-effect-from-7000-wishlists-to-42000/) [Practitioner case; conversion here was well above median]
- **Diplomacy is Not an Option** (Door 407, Russian team; RTS/tower defense/city-building): ~60k copies and >$1M in 6 days of EA (Feb 2022), +130k wishlists after EA launch; 1.0 in 2024: >100k copies in the release month; >1M lifetime wishlists — [WN Hub](https://wnhub.io/news/stores-and-publishing/item-19894); [WN Hub (RU)](https://wnhub.io/ru/news/stores-and-publishing/item-46573)
- **Backpack Battles** (German indie couple; PvP inventory auto-battler; EA Mar 8, 2024): ~471k wishlists before launch; 500k copies in under two weeks; 640k in the first month; China the top country — [Game World Observer](https://gameworldobserver.com/2024/04/25/backpack-battles-sales-640k-copies-china-top-country); its 1.0 month earned 87% less than the EA launch month — [WN Hub/GameDiscoverCo](https://wnhub.io/news/stores-and-publishing/item-49521)
- **Kingdoms Reborn** (solo-dev city builder, EA): third-party estimate ~204k copies, ~$2.4M gross, ~307k wishlists as of July 2026 ("medium confidence" estimate) — [Raijin.gg](https://raijin.gg/app/1307890/Kingdoms_Reborn) [estimate, not developer-reported]
- **shapez 2** (factory automation, EA): reported >270,000 sales in the first month — [GameDiscoverCo](https://newsletter.gamediscover.co/p/steam-the-new-wishlists-to-first) (from search summary)
- **Mars First Logistics** (physics logistics sim, EA June 2023): its 1.0 month more than doubled its first EA month revenue — [WN Hub/GameDiscoverCo](https://wnhub.io/news/stores-and-publishing/item-49521)
- **Beltmatic** (small factory-automation game): 51 reviews in 3 months, 1,000 after 9 months — slow-burn exception — [WN Hub summary of Zukowski](https://wnhub.io/news/stores-and-publishing/item-47399)
- **Final Outpost: Definitive Edition** (indie base-builder colony sim, released May 22, 2025): ~30,000 wishlists accumulated since the page went live Aug 4, 2024 (~9.5 months) — [Gamespress](https://gamespress.westeu-v2.propressroom.com/Final-Outpost-Definitive-Edition-launches-today) (no sales disclosed)
- **Russian-language DTF postmortems (not strategy-specific):** Thermonuclear (solo dev) had 1,380 wishlists at release and saw ~1.2% conversion against an expected ≥4% in its first 12 days — [DTF](https://dtf.ru/indie/955163-thermonuclear-12-dnei-spustya-reliza); Toffee Cats earned $456 in its first month, with the author admitting he assumed a cute cat game would "sell itself" — [DTF](https://dtf.ru/indie/3853876-provalnyj-reliz-toffee-cats-v-steam)
- Polden Publishing (Russian-speaking publisher) reports its portfolio gathered >4M Steam wishlists — [WN Hub (RU)](https://wnhub.io/ru/news/stores-and-publishing/item-51347) (context on publishers active in the CIS indie scene)

### Inferences
- Closest commercial analogues for Troll Strategy's pitch (visible logistics + settlement + auto-battle) are Diplomacy is Not an Option (settlement + battles, Russian team), Mars First Logistics (logistics as spectacle), and the auto-battler audience proven by Backpack Battles (strong in China). None combines all three; a "visible logistics" hook is the kind of thing a single creator video can spread (cf. Parcel Simulator's engineer-YouTuber spike).
- The demo is the single most evidence-backed lever for a solo dev without an audience (Parcel Simulator: ~33× faster daily wishlist rate after the demo; Next Fest gave ~7× the expected wishlists).
- Russian-speaking solo releases with ~1–2k wishlists convert poorly (Thermonuclear), consistent with VG Insights' finding that small wishlist counts predict sales weakly.

### Gaps
- No published postmortem with wishlist and sales numbers was found for a small indie that combines automation/logistics with auto-battles.
- No Habr/DTF postmortem with full numbers was found for a CIS-made strategy/city-builder in 2024–26 (the WN Hub list of 2025's top-grossing Russian-speaking games is paywalled).
- Prices of most comparables were not systematically collected (check SteamDB for Diplomacy is Not an Option, Kingdoms Reborn, Mars First Logistics, Backpack Battles, Parcel Simulator).
