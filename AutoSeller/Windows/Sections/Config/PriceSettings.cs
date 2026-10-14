using AutoSeller.Core.Localization;
using AutoSeller.Core.Pricing.Model;
using AutoSeller.Windows.Components;

namespace AutoSeller.Windows.Sections.Config;

internal static class PriceSettings
{
    private const int PercentStep = 5;
    private const int CooldownStepMinutes = 30;
    private const int CapStep = 5;
    private const float BasisPointsPerPercent = 100f;
    private const float PercentTenth = 0.1f;

    private static readonly SettingsControls.Choices.Choice[] modeChoices =
    [
        new(L.Pricing.ModeUndercut, L.Pricing.ModeUndercutDetail),
        new(L.Pricing.ModeMatch, L.Pricing.ModeMatchDetail),
    ];

    private static readonly SettingsControls.Choices.Choice[] unitChoices =
    [
        new(L.Pricing.UnitGil, L.Pricing.UnitGilDetail),
        new(L.Pricing.UnitPercent, L.Pricing.UnitPercentDetail),
    ];

    private static readonly SettingsControls.Choices.Choice[] newLotChoices =
    [
        new(L.Pricing.BelowFloorNewKeep, L.Pricing.BelowFloorNewKeepDetail),
        new(L.Pricing.BelowFloorNewList, L.Pricing.BelowFloorNewListDetail),
    ];

    private static readonly SettingsControls.Choices.Choice[] listingChoices =
    [
        new(L.Pricing.BelowFloorListingKeep, L.Pricing.BelowFloorListingKeepDetail),
        new(L.Pricing.BelowFloorListingReturn, L.Pricing.BelowFloorListingReturnDetail),
    ];

    public static void Draw(Configuration configuration)
    {
        DrawPriceGroup(configuration);
        DrawGuardGroup(configuration);
        DrawRaiseGroup(configuration);
        DrawFloorGroup(configuration);
        DrawCapGroup(configuration);
    }

