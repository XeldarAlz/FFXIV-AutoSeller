# AI usage

This doc records how AI tooling is used to build Auto Seller and what level of involvement that amounts to. Read it before you open a pull request.

Auto Seller ships from its own repository, so the [official Dalamud plugin repository's AI policy](https://dalamud.dev/plugin-publishing/ai-policy/) does not bind it. This project holds to that policy anyway, because it is the community's standard for what honest AI use looks like in a Dalamud plugin, and because a plugin nobody is forced to audit is exactly the kind that should be able to survive one. Everything below is written to satisfy it.

## Key files

| Path | Role |
| --- | --- |
| AI-USAGE.md | This doc: declared level, pipeline, and what contributors declare |
| CONTRIBUTING.md | The pull request checklist that points here, and the in-game testing bar every change is held to |

## Declared level

Dalamud's policy uses six levels, adapted from [AI-DECLARATION.md](https://ai-declaration.md/): None, Hint, Assist, Pair, Copilot, Auto.

**Auto Seller declares Copilot.** AI implements while the human plans, reviews, tests, and owns the result. The AI does most of the writing.

That is the honest label and it is deliberately not the lowest one that could be argued. The implementation step inside a task runs autonomously, which reads as Auto if you look only at that step. It is bounded on both ends by a human who decides what gets built and whether it ships, which is what Copilot describes.

## How the work actually happens

Claude Code is the tool, used as a senior harness rather than as an autocomplete:

- **Planned by the human.** Each phase of the plugin (reading the retainers, the pricing engine, the retainer driver, the AutoRetainer integration, the sales ledger, the windows) and each round of fixes starts as a written plan: what to build, the rules it has to keep, and how it will be checked.
- **Parallel implementers.** Claude Code agent pipelines carry the plan out, with several implementer agents working at once, each on its own branch, writing the code and its unit tests.
- **Independent reviewers.** Separate reviewer agents read each diff back without having written it, against the plan, CONTRIBUTING.md and .editorconfig as the rulebook, and their findings go back to an implementer before anything merges.
- **Autonomous execution inside human gates.** The pipeline runs unattended once started. It does not decide what to start, and it does not decide what ships.

The human gates, in order, none of them skippable:

1. **The plan comes first.** What to build, and the constraints it has to respect, are written before any agent runs.
2. **The diff is read.** Output is reviewed before it lands, not after users find the problem.
3. **It is tested in the game before release.** On a real client, one supervised run end to end on a rule for a single cheap item with low limits per run, watched the whole way when the change touches the run, the pricing or the retainer windows. A clean `dotnet build` and a green `dotnet test` do not replace it.
4. **Ownership transfers.** Every merged line is the maintainer's to defend, explain, and fix. "The AI did it" is not an answer to why something is written the way it is.
5. **Feedback is taken on its merits.** AI-assisted work invites sharper review, from users and from any reviewer, and that scrutiny is earned rather than unfair. The answer to a review comment is a fix or a reason, never a defense of the tooling.

## What this does not change

Nothing in this doc lowers a standard. Code produced with AI assistance is held to exactly the bar every other change meets: the CONTRIBUTING.md checklist, the .editorconfig style, one concern per pull request, and `[ASELL]` log lines that make every step of a run auditable. A reviewer cannot tell which lines came from where, and that is the point.

The one thing AI use does change is where the verification effort goes. AI gets Dalamud and FFXIVClientStructs APIs wrong often enough that any call into either is suspect until it has run in game, and the retainer list, the retainer's sell list, the sell window, the sale history and the market board search results are read straight from game memory. The price data from Universalis and the IPC calls into vnavmesh, AutoRetainer and Marketbuddy, with the helpers listed in `/asell deps`, deserve the same suspicion: a call that compiles says nothing about whether the service or the other plugin still answers it the same way. The price write deserves the most: retainer menu entries are chosen by their text, no price goes below its floor or past the step limit without approval, every price is checked in the sell window before Confirm and read back from the retainer's listings after it, and a write that cannot be confirmed stops the run. A first run on one cheap item with low limits per run keeps what a mistake can cost small.

## If you contribute

Three rules, no forms:

1. **Declare your level in the pull request description** if you went beyond autocomplete or inline suggestions. Use Dalamud's six level names so the vocabulary here matches theirs. None and Hint need no declaration.
2. **Test your change in game yourself** before you open the PR. This is already item 2 of the CONTRIBUTING.md checklist.
3. **Be able to explain your code.** If you cannot say why something is written the way it is, it is not ready.

Nobody is judged for the level they declare. An undeclared one is the problem.

## Translations

The eight non-English catalogs under AutoSeller/Localization/ are AI-assisted with human review. English is the source: it is the default text of every string in the `Core/Localization/L*.cs` files, and en.json is never loaded when the plugin runs in English. Game data is not translated at all: zone, item, world and retainer names are read from the game client, and so are the retainer menu entries and the prompts the run looks for, so they always match what the player sees in their own client.

This is the approach Dalamud's policy asks for, and the gap is coverage rather than method: catalogs without a native-speaker pass should be treated as placeholders until one lands. Native corrections are welcome from anyone, through the [translation issue form](https://github.com/XeldarAlz/FFXIV-AutoSeller/issues/new?template=translation_report.yml).

## Gotchas

- **Nobody enforces this on the project, which is the whole point.** A custom repository has no review queue and no ban list, so every rule here holds because the project chose it. Standards that only survive enforcement are not standards.
- **The no-attribution git rule is not concealment.** Commits here carry no co-author trailers or generated-with footers, so a commit message holds substantive content only. Disclosure lives in this doc and in pull request descriptions instead. The history on master carries no trailers at all, which is exactly why this doc has to exist: without it, nothing in the repository would say how the code was written.
- **A clean build is not a test.** Item 3 of the human gates exists because AI output compiles far more reliably than it works. Retainer menus, the sell window and its prompts read differently per client language, and market data differs per world and region, so a fix proven in one language or on one world says nothing about another.

## Related docs

- [CONTRIBUTING.md](CONTRIBUTING.md): the pull request checklist and the in-game testing bar.
- [TRADEMARK.md](TRADEMARK.md) and [NOTICE](NOTICE): what the license covers, the credit every fork owes, and the name and icon the license does not cover.
