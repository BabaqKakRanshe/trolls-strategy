# Legal, payments and compliance for a Russia-based solo developer releasing on Steam (and itch.io), plus Steam AI disclosure and third-party asset licences

Research date: 2026-10-05. **This is not legal or tax advice.** Rules on currency control, sanctions and taxes change often. Many payout reports are anecdotes from developers or promotional posts from intermediaries. Each item below carries its date. Tags: **[OLD]** = before 2025, **[anecdotal]** = one developer's experience, **[promo]** = written by a company selling the service, **[snippet]** = seen only in a search snippet because the page could not be fetched in full.

---

## 1. Paying the $100 Steam Direct fee from Russia (2025–2026)

### Takeaway
Valve charges $100 per app. The fee can be paid with "any payment method that Steam supports in your country" except Steam Wallet funds. Russia has no working Steam payment method since March 2022, so a Russia-based developer pays the fee with a foreign card (ideally their own, from a foreign bank), through a trusted person abroad, or through a paid intermediary (around ₽8,800 in mid-2026, as an anecdote). No official Russia-specific route exists.

### Cited Findings
- Fee is "$100 USD (or equivalent) … for each new app". It is "recoupable in the payment made after your product has at least $1,000.00 Adjusted Gross Revenue" and is payable by "any payment method that Steam supports in your country, excluding Steam wallet funds". Valve may add VAT/GST to the fee. — [Steamworks: Steam Direct Fee](https://partner.steamgames.com/doc/gettingstarted/appfee)
- Onboarding requires bank information, tax information and identity verification. The tax and identity check "may take 10 to 15 business days". The current onboarding page states "a 21-day waiting period between when you paid the app fee and when you can release that game". A public Coming Soon page must be up "for at least two weeks" before release. (Older material says 30 days. Use the live page.) — [Steamworks: Onboarding](https://partner.steamgames.com/doc/gettingstarted/onboarding)
- Demos are created from the base game's app page via "Add Demo". The demo documentation mentions no separate Steam Direct fee for a demo. A Valve review is required before the first demo release. — [Steamworks: Demos](https://partner.steamgames.com/doc/store/application/demos)
- Russian Visa/Mastercard cards have not worked with Steam since March 2022, and the wallet cannot pay Steam Direct. Slava Gris (July 2024) lists three workarounds: (1) a foreign developer who already has a Steamworks account pays and transfers the app; (2) invite a foreign user into your Steamworks company with permission to pay; (3) change account region and use VPN or remote access with an international card. — [WN Hub / App2Top, Slava Gris, 12 Jul 2024](https://wnhub.io/ru/news/investment/item-44141)
- **[2026, anecdotal]** DTF guide (author Game17Hz, 4 Jul 2026) names: a Kazakhstan-based intermediary ("Visual Floss Team") at about ₽8,800 all-in, which may switch the account region to Kazakhstan during the process; GGSel marketplace sellers (pricier, unverified by the author); and renting a foreign card from classified ads (high trust risk). Identity verification needs passport photos plus a selfie holding the passport. VPN should be off during registration. "Sole Proprietorship" is chosen by individuals. Verification took 2–7 days. — [DTF: Оплата Steam Direct из России в 2026](https://dtf.ru/howto/5165888-oplata-steam-direct-iz-rossii)
- **[2025, anecdotal]** A developer who published in 2025 confirms "вы не сможете оплатить взнос с Российской карты". The options are a personal card from a foreign bank or trusted contacts abroad. They named their own method only in private messages. Valve's tax and identity data went through a third-party verification and moderation step. — [DTF, Red Face Games, 12 Dec 2025](https://dtf.ru/id615524/4528029-kak-vypustit-igru-v-steam-iz-rossii-opyt-razrabotchika)
- **[promo, Feb 2025]** Intermediary Easy Payments recommends foreign Visa/Mastercard cards, which can pay the fee and also receive payouts. — [vc.ru / Easy Payments, 11 Feb 2025](https://vc.ru/services/1804828-kak-razrabotchikam-iz-rossii-i-belarusi-poluchat-ot-steam-vyplaty-za-prodazhu-igr)
- **[anecdotal, mid-2026]** GameDev.ru's long-running thread "Санкции и платежи от Valve" was still active in June 2026. A post on 24.06.2026 linked a third-party "steam-direct-product-submission-fee-payment" service. Another user said VPN does not get you past verification. Page encoding was broken, so the details are unreliable. — [GameDev.ru thread, p.391](https://gamedev.ru/industry/forum/?id=267528&page=391)

### Inferences
- The cleanest route pays the fee with a card from a foreign bank account **in the developer's own name**, the same account that will later receive payouts (see Q2). That keeps the identity, payment and bank records consistent for Valve's KYC. Intermediaries and rented cards add fraud and chargeback risk and leave a payer-identity mismatch. Nothing found says Valve forbids a third party paying the fee, so this is a risk judgement, not a stated rule.
- A free demo should not need a second $100 fee. It is a child app of the paid game's app ID. Next Fest therefore needs only the main app's fee, paid early enough that the Coming Soon page can be live.

### Gaps
- No Valve statement addresses Russia specifically for the fee. Whether Valve ever flags region changes or VPN use during onboarding is known only from anecdotes.
- The 21-day vs 30-day waiting period discrepancy: the live onboarding page says 21 days. Re-check at onboarding time.

---

## 2. Valve payouts to Russia-based developers: status since 2022, bank rules, third-country banks, alternatives, risks

### Takeaway
Valve pays monthly, **in USD by SWIFT only, to an account in the partner's own name**. Since April 2022 it has paid Russian accounts only through a foreign intermediary bank, and never to sanctioned Russian banks. By 2025–2026 very few Russian banks still take USD SWIFT. Most Russia-based developers use a USD account in a third country (Kazakhstan, Kyrgyzstan, Armenia, Georgia, Turkey, UAE, Serbia and others). Those banks have been tightening KYC on Russians steadily. In Oct–Dec 2025 EU sanctions hit Kyrgyz banks, and Armenian and Serbian banks began closing Russians' accounts. Accounts get frozen, payments get returned, and fees can be high. Alternatives: a foreign legal entity, a publisher, or (for ordinary Russian income only) payment agents.

### Cited Findings
**Valve's rules (official, current)**
- "We pay in US Dollars only. Your account needs to be able to accept payment via SWIFT wire transfer in USD." "Your bank account information needs to be in your name directly. We cannot process payments that require additional instructions to complete." Valve pays "by the 30th of the month following sales" and may hold payments under $100. Valve passes on no wire fees but "cannot control bank fees … imposed … by intermediary banks or your receiving bank". Failed payments are returned, Valve tells the partner the reason, and the money goes out in the next monthly batch once corrected. — [Steamworks: Payments FAQ](https://partner.steamgames.com/doc/finance/payments_salesreporting/faq)
- "The account holder name on your bank account must match the name you provide when onboarding" and the account must be "under the same name you provided for Legal Company Name". — [Steamworks: Onboarding](https://partner.steamgames.com/doc/gettingstarted/onboarding)
- The Steamworks docs say nothing about the bank's country having to match the partner's country of residence. — [Steamworks: Payments FAQ](https://partner.steamgames.com/doc/finance/payments_salesreporting/faq) (absence noted by reading the page)

**History [OLD]**
- March 2022: Valve suspended payments to Russia, Belarus and Ukraine. From 21 April 2022 it resumed payments to Russian developers who added foreign intermediary bank details (SWIFT, name, country, city, address) in their financial settings. — [Habr, Apr 2022](https://habr.com/ru/news/662299/); [DTF, Apr 2022](https://dtf.ru/gameindustry/1169452-steam-vozobnovil-vyplaty-razrabotchikam-iz-rossii-i-ukrainy)
- [OLD, Jun 2022] Community guide: no payouts to sanctioned/SWIFT-disconnected banks (Sber, VTB, Alfa, Rossiya, VEB, Novikom, PSB, Rosselkhoz, Otkritie, Sovcom, MKB). Raiffeisen and Tinkoff (3% fee on incoming SWIFT, minimum 200) were listed as working, with US intermediaries (Citibank CITIUS33, BNY Mellon IRVTUS3N, JPMorgan CHASUS33). **The list is outdated.** — [Steam Community guide, upd. 23 Jun 2022](https://steamcommunity.com/sharedfiles/filedetails/?id=2797389965)

**Russian banks in 2024–2026**
- [Jul 2024] Slava Gris named the Russian banks still accepting SWIFT then: Chelyabinvestbank (needs a ruble account), Moskommertsbank (reported to charge a large daily fee for holding currency), and T-Bank (through intermediaries, high fees). — [WN Hub, Slava Gris, 12 Jul 2024](https://wnhub.io/ru/news/investment/item-44141)
- [snippet] A GameDev.ru forum snippet mentions Moskommertsbank restricting incoming transfers from 21 November, with transfers returned to the sender. The year could not be confirmed. — [GameDev.ru thread (search snippet)](https://gamedev.ru/industry/forum/?id=267528&page=310)
- [2026, anecdotal] The July 2026 DTF guide states "Steam cannot send funds to Russian accounts": the balance simply accumulates until the developer has a compatible foreign account. This contradicts the 2022 Valve position that non-sanctioned Russian banks with an intermediary can receive. In practice almost no Russian bank route is reported to work in 2026. — [DTF, 4 Jul 2026](https://dtf.ru/howto/5165888-oplata-steam-direct-iz-rossii)
- [promo, Jan 2025] "Most Russian institutions are disconnected from SWIFT or under sanctions." The account holder name must match the Steam registration. Suggested countries: Kazakhstan, Kyrgyzstan, Turkey, UK, USA, Hong Kong, Cyprus, Georgia. — [Easy Payments blog, 28 Jan 2025](https://easypayments.online/blog/kak-poluchat-vyplaty-ot-steam)
- [promo, Feb 2025] Requirements: USD, a bank not on sanctions lists, a SWIFT connection. Accounts in CIS countries or Turkey can be opened remotely through intermediaries in up to two weeks. — [vc.ru / Easy Payments, 11 Feb 2025](https://vc.ru/services/1804828-kak-razrabotchikam-iz-rossii-i-belarusi-poluchat-ot-steam-vyplaty-za-prodazhu-igr)

**Third-country banks: the tightening trend**
- [Oct 2025] The EU's 19th sanctions package (effective 12 Nov 2025) sanctioned Kyrgyz banks Tolubai and Eurasian Savings Bank (bringing the number of sanctioned Kyrgyz banks to four), VTB's Kazakh subsidiary, and three Tajik banks. It bars their transactions with EU counterparties and USD/EUR currency operations. — [Current Time, 25 Oct 2025](https://www.currenttime.tv/a/sanktsii-evrosoyuza-protiv-bankov-v-kazahstane-kyrgyzstane-i-tadzhikistane/33569698.html)
- [Dec 2025] After the EU added Russia to its AML "high-risk countries" list in early December 2025, banks in Armenia and Serbia began closing Russians' accounts, **including for holders of residence permits**. They are also blocking transfers between a client's own accounts, refusing to open accounts, and freezing funds for indefinite periods. Banks in Kazakhstan, Tajikistan and Oman tightened checks on source of funds and tax status. Banks cite correspondent-bank pressure and fear of secondary sanctions. — [The Moscow Times (ru), 20 Dec 2025](https://ru.themoscowtimes.com/2025/12/20/banki-armenii-iserbii-nachali-blokirovat-perevodi-rossiyan-iz-za-novih-pravil-es-a183371)
- [OLD, May 2022, context] Kazakh and Armenian banks began requiring proof of residence, a local employment contract or a lease. Altyn Bank stopped opening non-resident accounts, and Kaspi offered no FX accounts. — [Forbes.kz, 20 May 2022](https://forbes.kz/articles/pochemu_rossiyanam_vse_trudnee_otkryit_bankovskie_scheta_daje_v_blijnem_zarubeje)
- [snippet, Jul 2026] Headline: almost 100 Russian banks were put under EU sanctions, and the "Zolotaya Korona" and "Tsifra bank" money-transfer services stopped transfers to Georgia and Kazakhstan. This hits the route for bringing money from a third-country account back to Russia. — [Meduza, 24 Jul 2026 (headline only; fetch failed)](https://meduza.io/feature/2026/07/24/pochti-100-rossiyskih-bankov-popali-pod-sanktsii-evrosoyuza-zolotaya-korona-i-tsifra-bank-ostanovili-perevody-v-gruziyu-i-kazahstan)

**E-money alternatives are closed to Russian residents**
- Payoneer closed Russian accounts on 16 December 2022. — [vc.ru: оплата itch.io из России](https://vc.ru/services/2118615-oplata-itch-io-iz-rossii)
- [snippet, Dec 2025] Wise blocked the cards of Russians and Belarusians who do not hold EEA residence, and set a deadline of 30 Jan 2026 to prove such residence. — [Meduza, 2 Dec 2025 (snippet)](https://meduza.io/news/2025/12/02/platezhnyy-servis-wise-nachal-blokirovat-karty-rossiyan-i-belorusov-iz-za-novyh-sanktsiy-es); [The Moscow Times, 1 Dec 2025 (snippet)](https://ru.themoscowtimes.com/2025/12/01/esche-odin-evropeiskii-platezhnii-servis-stal-blokirovat-karty-rossiyan-a181534)

**Alternatives**
- A foreign legal entity gives access to business accounts, Payoneer and Wise, plus treaty and tax options. — [Easy Payments blog [promo]](https://easypayments.online/blog/kak-poluchat-vyplaty-ot-steam). A Russian tax resident's foreign company or foreign sole-proprietor registration does **not** take them out of Russian currency law. — [Habr, F. Andreev (international tax lawyer), 9 Apr 2026](https://habr.com/ru/articles/956854)
- Payment-agent model (for Russian ИП invoicing foreign clients): an agent receives the FX, converts it and pays rubles under an agency agreement or assignment, backed by agent reports and acts. — [Habr, 9 Feb 2026](https://habr.com/en/articles/994432)
- Fees can eat payouts: Valve does not cover intermediary or receiving bank fees. — [Steamworks: Payments FAQ](https://partner.steamgames.com/doc/finance/payments_salesreporting/faq)

### Inferences
- Under Valve's "in your name directly / no additional instructions" rule, a **payment agent cannot simply receive Steam payouts for you**. The Steamworks partner (you, your foreign company, or a publisher) has to own the receiving account. For a solo developer the realistic options are: (a) a USD account in your own name in a third-country bank that still serves Russian residents; (b) a publisher or distributor who is the Steamworks partner and pays you under a contract (Russian currency control then applies to that contract); (c) your own foreign entity, which only makes sense with real residence or substance abroad.
- The trend from 2022 to 2026 is one-directional: each EU package or AML listing narrows the set of third-country banks that will hold a Russian resident's USD and receive from US banks. Keep a second receiving option ready. Expect occasional returned payments (Valve re-sends them in the next batch) and possible freezes. Don't let large balances sit in one foreign bank.
- Kyrgyzstan looks like the riskiest jurisdiction of the 2025 group, since several of its banks were sanctioned. The Armenia and Serbia reports (Dec 2025) undercut the "get a residence permit and open an account" approach.

### Gaps
- No reliable, dated 2025–2026 list of specific third-country banks that currently receive Valve payouts for Russian residents. The GameDev.ru thread is the richest source but could not be decoded. A developer should ask in that thread or in Russian indie Telegram chats before opening an account.
- No Valve statement on sanctions screening of partners resident in Russia, and none found on residents of occupied Ukrainian regions under comprehensive US sanctions.
- No data on how often Valve payouts to Kazakh, Georgian or UAE banks get returned.

---

## 3. US tax interview (W-8BEN) withholding for Russian residents

### Takeaway
Since **16 August 2024** the US–Russia tax treaty's income articles have been suspended, so a Russian tax resident **cannot claim treaty benefits**. Valve withholds **30% of the developer's share of US-sourced sales only**. In practice the developer gets about 49% of a US sale's gross instead of 70%. Non-US sales carry no US withholding. Treaty eligibility follows **tax residence**, not bank location, so a Kazakh or Armenian bank account changes nothing.

### Cited Findings
- Russia's decree of 8 Aug 2023 suspended treaty provisions with 38 countries. The US replied with a formal notice on 21 Jun 2024, effective **16 Aug 2024**. It suspended Art. 1(4), Arts. 5–21, Art. 23 and the Protocol. "For US-source payments made on and after 16 August 2024, reduced rates of withholding tax no longer apply … subject to the 30% statutory rate". This covers royalties. — [EY Tax Alert, Jun 2024](https://www.ey.com/en_gl/technical/tax-alerts/us-treasury-suspends-key-provisions-of-us-russia-tax-treaty-and-)
- Valve: withholding is shown as the portion "derived from sales made in the US – or the U.S. source income". The default is 30%. Treaty benefits require Form W-8BEN with a foreign or US TIN. Without a treaty, "We will be required by the IRS to withhold 30% of your revenue share payment". The applicable rate is shown on the partner's tax information page. — [Steamworks: Tax FAQ](https://partner.steamgames.com/doc/finance/taxfaq)
- Valve emailed Russian developers that from 16 Aug 2024 an extra 30% is withheld on US sales only. Half-Face Games: developers now get 49% instead of 70% of US sales. The Spectator developer said the US is about half their revenue and called it "a powerful blow". — [DTF, Aug 2024](https://dtf.ru/gameindustry/2917376-steam-stal-vzimat-s-rossiiskih-razrabotchikov-nalog-v-razmere-30-tolko-ot-prodazh-v-ssha)
- W-8BEN line 9: claim treaty benefits for "the country where you claim to be a resident for income tax treaty purposes". A person is a resident "if the person is a resident of that country under the terms of the treaty". Line 6a: the foreign TIN comes from your jurisdiction of tax residence (for a Russian individual, the ИНН). — [IRS Instructions for Form W-8BEN](https://www.irs.gov/instructions/iw8ben)
- For comparison, copyright-royalty treaty rates in a university chart: Kazakhstan 10%; Armenia, Kyrgyzstan, Georgia, Uzbekistan and Belarus 0% under the old US–USSR treaty; Cyprus 0%. The same chart **still lists Russia at 0%, which is outdated** since Aug 2024 (contradicted by EY above). — [UCLA treaty chart, royalties](https://cru.ucla.edu/alien-tax-treaty-chart-royalties); contradicted for Russia by [EY](https://www.ey.com/en_gl/technical/tax-alerts/us-treasury-suspends-key-provisions-of-us-russia-tax-treaty-and-)
- [OLD, Jul 2024, pre-suspension] Slava Gris advised careful W-8BEN completion to get 0% instead of the default 30%. This was correct before 16 Aug 2024 and **no longer applies to Russian residents**. Passport-based verification was faster (days, not weeks). — [WN Hub, Slava Gris, 12 Jul 2024](https://wnhub.io/ru/news/investment/item-44141)

### Inferences
- Example: on a $10 US sale (ignoring US sales tax and refunds) the developer gets $7.00 × 0.70 = $4.90. On a non-US sale it is $7.00. For a strategy game the US is often the largest single market, so blended net revenue may fall roughly 8–15% compared with a treaty-country developer, depending on the US share.
- Only a real change of tax residence to a treaty country (for example by spending 183+ days there and becoming resident under that treaty), or selling through a foreign entity or publisher resident in a treaty country, brings the US rate down. Either step changes Russian residency and currency-control status and needs professional advice.

### Gaps
- Whether the 30% US withholding can be credited against Russian НДФЛ/УСН/НПД while the treaty is suspended was not found. Russian foreign-tax-credit rules usually depend on a treaty being in force. **Consult a Russian tax adviser.**
- Valve's own characterisation of the payment type (royalty vs business income) and the 1042-S income code could not be confirmed from Valve's pages.

---

## 4. Russian side: legal form (НПД / ИП on УСН / plain individual), receiving Valve income, currency control, tax rates

### Takeaway
There are three practical forms: a **plain individual** (progressive НДФЛ from 13%, self-declared), **самозанятый/НПД** (6% on income from organisations, ₽2.4m annual cap; foreign-company income is possible but licensing income is a grey area), or **ИП on УСН "доходы" 6%** (cleanest for licensing and royalty-type income above the НПД cap, but ИП currency control is strict). Any foreign account must be reported to the ФНС within one month of opening, plus an annual cash-flow report by 1 June unless an exemption applies. Since 2025 the ФНС has stepped up currency-control enforcement. **A professional should confirm the setup before the first payout.**

### Cited Findings
**НПД (самозанятый)**
- Rates: **4%** on income from individuals and **6%** on income from organisations and ИП. Cap: **₽2.4m per calendar year**. A 10-year experiment with rates fixed for the period. Barred: resellers, people with employees, agents and commission agents, sellers of excisable goods. — [ФНС: npd.nalog.ru](https://npd.nalog.ru/)
- A самозанятый can receive income from abroad in foreign currency. The receipt is issued in rubles at the Central Bank rate on the date of receipt. Mandatory sale of FX proceeds was abolished on 10 Jun 2022. A later FX-conversion gain is taxed as НДФЛ at 13% via 3-НДФЛ, filed by 30 Apr of the following year. (The source's payment date of "15 June" differs from the usual 15 July deadline; verify.) — [Т—Ж, 26 Dec 2022 [OLD but rule-based]](https://t-j.ru/npd-currency/)
- [snippet] Bank apps (e.g., T-Bank) let a самозанятый issue a receipt to a foreign organisation. — [T-Bank Secrets (snippet)](https://secrets.tbank.ru/voprosy-otvety/valyuta-samozanyatye/)
- "Юрлицам, ИП и самозанятым в России запрещается принимать оплату с зарубежных электронных кошельков." — [Habr, 9 Feb 2026](https://habr.com/en/articles/994432)
- [snippet] The Ministry of Finance treats transfer of software rights under a licence agreement as distinct from software-development services. An ИП rights holder cannot apply the patent system (ПСН) to such licensing. This is relevant because the Steam Distribution Agreement is a licence. — [buh.ru (snippet)](https://buh.ru/news/uchet_nalogi/119425/)

**ИП on УСН**
- УСН rates in 2026: **6% of income** or 15% of income minus expenses. The limit to stay on УСН is ₽490.5m in 2026. VAT exemption applies up to **₽20m of income (inclusive)**; above that the УСН VAT options are 5% or 7%. — [Saby, 9 Jul 2026](https://saby.ru/articles/accounting/limity_i_stavki_usn)
- The standard VAT rate is 22% from 1 Jan 2026. The УСН VAT-exemption threshold is ₽20m in 2026, falling to ₽15m and then ₽10m. — [Российская газета, 7/14 Apr 2026](https://rg.ru/post/nalogi-kakie-izmeneniia-vstupili-v-silu-i-kak-oni-povliiaiut-na-grazhdan-i-biznes.html). Saby says the reductions are scheduled for 2029–2031 instead, so the sources **conflict on timing**. — [Saby, 9 Jul 2026](https://saby.ru/articles/accounting/limity_i_stavki_usn)
- ИП and ООО currency control (2026): a Russian-resident ИП is subject to currency control even with a foreign registration. Export contracts above **₽10m** must be registered with an authorised bank within 30 working days of crossing the threshold. Fines run **20–40%** of the amount for non-registration or unreported foreign payment systems, and 5–30% for funds not repatriated (45-day grace). Contracts need clear amounts, payment deadlines and completion terms, or the ФНС may treat the operations as unlawful. Business foreign accounts are reported quarterly; personal ones annually by 1 June. Foreign payment systems (Payoneer, Wise, Deel) are reportable above ₽600k a year. — [Habr, Fedor Andreev, 9 Apr 2026](https://habr.com/ru/articles/956854)
- [snippet] For an incoming FX payment to an ИП's Russian account, the bank must receive the currency-operations certificate and supporting documents (contract, invoice/act) within 15 working days. In 2025 the ФНС "заметно усилила проверки валютных операций", and the trend continued into 2026. — [search snippet, Tochka/related](https://allo.tochka.com/card/new-currency-rules)

**Plain individual (no status)**
- НДФЛ 2026 (progressive): 13% up to ₽2.4m, 15% for ₽2.4–5m, 18% for ₽5–20m, 20% for ₽20–50m, 22% above ₽50m. — [Российская газета, Apr 2026](https://rg.ru/post/nalogi-kakie-izmeneniia-vstupili-v-silu-i-kak-oni-povliiaiut-na-grazhdan-i-biznes.html)

**Foreign accounts held by individual residents**
- A resident must notify the tax office of opening or closing a foreign account "не позднее одного месяца". The form was approved by ФНС order of 26.04.2024 № СД-7-14/349@ and can be filed online. Exempt: people abroad 183+ days a year and tax non-residents. — [ФНС: уведомление об открытии счёта](https://www.nalog.gov.ru/rn11/related_activities/accounting/recording_individuals/notice/)
- Fines: ₽1,000–1,500 for late notice, up to ₽5,000 for no notice (КоАП 15.25). The annual cash-flow report (ОДДС) is due by 1 June. — [vc.ru, 21 Jun 2026](https://vc.ru/money/2989065-zarubezhnye-scheta-dlya-rossiyan). Slava Gris (2024) quoted ₽4,000 per account for missing notice. — [WN Hub, Jul 2024](https://wnhub.io/ru/news/investment/item-44141)
- [snippet] No ОДДС is needed for accounts in EAEU states (Kazakhstan, Kyrgyzstan, Armenia) or automatic-exchange countries if credits and debits for the year are ≤ ₽600k. — [nalog-nalog.ru (snippet)](https://nalog-nalog.ru/valyutnye_operacii/kuda_i_kogda_podavat_uvedomlenie_o_zarubezhnyh_schetah/)
- After money reaches a transit account, the bank's currency control asks for the contract and act before crediting it. The developer then pays tax under their regime, or declares the income and pays НДФЛ. — [search summary of vc.ru / Easy Payments](https://vc.ru/services/1804828-kak-razrabotchikam-iz-rossii-i-belarusi-poluchat-ot-steam-vyplaty-za-prodazhu-igr)

### Inferences
- **Under ₽2.4m a year (most first releases):** НПД at 6% is the cheapest on paper. It is risky in two ways: no explicit ФНС ruling was found that Steam licence revenue qualifies, and receipts must be issued for each FX receipt at the CBR rate. Get written confirmation from a tax adviser, or check ФНС letters.
- **Above ₽2.4m, or for certainty:** ИП on УСН 6% fits royalty or licence income and allows the 30% US withholding question to be handled in one place. In exchange the ИП takes on full currency control: a contract (the Steam Distribution Agreement, accepted online) shown to the bank, acts/reports, deadlines, and repatriation rules. Valve does not issue Russian-style acts, so the monthly Steam payment report is the de-facto supporting document. Whether a given bank accepts it is a known friction point.
- **Plain individual with a third-country account:** the simplest to set up (notify the ФНС, file ОДДС or claim the exemption, 3-НДФЛ yearly at 13–15%). The risk is whether royalty-type income may legally be credited to a personal foreign account in that country. This depends on Art. 12 of 173-ФЗ and whether the country is EAEU or automatic-exchange. Not verified, so consult.
- Whatever the form, keep Steam monthly reports, bank statements and the Steamworks agreement on file as the evidence chain for the ФНС.

### Gaps
- No ФНС or Минфин letter found that explicitly allows or bars НПД for income from distributing one's own game through a foreign storefront under a licence.
- No authoritative 2026 text found on which credits Art. 12 of 173-ФЗ allows into a resident individual's account in non-EAEU / non-CRS countries (e.g., UAE, Serbia, Turkey).
- Fixed 2026 ИП insurance contributions were not found.
- VAT on licence income from a foreign licensee (likely outside Russian VAT by place-of-supply rules, but not verified).
- **All of Q4 needs a Russian tax and currency-control professional before the paid release.**

---

## 5. itch.io payouts for Russian developers, and whether this matters for a free build

### Takeaway
itch.io pays out only through **PayPal or Payoneer** ("Collected by itch.io") or lets creators connect **PayPal or Stripe** directly. PayPal left Russia in March 2022 and Payoneer closed Russian accounts in December 2022, so a Russia-resident developer cannot withdraw itch.io revenue without a non-Russian account. **A free build needs no payment setup at all**, so for the planned free itch.io build this does not matter.

### Cited Findings
- "Collected by itch.io": payouts by PayPal or Payoneer, USD only, a small minimum balance, revenue available 7 days after purchase, and a tax interview that must reach "Validated". "Direct to you": the creator connects PayPal or Stripe. The docs mention no payment setup for free projects. — [itch.io docs: Payments](https://itch.io/docs/creators/payments)
- PayPal suspended services in Russia on 5–6 March 2022. — [Axios, 5 Mar 2022](https://www.axios.com/2022/03/05/paypal-suspends-business-russia-ukraine-invasion); [Engadget](https://www.engadget.com/paypal-suspends-services-russia-193756988.html)
- Payoneer closed accounts of Russians on 16 December 2022. PayPal funds cannot be withdrawn to a Russian bank, which makes itch.io payouts "extremely difficult". — [vc.ru: оплата itch.io из России](https://vc.ru/services/2118615-oplata-itch-io-iz-rossii); see also a Russian developer's devlog on these problems: [itch.io devlog (search summary)](https://akiyamy.itch.io/locked-up-vn/devlog/357571/about-the-problems-encountered-and-the-future)

### Inferences
- For the free itch.io alpha, set the price to free with no "pay what you want" or donation option, and skip the itch tax interview. If donations are enabled later, the money would sit in itch.io until a non-Russian PayPal or Payoneer account exists. That account would also need to be in the developer's name and reported to the ФНС (Q4).
- Licence knock-on: a free itch build is "free", but it promotes a paid Steam game. See Q8 for why the free/non-commercial art packs should still be upgraded before the itch build goes public alongside a Steam page.

### Gaps
- Stripe's current country list was not checked (it is widely understood not to support Russia, but this was not verified here).

---

## 6. Russian players buying on Steam in 2025–2026: payment methods, RUB pricing, blocking risk, whether Russian sales pay out normally

### Takeaway
Steam still has a Russian region priced in **RUB**. Players cannot pay directly (no Visa, Mastercard or Mir since 2022). They top up wallets through login-based intermediaries (≈5–8% fee), gift cards and marketplaces (5–18%), or foreign cards. Purchases are ordinary Steam sales, and Valve takes its 30% as usual. No Roskomnadzor block exists as of early October 2026, though outages and regulatory threats recur.

### Cited Findings
- **RUB is a live Steam currency**: "RUB – Yes – Russian Rouble, reported in kopeks" (as of 5 Oct 2026). Where a native currency isn't available, Steam uses USD or regional USD tiers (USD_CIS etc.). — [Steamworks: Supported currencies](https://partner.steamgames.com/doc/store/pricing/currencies)
- [OLD, Oct 2022] Valve raised its recommended RUB prices: $60 → ₽1,900 (was ₽1,085), and about ₽500 now recommended for a $12.99 game (formerly for $30). — [WN Hub, 25 Oct 2022](https://wnhub.io/news/stores-and-publishing/item-21377)
- [Apr 2026] Payment methods and fees: login-based top-up services 5–8% (3–7 min); gift cards; marketplace resellers (ggsel, plati) 5–18%; foreign cards from Kazakhstan, Georgia, Turkey or Armenia (which may need a region change). "Ни Visa, ни MasterCard, ни МИР" are accepted directly since 2022. Ban risk for using legitimate top-up services is described as minimal. — [vc.ru, 16 Apr 2026](https://vc.ru/services/2872114-sposoby-popolneniya-steam-v-rossii)
- [Jun 2026] Top-up services take rubles via SBP QR and credit the wallet instantly. "Пополнение кошелька Steam напрямую рублями, как раньше — невозможно." — [DTF, 17 Jun 2026](https://dtf.ru/steam/5133756-sposoby-popolneniya-steam-v-rossii)
- [Apr 2026] NGL.media investigation: Russians spent about **$546m** on Steam and intermediaries took over **$31m**. The main channels are FunPay (Georgia/Kazakhstan/UK/Seychelles entities, routed via the UAE) and Plati.Market (Seychelles). Example: of $45.5m in Russian sales of *Monster Hunter Wilds*, "Steam received USD 13.6 mln (30% of the total sale proceeds)". Valve did not comment. NGL frames the $546m as spending **since the full-scale war began**, while Mezha reports it as **annual**, so **the sources conflict**. — [NGL.media, 8 Apr 2026](https://ngl.media/?p=51412); [Mezha](https://mezha.ua/en/news/rosiyani-vitrachayut-31-mln-shchorichno-na-obhid-obmezhen-v-steam-310097/)
- **Blocking risk:** on 2 Jun 2026 Roskomnadzor denied blocking Steam after outages on 1 Jun. — [Ведомости, 2 Jun 2026](https://www.vedomosti.ru/technology/news/2026/06/02/1202239-oproverg-blokirovku-steam). In Feb 2025 RKN listed one Steam Community page and delisted it after removal. Steam reportedly removed about 250 prohibited items. Deputy Gorelin: Steam "demonstrates readiness to fulfill RKN prescriptions". Instability in early Oct 2026 appeared ISP-local rather than a block. — [Metaratings, Oct 2026](https://cybersport.metaratings.ru/articles/chto-proishodit-so-steam-segodnya/)
- [snippet, low-confidence blog] Since early 2026 RKN has sued at least seven foreign game companies (EA, Take-Two, others) over data localisation. Valve could face slowdown or blocking if it does not comply. — [SecurityLab blog (snippet)](https://www.securitylab.ru/blog/personal/paragraph/361877.php)
- [OLD context] Since 2015 Valve region-locks gifts and trades from cheaper regions such as Russia into pricier ones. — [PC Gamer](https://www.pcgamer.com/uk/valve-restricts-steam-gifting-and-trading-between-regions)

### Inferences
- Sales to Russian-region accounts are wallet purchases like any other. The NGL example (Valve's 30% cut of Russian *MH Wilds* sales) implies the remaining 70% flows to the publisher normally. For a Russia-based developer those sales are paid in the same monthly USD payout, under the same constraints as Q2. They carry no US withholding because they are not US-source.
- RUB pricing: start from Valve's current pricing tool recommendation. The 2022 numbers show RUB is a low-price region (roughly 35–40% of USD). Expect a Russian-speaking audience to be a large share of units but a small share of revenue.
- A Steam block in Russia would cut the domestic audience and the local Russian-language community. So far the record (2022–Oct 2026) is outages and denials, not a block.

### Gaps
- No primary source found for current (2025–2026) Valve RUB recommendations, or for Russia's share of Steam revenue or users.
- No explicit Valve statement that Russian-region sales are paid to developers as usual. That conclusion rests on NGL's revenue split and on Valve's unchanged wallet model.

---

## 7. Steam AI disclosure: current Content Survey wording (Jan 2026 update), what must be disclosed, code assistants, store-page display, player sentiment

### Takeaway
Since January 2024 Steam has required developers to disclose generative-AI content (pre-generated or live-generated) in the Content Survey, and shows the disclosure on the store page. On about **16–17 January 2026** Valve reworded the survey: "efficiency gains" from AI tools in development (e.g., **code assistants**) are "not the focus". What must be disclosed is AI-made content that "ships with your game, and is consumed by players", including store-page, community and marketing assets, and explicitly **localization**. Players are split: in July 2026 about 8% of core Steam fans said they refuse to buy AI-disclosed games and 31% view disclosure negatively. 56% still think code helpers should be disclosed.

### Cited Findings
**Original policy (10 Jan 2024)**
- Pre-Generated: "Any kind of content (art/code/sound/etc) created with the help of AI tools during development." Live-Generated: "Any kind of content created with the help of AI tools while the game is running." Live-generated content also requires telling Valve the guardrails against illegal content. Much of the disclosure is shown on the store page. Players got a way to report illegal live-generated AI content. Valve does not publish live-generated AI adult sexual content. — [The Decoder, 10 Jan 2024](https://the-decoder.com/valve-tightens-review-process-for-games-with-generative-ai-content-on-steam/)
- "Under the Steam Distribution Agreement, you promise Valve that your game will not include illegal or infringing content, and that your game will be consistent with your marketing materials." In review, Valve checks AI output against those promises like any other content. — [The Decoder](https://the-decoder.com/valve-tightens-review-process-for-games-with-generative-ai-content-on-steam/); [WN Hub, Jan 2024](https://wnhub.io/news/stores-and-publishing/item-42710)

**January 2026 rewording**
- New form text: "efficiency gains through the use of these tools is not the focus of this section. Instead, it is concerned with the use of AI in creating content that ships with your game, and is consumed by players." It lists "artwork, sound, narrative, **localization**, etc." and acknowledges "many modern game development environments have AI-powered tools built into them". Disclosure covers in-game content, store pages, community assets and marketing materials. A separate checkbox asks whether the game will "generate content or code during gameplay", and the developer is responsible for safeguards. Spotted by GameDiscoverCo (Simon Carless). — [PCGamesN, Ken Allsop, 17 Jan 2026](https://www.pcgamesn.com/steam/new-gen-ai-disclosure-form)
- Yes/no question as reported: "Does this game use generative artificial intelligence to generate content for the game, either pre-rendered or live-generated? This includes the game itself, the store page, and any Steam community assets or marketing materials." Using AI "code helpers" needs no disclosure if no generative content reaches the final product. Developer reactions were positive: Sabin VanderLinden called it "a really thoughtful middle ground"; Samuel Cohen: "even Unreal Engine has its own AI assistant chat bot". — [GosuGamers](https://www.gosugamers.net/entertainment/news/77861-steam-revises-ai-policy-while-keeping-generative-content-disclosure-intact)
- Headline: "Steam updates AI disclosure form to specify that it's focused on AI-generated content that is 'consumed by players,' not efficiency tools used behind the scenes." Several summaries date the change to 16 Jan 2026. — [PC Gamer](https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/)
- [secondary blog, lower reliability] StraySpark (25 Mar 2026) describes a "Tier 1/Tier 2" structure and lists exempt tools (Copilot, Claude Code, Cursor, MCP servers, build tools, non-AI procedural generation). It claims "over 7,300 games" disclosed as of March 2026. The tier framing is the blog's own, not Valve's wording, and the count conflicts with Totally Human's July 2025 figure below. — [StraySpark](https://www.strayspark.studio/blog/steam-ai-disclosure-rules-2026-indie-developer-guide)

**Prevalence**
- Totally Human Media (Ichiro Lambe): 7,818 of about 114,126 Steam games (7%) carried AI disclosures as of 13 Jul 2025, about 20% of 2025 releases. Disclosures rose 800%, and 60% concern visual assets. — [VGC](https://www.videogameschronicle.com/news/steam-games-disclosing-generative-ai-use-are-up-800-this-year/); [WN Hub](https://wnhub.io/news/analytics/item-48275)
- "Nearly 20% of games in Steam's recent Next Fest event came with a generative AI warning." — [PC Guide, Jul 2026](https://www.pcguide.com/news/pc-gamers-remain-skeptical-of-steams-ai-disclaimers-poll-shows-many-believe-game-devs-are-hiding-it/)

**Sentiment**
- GameDiscoverCo Steam Fan Snapshot (~3,800 core Steam fans, 25 Jun–2 Jul 2026, published 7 Jul 2026). On AI-disclosed games: 43% no major issue, 26% neutral, 31% negative, including **8% who refuse to buy**. 44% read disclosures in detail, 45% glance, 11% ignore. Only 17% believe developers fully disclose audiovisual AI use. Should **coding helper tools** be disclosed? **56% yes**, 8% no, 37% neutral. The most-mentioned acceptable uses: code helpers (239 mentions), prototyping/placeholders (119), repetitive tasks (101). Typical view: "I'm OK with AI being used in games as long as there is human review." — [GameDiscoverCo newsletter, 7 Jul 2026](https://newsletter.gamediscover.co/p/what-do-steam-fans-really-think-about); [PC Guide](https://www.pcguide.com/news/pc-gamers-remain-skeptical-of-steams-ai-disclaimers-poll-shows-many-believe-game-devs-are-hiding-it/)
- [snippet] Developer survey (826 respondents): 88.4% think Valve should require stricter genAI disclosure, pushing back on the January 2026 narrowing. — [games.gg (snippet)](https://games.gg/news/game-devs-steam-ai-disclosure-survey/)
- Tim Sweeney (Epic) mocked mandatory AI labels in Nov 2025 ("shampoo" analogy), predicting they will become meaningless as AI spreads. Mike Bithell called genAI workflows "low-effort". — [LetsDataScience, 29 Nov 2025](https://letsdatascience.com/news/tim-sweeney-criticizes-steams-mandatory-ai-disclosure-policy-c0bb05d4)

### Inferences
- **Troll Strategy, code assistants only:** under the January 2026 wording, using AI coding assistants (and editor or MCP automation) is "efficiency" and needs no disclosure, provided **no AI-generated content ships or appears in marketing**.
- **Check these items before answering "No"**, because each would be AI content "consumed by players":
  1. **Localization:** the project keeps translation files in `tools/localization/translations`. If those were produced by an LLM or machine translation, the 2026 form names "localization" explicitly, so disclosure is likely needed.
  2. **AI 3D, 2D or audio generation:** e.g., meshes from an AI 3D generator, AI-made icons, AI voice, or AI-assisted capsule or trailer art.
  3. **AI text** in store copy or in-game narrative.
  
  The project's SFX come from a deterministic Python recipe (`assets/audio/build_sfx.py`), which is procedural, not generative AI.
- If something is disclosed, write a short, specific statement (e.g., "Some translations were machine-assisted and reviewed by a human"). This matches the conditional acceptance seen in the survey ("as long as there is human review"). Over half of core fans want even code assistants disclosed, so some developers add a voluntary sentence to the store page or FAQ. That is optional, and it carries some risk (8% hard refusal).
- The answer can be changed later in Steamworks. Keep a short internal log of AI use per asset so the survey answer stays accurate.

### Gaps
- Valve's own January 2026 post or the full live survey text was not retrieved directly (Steam pages render via JavaScript). The wording above is quoted by PCGamesN and GosuGamers from GameDiscoverCo screenshots.
- The exact store-page block title and layout for AI disclosures (2026) was not verified.
- No data specific to strategy-genre players.

---

## 8. Third-party asset packs: Steam's rules, licence needs for a free demo vs paid release, "asset flip" perception and mitigation

### Takeaway
Steam has no rule against store-bought assets. Under the Distribution Agreement the developer promises the game contains **no infringing content** and matches its marketing. Valve has removed bulk "nearly-identical" asset flips (2017) but otherwise allows almost anything. Licence compliance is the developer's job. **Non-commercial free packs must be upgraded before a Coming Soon page, a demo or a Next Fest entry for a paid game**, since all of these market a commercial product. For Troll Strategy that is cheap: Minifantasy commercial from $2.99 per pack, Raven Fantasy Icons premium from $35. Players judge "asset flip" mostly by gameplay and coherent art direction, not by asset origin alone.

### Cited Findings
**Steam's rules**
- Developers promise "your game will not include illegal or infringing content, and that your game will be consistent with your marketing materials." Valve checks this in pre-release review. — [The Decoder, Jan 2024](https://the-decoder.com/valve-tightens-review-process-for-games-with-generative-ai-content-on-steam/)
- [OLD, Sep 2017] Valve removed 173 asset-flip titles from Silicon Echo and related accounts for "mass-shipping nearly-identical products on Steam that were impacting the store's functionality". In 2018 Valve moved to "allow everything" except "obvious troll" games. *Getting Over It* uses purchased assets and is not considered a flip. — [Wikipedia: Asset flip](https://en.wikipedia.org/wiki/Asset_flip); [TweakTown](https://www.tweaktown.com/news/59291/valve-remove-173-games-steam-due-asset-flipping/index.html)

**Licences**
- Unity Asset Store EULA allows you to "incorporate the Asset … into an electronic application … as an embedded component" and to "monetize the Asset within … a Licensed Product". It bars cost-sharing so third parties can use an asset, and reselling or commercialising assets except as permitted. — [Unity Asset Store Terms](https://unity.com/legal/as-terms)
- **Minifantasy** (Krishna Palacio), free licence: **non-commercial projects only**; must credit "Krishna Palacio" and send the author a link on completion. The commercial licence costs **$2.99+** per pack, allows "non-commercial or commercial" use, and keeps the credit/link and no-redistribution terms. — [Minifantasy Creatures page](https://krishna-palacio.itch.io/minifantasy-creatures)
- **Raven Fantasy Icons** (Clockwork Raven), free version: "Personal use includes any projects or game released for free with no microtransactions and/or paid advertisement/ad." The premium version (**$35+**) "can be used in any project, even commercial". Attribution is "not necessary but welcome". — [Raven Fantasy Icons page](https://clockworkraven.itch.io/raven-fantasy-icons)
- Local project fact: `assets/licenses.json` sets `"commercialReleaseBlocked": true` because the Minifantasy (Creatures, Forgotten Plains) and Raven Fantasy Icons free archives lack commercial-licence proof. An entry `user-provided-sources` reads "License not supplied". Music and SFX entries (xDeviruchi, YannZ CC BY 4.0, Sonniss GDC 2026, generated SFX) allow commercial use, with attribution where listed. — local file `F:/ClaudeGames/trollstrategy/assets/licenses.json`

**Perception**
- Asset flips are "viewed by gamers as uncreative" shovelware. — [Wikipedia: Asset flip](https://en.wikipedia.org/wiki/Asset_flip)
- [Sep 2023] Unity forum: a warning that recognisable Asset Store art will be "seen as an asset flip", rebutted with counterexamples: *Perfect Heist 2* (Very Positive, 1,700+ reviews), *Polygon* (Mostly Positive, 23,700+), *BattleBit Remastered*. "If gameplay is interesting, or somehow original, no one will care about which assets are used." — [Unity Discussions](https://discussions.unity.com/t/dont-buy-recognizable-asset-store-assets-it-will-be-seen-as-an-asset-flip/928444)

### Inferences
- **Free Steam demo, Coming Soon page, trailer, Next Fest:** all of these promote a paid product. The Minifantasy free licence is "non-commercial projects", and the project is commercial even when one build is free. Raven's free-use definition (a game "released for free with no microtransactions") arguably covers a standalone free game, but not the demo or marketing of a paid one. **Buy the commercial licences before any public Steam asset (screenshots and trailer included) shows these sprites or icons**, and record the receipts in `licenses.json`. Cost: about $2.99 × 2 + $35 ≈ $41 minimum.
- **Free itch.io build:** probably within Raven's free terms on its own, but Minifantasy's "non-commercial project" language is doubtful once a paid Steam version exists. Upgrade first, since the cost is trivial.
- Resolve `user-provided-sources` ("License not supplied") before the commercial gate is lifted.
- **Asset-flip mitigation** (from general practice; no 2025–2026 primary source fetched): one coherent art direction (palette, lighting, shaders, UI) so mixed packs read as one game; distinctive custom key art and capsule (the most visible asset); show the game's unique systems in the first seconds of the trailer and screenshots; avoid the most recognisable "hero" assets of popular packs in marketing; credit asset authors openly in-game and on the store page (Minifantasy requires it). Troll Strategy mixes a Blender-built island kit, custom UI Toolkit styling and small pixel packs, so recognisability risk is mainly in the Minifantasy creature and tile sprites and the Raven icons.

### Gaps
- No current (2025–2026) data on how often Steam reviews or curators flag "asset flip" for games built on small itch.io pixel packs like Minifantasy.
- No official Valve guidance specific to third-party asset use in store-page media beyond the general "not infringing / consistent with marketing" promise.
- The Creative Commons NC definition (whether a free demo of a paid game is "commercial") was not fetched. The inference above relies on the packs' own wording.