    private static void DrawPriceGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Pricing.GroupPrice));
        SettingsRow.Draw(Loc.T(L.Pricing.Mode), Loc.T(L.Pricing.ModeHelp), SettingsControls.RowComboWidth, configuration, static cfg =>
        {
            var selected = (int)cfg.PricingMode;
            if (SettingsControls.Choices.DrawCombo("##asell_pricing_mode", modeChoices, ref selected))
            {
                cfg.PricingMode = (PricingMode)selected;
                cfg.SaveDebounced();
            }
        });

        using var undercut = Motion.PushSection("##asell_pricing_undercut", configuration.PricingMode == PricingMode.Undercut);
        if (undercut is null)
        {
            return;
        }

        SettingsRow.Draw(Loc.T(L.Pricing.UndercutUnit), Loc.T(L.Pricing.UndercutUnitHelp), SettingsControls.RowComboWidth, configuration, static cfg =>
        {
            var selected = (int)cfg.UndercutUnit;
            if (SettingsControls.Choices.DrawCombo("##asell_pricing_unit", unitChoices, ref selected))
            {
                cfg.UndercutUnit = (UndercutUnit)selected;
                cfg.SaveDebounced();
            }
        });

        if (configuration.UndercutUnit == UndercutUnit.Gil)
        {
            SettingsRow.Draw(Loc.T(L.Pricing.UndercutGil), Loc.T(L.Pricing.UndercutGilHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
            {
                var range = PricingSettings.UndercutGilRange;
                if (SettingsControls.Step("##asell_pricing_undercut_gil", (int)cfg.UndercutGil, 1, (int)range.Minimum, (int)range.Maximum, Loc.T(L.Pricing.GilFormat), out var gil))
                {
                    cfg.UndercutGil = (uint)gil;
                    cfg.SaveDebounced();
                }
            });
            return;
        }

        SettingsRow.Draw(Loc.T(L.Pricing.UndercutPercent), Loc.T(L.Pricing.UndercutPercentHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
        {
            var range = PricingSettings.UndercutBasisPointsRange;
            var percent = cfg.UndercutBasisPoints / BasisPointsPerPercent;
            if (Stepper.Draw("##asell_pricing_undercut_percent", ref percent, PercentTenth, range.Minimum / BasisPointsPerPercent, range.Maximum / BasisPointsPerPercent,
                    Loc.T(L.Pricing.PercentTenthsFormat), SettingsControls.RowStepperWidth))
            {
                cfg.UndercutBasisPoints = range.Clamp((int)MathF.Round(percent * BasisPointsPerPercent));
                cfg.SaveDebounced();
            }
        });
    }

    private static void DrawGuardGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Pricing.GroupGuards));
        SettingsRow.Draw(Loc.T(L.Pricing.ReferenceRatio), Loc.T(L.Pricing.ReferenceRatioHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
        {
            var range = PricingSettings.MinimumReferenceRatioPercentRange;
            if (SettingsControls.Step("##asell_pricing_reference", cfg.MinimumReferenceRatioPercent, PercentStep, range.Minimum, range.Maximum, Loc.T(L.Pricing.PercentFormat), out var value))
            {
                cfg.MinimumReferenceRatioPercent = value;
                cfg.SaveDebounced();
            }
        });

        SettingsRow.Draw(Loc.T(L.Pricing.BaitRatio), Loc.T(L.Pricing.BaitRatioHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
        {
            var range = PricingSettings.BaitRatioPercentRange;
            if (SettingsControls.Step("##asell_pricing_bait", cfg.BaitRatioPercent, PercentStep, range.Minimum, range.Maximum, Loc.T(L.Pricing.PercentFormat), out var value))
            {
                cfg.BaitRatioPercent = value;
                cfg.SaveDebounced();
            }
        });

        SettingsRow.Draw(Loc.T(L.Pricing.StepLimit), Loc.T(L.Pricing.StepLimitHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
        {
            var range = PricingSettings.StepLimitPercentRange;
            if (SettingsControls.Step("##asell_pricing_step", cfg.StepLimitPercent, PercentStep, range.Minimum, range.Maximum, Loc.T(L.Pricing.PercentFormat), out var value))
            {
                cfg.StepLimitPercent = value;
                cfg.SaveDebounced();
            }
        });

        SettingsRow.Draw(Loc.T(L.Pricing.Cooldown), Loc.T(L.Pricing.CooldownHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
        {
            if (SettingsControls.Step("##asell_pricing_cooldown", cfg.RepriceCooldownMinutes, CooldownStepMinutes, Configuration.MinimumRepriceCooldownMinutes,
                    Configuration.MaximumRepriceCooldownMinutes, Loc.T(L.Pricing.MinutesFormat), out var value))
            {
                cfg.RepriceCooldownMinutes = value;
                cfg.SaveDebounced();
            }
        });

        SettingsRow.Draw(Loc.T(L.Pricing.PriceWarDrops), Loc.T(L.Pricing.PriceWarDropsHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
        {
            var range = PricingSettings.PriceWarDropCountRange;
            if (SettingsControls.Step("##asell_pricing_war_drops", cfg.PriceWarDropCount, 1, range.Minimum, range.Maximum, Loc.T(L.Pricing.DropsFormat), out var value))
            {
                cfg.PriceWarDropCount = value;
                cfg.SaveDebounced();
            }
        });

        SettingsRow.Draw(Loc.T(L.Pricing.PriceWarWindow), Loc.T(L.Pricing.PriceWarWindowHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
        {
            if (SettingsControls.Step("##asell_pricing_war_window", cfg.PriceWarWindowHours, 1, Configuration.MinimumPriceWarWindowHours,
                    Configuration.MaximumPriceWarWindowHours, Loc.T(L.Pricing.HoursFormat), out var value))
            {
                cfg.PriceWarWindowHours = value;
                cfg.SaveDebounced();
            }
        });
    }

    private static void DrawRaiseGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Pricing.GroupRaise));
        SettingsRow.Draw(Loc.T(L.Pricing.Raise), Loc.T(L.Pricing.RaiseHelp), SettingsControls.ToggleWidth, configuration, static cfg =>
        {
            if (SettingsControls.Toggle("##asell_pricing_raise", cfg.RaisePrices, out var value))
            {
                cfg.RaisePrices = value;
                cfg.SaveDebounced();
            }
        }, SettingsRow.ToggleHeight);

        using var raising = Motion.PushSection("##asell_pricing_raising", configuration.RaisePrices);
        if (raising is null)
        {
            return;
        }

        SettingsRow.Draw(Loc.T(L.Pricing.RaiseGap), Loc.T(L.Pricing.RaiseGapHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
        {
            var range = PricingSettings.RaiseGapPercentRange;
            if (SettingsControls.Step("##asell_pricing_raise_gap", cfg.RaiseGapPercent, 1, range.Minimum, range.Maximum, Loc.T(L.Pricing.PercentFormat), out var value))
            {
                cfg.RaiseGapPercent = value;
                cfg.SaveDebounced();
            }
        });

        SettingsRow.Draw(Loc.T(L.Pricing.MaxRaise), Loc.T(L.Pricing.MaxRaiseHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
        {
            var range = PricingSettings.MaximumRaisePercentRange;
            if (SettingsControls.Step("##asell_pricing_max_raise", cfg.MaximumRaisePercent, PercentStep, range.Minimum, range.Maximum, Loc.T(L.Pricing.PercentFormat), out var value))
            {
                cfg.MaximumRaisePercent = value;
                cfg.SaveDebounced();
            }
        });
    }

    private static void DrawFloorGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Pricing.GroupFloor));
        SettingsRow.Draw(Loc.T(L.Pricing.BelowFloorNew), Loc.T(L.Pricing.BelowFloorNewHelp), SettingsControls.RowComboWidth, configuration, static cfg =>
        {
            var selected = (int)cfg.BelowFloorNewLot;
            if (SettingsControls.Choices.DrawCombo("##asell_pricing_floor_new", newLotChoices, ref selected))
            {
                cfg.BelowFloorNewLot = (BelowFloorNewLot)selected;
                cfg.SaveDebounced();
            }
        });

        SettingsRow.Draw(Loc.T(L.Pricing.BelowFloorListing), Loc.T(L.Pricing.BelowFloorListingHelp), SettingsControls.RowComboWidth, configuration, static cfg =>
        {
            var selected = (int)cfg.BelowFloorListing;
            if (SettingsControls.Choices.DrawCombo("##asell_pricing_floor_listing", listingChoices, ref selected))
            {
                cfg.BelowFloorListing = (BelowFloorListing)selected;
                cfg.SaveDebounced();
            }
        });
    }

    private static void DrawCapGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Pricing.GroupCaps));
        SettingsRow.Draw(Loc.T(L.Pricing.MaxReprices), Loc.T(L.Pricing.MaxRepricesHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
        {
            if (SettingsControls.Step("##asell_pricing_max_reprices", cfg.MaxRepricesPerRun, CapStep, 0, Configuration.MaximumWritesPerRun, "%d", out var value))
            {
                cfg.MaxRepricesPerRun = value;
                cfg.SaveDebounced();
            }
        });

        SettingsRow.Draw(Loc.T(L.Pricing.MaxListings), Loc.T(L.Pricing.MaxListingsHelp), SettingsControls.RowStepperWidth, configuration, static cfg =>
        {
            if (SettingsControls.Step("##asell_pricing_max_listings", cfg.MaxNewListingsPerRun, CapStep, 0, Configuration.MaximumWritesPerRun, "%d", out var value))
            {
                cfg.MaxNewListingsPerRun = value;
                cfg.SaveDebounced();
            }
        });
    }
}
