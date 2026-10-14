# Contributing

Thanks for taking an interest. This is a small solo project, but PRs are welcome and I'll review them.

## Quick start

```bash
git clone --recurse-submodules https://github.com/XeldarAlz/FFXIV-AutoSeller.git
cd FFXIV-AutoSeller
dotnet build AutoSeller.sln -c Release
dotnet test AutoSeller.sln -c Release
```

You need the .NET 10 SDK. The plugin requires Dalamud at runtime; CI pulls a Dalamud dev build automatically and that's enough to compile. When your Dalamud build is not where XIVLauncher puts it, pass its folder with `-p:DalamudLibPath=<folder>/`. See `.github/workflows/ci.yml` if you want to reproduce CI locally.

`dotnet test AutoSeller.Tests -c Release` runs the unit tests alone. They cover every pure model, run those models together in the order a sell run uses them (`AutoSeller.Tests/EndToEnd/`), check that all nine catalogs carry the same keys in the same order with the same placeholders and no em dashes, and that the newest changelog entry matches the version in `Directory.Build.props`. The test project does not reference the plugin; it links `Core/Shared/`, `Core/Changelog/`, the string table, `Core/Ledger/`, `Core/Pricing/Universalis/` and every `Model/` folder under `AutoSeller/Core/` by path, so code placed there is tested without editing the test project and must not depend on Dalamud, ECommons, FFXIVClientStructs or clib.

Load the built plugin via `/xlsettings` -> **Experimental** -> **Dev Plugin Locations**, pointing at `AutoSeller/bin/Release/AutoSeller.dll`.

## Project layout

- `AutoSeller/Core/Rules/`: the rule book frozen at Start (sell list, category rules, never-sell list, vendor settings, sources) and its builder, item eligibility, lot planning, the vendor rules, the rules import and export, the blocked items view, the gear set guard and the rules check behind the Rules page.
- `AutoSeller/Core/Items/`: the marketable item catalog with its search and item categories, and the game data facts of any item, read once per item.
- `AutoSeller/Core/Inventory/`: the bag reader and the containers each source reads.
- `AutoSeller/Core/Characters/`: each character's retainer switches, last retainer snapshots, reprice history, approvals and plan edits, kept in `chars/<content id>.json`.
- `AutoSeller/Core/Market/`: the Compare Prices search machine and its cross-check, the competitor snapshot and search reuse, the send path choice, the sale history capture, the market gates, and the game side: the search tracker with its hooks, the market checks and the `/asell look`, `/asell mbstatus` and `/asell sendpath` commands.
- `AutoSeller/Core/Pricing/`: the price decision with its floor, bait filter, reference choice and reprice history, the undercut watch, own sellers, the Universalis client, throttle, translator, reference snapshot and fetch generations under `Universalis/`, and the reference service that fetches them.
- `AutoSeller/Core/Allocation/`: the slot allocator that spreads lots over retainers, and the player's plan row edits.
- `AutoSeller/Core/Retainers/`: retainer market snapshots and the diff between two reads of a retainer, the retainer roster reader and the market reader of the open retainer.
- `AutoSeller/Core/Execution/`: the sell authorization frozen at Start with its fingerprints and approved prices, the sale guard, the sell window check, the read-back, the value book, the journal events and their replay.
- `AutoSeller/Core/Ledger/`: the run journal, durable files and checkpoints.
- `AutoSeller/Core/Sales/`: sale notice and sale history parsing, inferred sales, the sale matcher, stats, hints and the CSV export.
- `AutoSeller/Core/Driver/`: the retainer window chain, UI pacing and prompt text matching.
- `AutoSeller/Core/AutoRetainer/`: the AutoRetainer state and the post-process budget.
- `AutoSeller/Core/Tasks/`: the run controller, the run phases, pause reasons, Start's readiness check and block rules, and the shared movement helpers (teleport, aethernet, walking, landing).
- `AutoSeller/Core/Shared/`: gil arithmetic, the market limits, item keys and sorted id sets every model shares.
- `AutoSeller/Core/Travel/` and `AutoSeller/Core/Ipc/`: zone and aetheryte data, and the vnavmesh bridge.
- `AutoSeller/Core/Game/`: NPC and dialog handling, text input focus, and the duty and GM watchers.
- `AutoSeller/Core/External/`: the helper plugins on the Plugins page and their one-click install.
- `AutoSeller/Core/Stats/` and `AutoSeller/Core/Changelog/`: History and the changelog entries.
- `AutoSeller/Core/` itself and `Core/Debug/`: the run log behind the Console page, the humanizer, and the `/asell target` dump.
- `AutoSeller/Core/Localization/` and `AutoSeller/Localization/`: the string table and the nine language catalogs.
- `AutoSeller/Windows/`: the ImGui window with its shell, pages, sections, settings and components.
- `AutoSeller/`: plugin entry points, config, command wiring.
- `AutoSeller.Tests/`: unit tests for everything above that runs without the game, and `EndToEnd/`, which carries one sell run through the pure models from the rule book to a sale record.
- `ECommons/`: submodule, shared Dalamud helpers. Don't patch this directly; upstream it.

Keep logic small and direct. This plugin has one job.

## Before you open a PR

1. `dotnet build -c Release` cleanly, and `dotnet test -c Release` passes.
2. Test in game: at least one dry run at a summoning bell with the Console open, from Start through comparing prices to the sell window, and one supervised real listing of a single cheap item when your change touches the run, the pricing or the retainer windows. Watch that listing the whole way and attach its Console lines. When you change the AutoRetainer post-process, let it run after one venture with the dry run on first. Retainer menus and the sell window read differently per client language, so a fix that works in one language may not work in another.
3. Keep the diff focused. One concern per PR.
4. Match the existing style. No heavy abstractions "for later."
5. Every string a user sees goes through `Core/Localization/L*.cs` and all nine catalogs under `AutoSeller/Localization/`, with the same keys in the same order. CI checks the keys and rejects em dashes.
6. If your change affects what a user sees or types (commands, window layout, settings), update the README.
7. If you used AI beyond autocomplete, say which level in the PR description. It is one line, and [AI-USAGE.md](AI-USAGE.md) explains the level names and why I ask.

## Good first issues

Check the tracker for anything labeled `good first issue`. Retainer window quirks in a client language other than English, a summoning bell it cannot walk up to, and translation fixes are usually the lowest-friction way to help: name where it misbehaved, attach a log of what the plugin did vs. what should have happened, and a fix is usually a small change.

## Security

Please don't file public issues for security problems; see [SECURITY.md](SECURITY.md).

## Code of conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Be decent.

## License

By contributing, you agree your contributions are licensed under AGPL-3.0-or-later with the additional terms in [NOTICE](NOTICE), the same as the project.
