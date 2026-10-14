# Security policy

## Supported versions

Only the latest release published to the custom Dalamud repo is supported. If you're on an older version, please update before reporting.

## Reporting a vulnerability

Please report security issues privately via GitHub's private vulnerability reporting:

https://github.com/XeldarAlz/FFXIV-AutoSeller/security/advisories/new

Please don't open a public issue or Discussion for anything that could let someone else exploit users of the plugin before a fix is out.

What counts:

- Code execution or crashes triggerable by crafted game state, by a crafted answer from a helper plugin such as AutoRetainer or Marketbuddy, or by a crafted Universalis response.
- The plugin selling something it shouldn't: an item no rule of yours allows, an item on the never-sell list, in a gear set, equipped or protected in AutoRetainer, a price below the item's floor, a drop past the step limit you did not approve, anything at all while the dry run is on, an undercut of your own or your alts' listings, a vendor sale of anything your rules do not call junk, or a Yes to a prompt it does not know.
- The plugin writing a price anywhere but the game's sell window, confirming a write it did not check first, or carrying on after a write it could not confirm.
- The plugin teleporting, travelling, or persisting something it shouldn't, beyond the documented repricing, listing and sales tracking, which is the feature, or leaving AutoRetainer held back or waiting after a run ends.
- Data exfiltration: the plugin's only network calls go to the public Universalis API (universalis.app) for price data and tax rates, with item and world ids only, and the one-click helper install on the Plugins page only asks Dalamud to add that helper's repository and install it. Sending your data anywhere would be a bug.

What it reads and keeps, so you can tell a bug from the design:

- **Read from the game, never sent anywhere**: your bags and crystals and, when you opt in, a retainer's own inventory, the saddlebag and the armoury chest; your gear sets, so nothing in one is sold; your retainers' names, cities, gil and market listings; the market board results it compares prices against; sale notices and each retainer's sale history; and, during a run, the retainer windows, the sell window and any prompt that opens.
- **Read from AutoRetainer, when installed**: its protect list and the retainers of your other characters, so protected items are never sold and your alts are never undercut. They are read again for every plan and never stored.
- **Kept on disk in the plugin's config folder**: your settings and rules; per character (keyed by the character's content id, the number Dalamud uses to tell characters apart) its name and home world, the retainers you enabled, the last snapshot of each retainer's listings with when each listing was first seen, reprice history, approvals and plan edits; a journal of every price write; the sales ledger (item, quality, quantity, price, time, retainer); and `run-history.json` with your past runs.
- **Copied only when you press Copy log**: the Console lines, with a header naming the plugin version, the Dalamud version and your zone.
- **Exported only when you ask**: the Sales page's CSV, which carries no character, retainer or buyer names and no ids.

What doesn't:

- The fact that Auto Seller reprices and lists your retainers' items for you. That's against Square Enix's Terms of Service and is the whole feature; turning it on is the user's choice.

I'll aim to acknowledge reports within a few days and to ship a fix or workaround as soon as I've verified the issue.
