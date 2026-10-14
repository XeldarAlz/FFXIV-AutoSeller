# Third-party notices

Auto Seller ships no third-party data and no code ported from other plugins. The one bundled third-party asset is the font below; its license travels with it.

The release zip also carries the compiled libraries the plugin builds against: the ECommons submodule and the NuGet packages pinned in `AutoSeller/packages.lock.json` (the clib automation library and what it depends on). They ship as built and unmodified, each under its own license, and none of their source is copied into this repository. Besides those, the zip holds the plugin itself, its manifest, its icon, the nine language catalogs and the font below.

vnavmesh, AutoRetainer and Marketbuddy are separate plugins you install yourself; none of their code ships with Auto Seller, which talks to them only through Dalamud's IPC. Market prices, sale history and tax rates are read at run time from the public Universalis API (universalis.app); no Universalis data ships with the plugin. Game data such as item, zone, world and retainer names, the retainer menu text, the market board listings and the sell window's prompts is read from your game client at run time.

## Noto Sans

- Source: Noto Sans Medium by the Noto Project Authors, https://github.com/notofonts/latin-greek-cyrillic, shipped as `AutoSeller/Fonts/NotoSans-Medium-Latin.ttf`.
- Used: the primary font for Latin letters, digits and punctuation in the plugin's windows.
- License: SIL Open Font License 1.1, shipped next to the font in full as `AutoSeller/Fonts/OFL-NotoSans.txt`.
