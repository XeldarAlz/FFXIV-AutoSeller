<p align="center">
  <img src="AutoSeller/Images/Icon.png" width="180" alt="Auto Seller icon" />
</p>

<h1 align="center">Auto Seller</h1>

<p align="center">
  <a href="https://discord.gg/hppkAvdBEE"><img alt="Discord" src="https://img.shields.io/badge/Discord-join-5865F2?style=flat-square&logo=discord&logoColor=white"></a>
  <a href="https://github.com/XeldarAlz/FFXIV-AutoSeller/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/XeldarAlz/FFXIV-AutoSeller?style=flat-square&color=blue"></a>
  <a href="https://github.com/XeldarAlz/FFXIV-AutoSeller/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/XeldarAlz/FFXIV-AutoSeller/total?style=flat-square&color=blue&cacheSeconds=300"></a>
  <a href="https://github.com/XeldarAlz/FFXIV-AutoSeller/actions/workflows/release.yml"><img alt="Build" src="https://img.shields.io/github/actions/workflow/status/XeldarAlz/FFXIV-AutoSeller/release.yml?style=flat-square"></a>
  <a href="LICENSE.md"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0--or--later-blue?style=flat-square"></a>
</p>

<p align="center">
  <em>Your retainer listings, sold for you. Built on Dalamud.</em>
</p>

---

## What it does

Keeping your retainers' listings at a price that sells means visiting a bell, comparing prices and retyping them, retainer by retainer, every time someone undercuts you. Auto Seller shows your retainers' listings and everything in your bags in one list, and for each row you choose what happens: keep a listing the cheapest, give it your own price or leave it as it is, or sell an item on the market board or to an NPC. **Run now** goes to a summoning bell and does all of it in one trip; **Autopilot** does it on its own every hour, 2 hours or 4 hours while you are idle, or after each AutoRetainer venture. Every change is checked and your sales are recorded.

Nothing new is listed or vendored until you tick it, no price goes below its floor, and a run stops at the limits you set for it. The listings you already have up are kept the cheapest out of the box; turn that off under Settings, Pricing.

## Features

- **Sell page**: one board with two tabs. **Your listings** shows every listing of the character you play and says how they stand ("1 will be undercut · 19 are already the cheapest"); a picker beside the search shows any other character's listings, read only, with the lowest seller, how long each has been listed and its state. **Sell items** lists everything you carry that can be sold. The footer holds the switches that turn a retainer on or off and what your items could fetch at today's prices, and a single line under the tabs says when something needs you. Each row has one-click buttons:
  - a listing: **Undercut**, on from the start, keeps it just under the cheapest seller, and **My price** lets you type your own; with both off it is left as it is. The row shows its price now, the price it will get and why ("Undercuts the lowest seller", "Already lowest", "1 bait listing ignored", "Below floor, kept", "Price war, paused"). A drop over the step limit waits for your approval.
  - an item you carry: **Market board** or **NPC**; click again to stop. It lists all you hold unless you set how many, at the best price (just under the cheapest seller, never below its floor) unless you type your own, on any retainer unless you pick one. A chosen item stays chosen, so what you craft or gather keeps selling, and an item no retainer has room for says so, naming a retainer you switched off.
