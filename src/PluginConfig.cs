﻿using BepInEx.Configuration;
using UnityEngine;

namespace Bottleneck
{
    public static class PluginConfig
    {
        public static ConfigEntry<int> productionPlanetCount;
        public static ConfigEntry<bool> popupLowPowerWarnings;
        public static ConfigEntry<float> lackOfProductionRatioTrigger;
        public static ConfigEntry<float> consumptionToProductionRatioTrigger;
        public static ConfigEntry<float> maximumReachRatioTrigger;
        public static ConfigEntry<bool> statsOnly;
        public static ConfigEntry<bool> planetFilter;
        public static ConfigEntry<bool> systemFilter;
        public static ConfigEntry<bool> includeSecondLevelConsumerProducer;
        public static ConfigEntry<bool> disableItemHoverTip;
        public static ConfigEntry<int> overwriteStackingLevel;
        public static ConfigEntry<int> overwriteProliferatorLevel;
        public static ConfigEntry<bool> disableProliferatorCalc; // To make it not break MMS compat
        public static ConfigEntry<float> ejectorSpeedFactor;
        public static ConfigEntry<float> siloSpeedFactor;
        public static ConfigEntry<float> minerOutputLimit;
        public static ConfigEntry<bool> displayPerSecond;
        public static ConfigEntry<int> fontSizeValue;


        public static void InitConfig(ConfigFile confFile)
        {
            productionPlanetCount = confFile.Bind("General", "ProductionPlanetCount", 5, new ConfigDescription(
                "Number of production planets to show. Too many and tip gets very large",
                new AcceptableValueRange<int>(2, 35)));
            includeSecondLevelConsumerProducer = confFile.Bind("General", "Include Second Level Items", true, 
                "Disable to show only the direct consumers or producers. When enabled one extra level of consumer/producer will be included in results");
            disableItemHoverTip = confFile.Bind("General", "Disable Item Hover Tip", false, 
                "Suppress item tooltip in stats window");
            popupLowPowerWarnings = confFile.Bind("General", "PopupLowPowerWarnings", true, "When planets with too little power are detected a message will be popped up (once per session)");
            planetFilter = confFile.Bind("General", "Planet Filter", true,
                "When precursor/consumer filter is active filter planet list to only ones that produce/consume selected item");            
            systemFilter = confFile.Bind("General", "System Filter", true,
                "When planet filter is active include star systems item in list (requires Planet Filter enabled)");            
            lackOfProductionRatioTrigger = confFile.Bind("General", "lackOfProductionRatio", 1.0f, //
                "When consumption rises above the given ratio of max production, flag both text in red." +//
                " (e.g. if set to '0.9' then you will be warned if you consume more than 90% of your max production)");
            consumptionToProductionRatioTrigger = confFile.Bind("General", "consumptionToProductionRatio", 1.5f, //
                "If max consumption raises above the given max production ratio, flag the text in yellow." +//
                " (e.g. if set to '1.5' then you will be warned if your max consumption is more than 150% of your max production)");
            maximumReachRatioTrigger = confFile.Bind("General", "maximumReachRatio", 0.999f, //
                "If production or consumption raise above the given theory max ratio, flag the unit text in blue or yellow.");

            statsOnly = confFile.Bind("Stats", "Disable Bottleneck", false,
                "Disable Bottleneck functionality, use only BetterStats features");
            overwriteStackingLevel = confFile.Bind("Stats", "Overwrite Stacking Level", -1,
                "Overwrite the maximum cargo stacking level. By default it uses the vanilla limit (4)");
            overwriteProliferatorLevel = confFile.Bind("Stats", "Overwrite Proliferator Level", -1,
                "Overwrite the maximum proliferator level. By default it uses the highest proliferator unlocked");
            disableProliferatorCalc = confFile.Bind("Stats", "Disable Proliferator Calculation", false,
                "Tells mod to ignore proliferator points completely. Can cause production rates to exceed theoretical max values");
            ejectorSpeedFactor = confFile.Bind("Stats", "Ejector Speed Factor", 1.0f,
                "EM-Rail Ejector speed multiplier. Set this value to 2.0 when feeding proliferated sails.");
            siloSpeedFactor = confFile.Bind("Stats", "Silo Speed Factor", 1.0f,
                "Vertical Launching Silo speed multiplier. Set this value to 2.0 when feeding proliferated rockets.");
            minerOutputLimit = confFile.Bind("Stats", "Miner Output Limit", 0.0f,
                "Maximum output limit (/min) of Mining Machine, Water Pump or Oil Extractor. Default value (0) is no limit");

            displayPerSecond = confFile.Bind("UI", "displayPerSecond", false,
                "Used by UI to persist the last selected value for checkbox");
            fontSizeValue = confFile.Bind("UI", "Font Size - Value", 26,
                "Font size of the value text in UIProductEntry. Vanilla font size is 34");
        }
    }
}
