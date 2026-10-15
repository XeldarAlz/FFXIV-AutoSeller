## What

<!-- One or two sentences on what this PR changes. -->

## Why

<!-- The motivating problem, linked issue, or user-visible behavior this fixes. -->

Closes #

## How to test

<!--
Minimum steps a reviewer can run to verify the change. Testing usually means writing a rule for one cheap item, checking the plan on the Sell page, and pressing Start with low limits per run, watching the plugin go to a summoning bell, read each retainer's listings, compare prices and list or reprice that item with the Console open. When the change touches the run, the pricing or the retainer windows, say which cheap item you listed or repriced and that you watched it the whole way; when it touches the AutoRetainer post-process, say that it ran after a venture and handed the retainer back. Make sure the dependencies listed in /asell deps are installed first. For UI-only changes, describe what to click.
-->

## Checklist

- [ ] `dotnet build -c Release` passes and `dotnet test -c Release` is green
- [ ] Verified in-game on at least one supervised run of a rule for one cheap item with low limits per run
- [ ] New or changed user-facing text is in `L*.cs` and all nine catalogs, same keys in the same order
- [ ] If this changes user-visible behavior, README is updated
- [ ] If this touches the run, the pricing or the retainer windows, relevant `[ASELL]` log lines make the sequence auditable
