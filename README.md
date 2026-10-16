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

Shows your retainers' listings and everything in your bags in one list. For each row choose what happens: keep a listing the cheapest, give it your own price, or sell an item on the market board or to an NPC. Press **Sell now** or **Undercut now** and the plugin goes to a summoning bell and does it all in one trip, or switch on **Autopilot** to have it done on its own while you are idle. Nothing new is listed until you tick it, no price goes below its floor, and every sale is recorded.

## Features

- **Sell page**: your listings and how they stand, everything you carry that can be sold, and one-click buttons per row: Undercut, My price, Market board or NPC.
- **Autopilot**: every hour, 2 hours or 4 hours, only once you have stood still outside combat, duties and cutscenes, or after each AutoRetainer venture.
- **Sell now and Undercut now**: lists and vendors only what you picked, or only checks and undercuts your listings, visiting only the retainers that need it.
- **Guarded pricing**: Undercut or Match against the same quality only, bait listings ignored, floors never below vendor value, and a step limit, cooldown and price-war detection against a race to the bottom.
- **Never against yourself**: your own retainers and your alts are never undercut.
- **Rules**: per-item floors, ceilings, fixed prices, lot sizes, keep counts and preferred retainers, category rules with a junk rule, and Never sell for anything protected.
- **Sources**: your bags and crystals, and when you opt in, a retainer's own inventory, the saddlebag and the armoury chest.
- **Assist**: compares prices when you open a sell window by hand and fills the suggested price, leaving Confirm to you.
- **In-game buttons**: Run all retainers on the retainer list, Reprice this retainer on a sell list, and Sell now on AutoRetainer's list.
- **Undercut watch**: your listings checked against Universalis on a timer, with no game request.
- **Sales**: a ledger of every sale with daily, 7-day and 30-day totals, a chart, hints on what sells fast or sits unsold, and a CSV export.
- **Run controls**: Pause, Resume and Stop, auto-pause while you're in a duty, and hold new moves while you type in chat.
- **Humanizer**: short random delays before moving off or teleporting.
- **GM alert**: ends a run when a GM is near, with optional toast, chat line, beeps, custom commands, or closing the game.
- **Runs and console**: past runs with their outcome, and every step logged on the Console page, ready to copy into a bug report.

## Install

In-game: `/xlsettings` → **Experimental** → paste into **Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/XeldarAlz/DalamudPlugins/main/repo.json
```

Tick **Enabled**, click **+**, then **Save and Close**. Open `/xlplugins` → **All Plugins**, search for **Auto Seller**, and install.

The plugin needs **vnavmesh** to walk to a summoning bell, and uses **AutoRetainer** when it is installed to run after ventures, respect its protect list and know your alts. Open `/asell deps` after install to see the list and one-click each missing one. Runs work for the current character on its home world; for your first runs, sell one cheap item with low limits per run and watch the whole run.

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
| `/asell start` | Start a run of the current plan |
| `/asell stop` | Stop the current run |
| `/asell pause` | Pause or resume the current run (`/asell resume` works too) |
| `/asell help` | List these commands in the chat |
| `/asell target` | Log targeted NPC's BaseId (debug helper) |
| `/asell look <item id>` | Compare market prices for one item once and write them to the Console; never lists or reprices (debug helper) |
| `/asell mbstatus` | Write the market checks, the search state and the age of the Universalis reference data to the Console (debug helper) |
| `/asell sendpath compare\|proxy` | Choose how `/asell look` sends its searches; alone, shows which one is in use (debug helper) |
| `/asell historypath window\|packet` | Choose where a retainer's sale history is read from; alone, shows which one is in use (debug helper) |

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