- **Autopilot**: one switch in the bar at the bottom of the Sell page, beside Sell now or Undercut now. Every hour, 2 hours or 4 hours it goes to the bell on its own, only once you have stood still for 2 minutes outside combat, duties and cutscenes, says so in chat, and stops when you press a movement key. With AutoRetainer it can instead run after each venture.
- **Sell now and Undercut now**: the button in the bar at the bottom follows the tab you are on. On **Sell items** it is **Sell now**: it lists and vendors only what you picked and visits only the retainers those items go to. On **Your listings** it is **Undercut now**: it only checks and undercuts your listings. It says what it will do ("sell 2 items", "undercut 1 listing · check 18"), and hovering it lists every step. Autopilot always does both. While a run works, that bar shows its progress with Pause and Stop, and each row shows what happened to it.
- **Guarded pricing**: Undercut or Match mode against listings of the same quality only. Your own retainers and your alts are never undercut, bait listings are ignored, the reference price comes from real sales, the floor never goes below vendor value, and a maximum drop per reprice, a cooldown and price-war detection keep a race to the bottom in check.
- **Rules (advanced)**: not needed for the above, they give finer control. **Items** lists every item you gave an order, with **Market board**, **NPC** or **Never sell** on each row; open a market item for its floor, ceiling, fixed price, lot size, bag keep count, listings to keep up, preferred retainer and quality. **Categories** holds category rules with a minimum value and the junk rule, which stays off until you turn it on. **Blocked** shows which matched items in your bags wait and why. Items set to Never sell, in a gear set, equipped or protected in AutoRetainer are never sold. Rules can be exported and imported as JSON, without any retainer names or ids.
- **Sources**: your bags and crystals, and when you opt in, a retainer's own inventory (listed through that retainer's own sell list), the saddlebag (stacks planned for a retainer are moved to your bags before the bell, while they have room) and the armoury chest (never items in a gear set).
- **Ways to run**: Run now and Autopilot, which go to a nearby summoning bell, the bell in your inn room or a bell you picked; Autopilot's AutoRetainer post-process after each venture; or Assist, which compares prices when you open a sell window by hand, fills the suggested price, never below the floor, shows the reason, the floor and the reference on a card beside the window and leaves Confirm to you. Hold the skip key (Shift by default) as the window opens to skip it.
- **In-game buttons**: "Run all retainers" on the retainer list and "Reprice this retainer" on a retainer's sell list, so a run can start without opening the window. "Reprice this retainer" works only the open retainer and leaves its sell list open. With AutoRetainer, a Sell now button on its retainer list sends every retainer through Auto Seller.
- **Undercut watch**: your listings are checked against Universalis on a timer, with no game request, and a run can visit only the retainers that need it: those with an undercut listing, a lot to list or a listing to return.
- **Sales**: a ledger built from sale notices, the retainer sale history and listing changes, with daily, 7-day and 30-day totals beside a daily chart, grouped by item, retainer or character or newest first, each sale marked confirmed or inferred (or uncertain when the retainer's gil did not grow), hints when an item sells fast or sits unsold, and a CSV export without character, retainer or buyer names.
- **Run controls**: Pause, Resume and Stop in the dock at the bottom of the window or as commands, auto-pause while you are in a duty, and hold new moves while you type in chat.
- **Humanizer**: short random delays before the character moves off or casts a teleport.
- **GM alert**: ends a run when a GM is near, with an optional toast, chat line, beeps, custom commands, or closing the game.
- **Runs and console**: your past runs with their outcome on the Sales page's Runs tab, and every step logged on the Console page, ready to copy into a bug report.

## How a run works

1. On the Sell page, choose what happens to each row (or write rules), then press **Sell now** or **Undercut now** (or type `/asell start` to do both), or turn on **Autopilot**. The run keeps to the rules, floors, caps and edits you saw at that moment.
2. When the plan lists saddlebag stacks, it first moves them to your bags while they have room. It then walks to a nearby summoning bell, or travels to the inn of your Grand Company's city and uses the bell in your room. Under Settings, General, you can make it always use your inn room or a bell you pick: stand at any bell, in a city for example, and press Use the bell I'm standing at; runs teleport there when it is far. AutoRetainer, when installed, is held back for the run and restored afterwards; while AutoRetainer is busy, Run now stays off.
3. For each enabled retainer it reads the sale history and the listings, compares prices, reprices what is due, lists the planned lots, returns a listing that stayed below its floor for three days when you chose that, and, when your rules allow it, has the retainer sell junk to a vendor. When that retainer sells from its own inventory, it then lists those lots through the retainer's own sell list.
4. Every price is checked in the sell window before Confirm and read back from the retainer's listings after it. A write it cannot confirm stops the run and asks you to check.
5. It closes the retainer windows and records the run and the sales on the Sales page.

As an AutoRetainer post-process it skips the walk, works on the one retainer AutoRetainer hands over after its venture, and always hands it back at the retainer's menu. Turn it on under Settings, AutoRetainer, which also shows what it is doing right now. A Stop while it works keeps the next retainers with AutoRetainer until you leave the bell.

## Safety

- **Autopilot only while you are idle.** It starts only after you have stood still for 2 minutes outside combat, duties, cutscenes and chat typing, says so in chat, and stops after the item it is on as soon as you press a movement key.
- **Limits per run.** A run stops repricing and listing at the reprices and new listings per run under Settings, Pricing, Limits per run, and stops vendoring at the vendored value per run of your vendor rules.
- **Nothing new sold without your say.** Only items you tick, or that your sell list or category rules allow, are listed, and only items you mark for the NPC, or junk once you turn that on, are vendored. Listings already up are undercut unless you turn Undercut all my listings off.
- **Floors and step limit.** No price is written below the item's floor, and no reprice drops a price further than the step limit without your approval.
- **Never against yourself.** Your own listings are never undercut, and neither are your alts' listings when AutoRetainer knows them.
- **The player's path.** Prices go through the game's sell window, the same one you use. There are no direct market writes.
- **Checked twice.** Every write is checked before Confirm and read back after it, and a write whose listings cannot be read first is not made. A write it cannot confirm stops the run in a "needs you" state, nothing resumes on its own, and Run now and Autopilot wait until you answer its question on the Sell page.
- **Menus by text.** Retainer menu entries are chosen by their text from the game data, never by their place in the menu, and there is no blanket Yes to prompts.
- **Hands back.** As an AutoRetainer post-process it hands the retainer back on success, failure, Stop, logout, a character change and unload, and when its time for that retainer runs out.
- **What it never does**: list an item you did not tick or allow by a rule, go below a floor, vendor anything you did not mark for the NPC or call junk, or recover on its own from a write it could not confirm. Its only network calls go to Universalis for price data.

## Install

In-game: `/xlsettings` → **Experimental** → paste into **Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/XeldarAlz/DalamudPlugins/main/repo.json
```

Tick **Enabled**, click **+**, then **Save and Close**. Open `/xlplugins` → **All Plugins**, search for **Auto Seller**, and install.

## Requirements

- **vnavmesh** for walking to a summoning bell. Run now and Autopilot stay off until it is installed and loaded.
- **AutoRetainer** to run Auto Seller after ventures, for its protect list and to know your alts. It is optional: without it, Run now, the Autopilot timer and Assist still work.
- **Marketbuddy** is not needed. When it is installed, Auto Seller locks it while it types a price, so only one of them fills the sell window.
- **At least one retainer**, with runs started from your home world.

Open `/asell deps` after install to see the helpers and one-click each missing one.

## Commands

| Command | Action |
|---|---|
| `/asell` | Toggle the main window |
| `/seller` | Alias for `/asell` |
| `/asell config` | Open the Settings page |
| `/asell stats` | Open the Runs tab of the Sales page (`/asell history` works too) |
| `/asell sales` | Open the Sales page |
| `/asell deps` | Open the Plugins page (`/asell dependencies` works too) |
| `/asell log` | Open the Console page |
| `/asell changelog` | Open the Changelog page |
| `/asell about` | Open the About page |
| `/asell start` | Open the Sell page and start a run of the current plan; when it cannot start, the chat says why |
| `/asell stop` | Stop the run |
| `/asell pause` | Pause or resume the current run (`/asell resume` works too) |
| `/asell help` | List these commands in the chat |
| `/asell target` | Log the targeted NPC's BaseId (debug helper) |
| `/asell look <item id>` | Compare market prices for one item once and write the listings, sale history, reference and floor to the Console (debug helper; it never lists or reprices). With the default send path it needs that item's sell window open at a retainer; with `proxy` it needs a summoning bell or an open market board |
| `/asell mbstatus` | Write the market checks, the search state, the age of the Universalis reference data and, with a retainer's sell list open, that retainer's listings to the Console (debug helper) |
| `/asell sendpath compare\|proxy` | Send `/asell look` searches through the sell window's Compare prices button (`compare`, the default) or the item search proxy (`proxy`); runs and Assist always use the sell window. Without an argument it says which one is in use (debug helper) |
| `/asell historypath window\|packet` | Read a retainer's sale history from its Sale History window (`window`, the default) or from the data the game receives for it (`packet`, which reads the window until its handler is found in game); without an argument it says which one is in use (debug helper) |

## Known limits

- **Home world, one character at a time.** Runs work for the current character on its home world. Other characters sell through AutoRetainer's multi mode, one after another. Travelling home from another world, and house and Free Company bells, are not part of 1.0.
- **No entrusting.** Gil and items are never entrusted to or withdrawn from retainers; AutoRetainer's entrust plans and gil withdrawal already do that.
- **Sales inform, they do not steer.** The Sales page shows hints such as "sold in under an hour, consider raising"; prices do not change on their own because of them.
- **Equipped gear is never a source.**
- **Selling only.** Desynthesis, Grand Company turn-ins, coffers and venture rewards are left to the plugins that already do them, such as AutoRetainer, Deliveroo and Auto GC Supply.
- **No direct market writes.** Some repricers write prices straight into the game's memory. Auto Seller never does: every price goes through the sell window, so it can be checked before Confirm and read back after.
- **No undercut alerts.** There is no chat, toast or Discord alert when a listing is undercut yet; Autopilot reprices it at its next run.
- **Fixed pacing.** Market searches and window steps keep a fixed safe pace with random jitter; only the delays before moving off and casting a teleport can be changed, under Settings, Humanizer.
- **Prices are estimates.** Universalis only updates when someone searches the board, so the undercut watch shows the age of its data, and a run reads the live board before it writes.
- **Not yet run in game.** No part of 1.0.0.0 has run on a live client yet. The writes (typing the price and quantity in the sell window, the Adjust Price, Return to Retainer and Have Retainer Sell Items menus, saddlebag moves and Marketbuddy's lock), the reads (the Sale History window's layout and prices, sale notice values, gear set membership, which gil a vendor sale lands in, how soon listings fill in) and the AutoRetainer post-process over real ventures follow the game data and other tools so far. Each step logs what it did to the Console. For your first runs, sell one cheap item with low limits per run and watch the whole run.

## Languages

The windows are available in English, Deutsch, Français, Español, Português (Brasil), Русский, Türkçe, 日本語, and 中文. The plugin picks a language from your Dalamud and game client settings on first launch; change it any time under Settings, General, Language. Game data such as zone, item, and world names always follows the game client.

Spotted a wrong or awkward translation? Open a [translation issue](https://github.com/XeldarAlz/FFXIV-AutoSeller/issues/new?template=translation_report.yml) and tell me what it should say instead.

## Community

Questions, ideas, or just want to hang out with other players? Come say hi on Discord.

→ [Join our Discord](https://discord.gg/hppkAvdBEE)

## More from me

If you liked this plugin, take a look at my other Dalamud work. You might find something else there for you.

→ [XeldarAlz Dalamud Plugins](https://github.com/XeldarAlz/DalamudPlugins)

## License

AGPL-3.0-or-later. See [LICENSE.md](LICENSE.md). [NOTICE](NOTICE) adds the attribution terms the AGPL allows: a fork, or any project that reuses this code, must credit the original author and must not pass itself off as the original. The license covers the code, not the name or the icon: read the [trademark and naming policy](TRADEMARK.md) before you publish a fork.

How AI is used to build this plugin is written down in [AI usage](AI-USAGE.md).
