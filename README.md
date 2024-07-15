# Bottleneck

_This is fork version of Semar's [Bottleneck](https://dsp.thunderstore.io/package/Semar/Bottleneck/) to support the Dark Fog update._  
_You can find readme files in other languages here_

English / [中文](https://github.com/starfi5h/dsp-bottleneck/blob/master/README_zhcn.md)

This mod adds some information to the stats panel to help find production bottlenecks. It will show the top 5 (configurable) planets an item is made on
and also try to assess what your assemblers are stuck on (needing items, no power, stacking). It also adds some filter buttons for limiting the items shown to 
only the precursor (or dependent) items to narrow down the search for bottlenecks

![Example](https://github.com/starfi5h/dsp-bottleneck/blob/master/Examples/screenshot.png?raw=true)

## BetterStats with Proliferator

This plugin contains a fork of BetterStats with support for Proliferator. To use it,
you'll have to disable the actual BetterStats plugin, unfortunately. The forked BetterStats
is completely optional, the Bottleneck plugin should work just fine when BetterStats is installed, the proliferator enhancements just won't be present.

For production items that can be proliferated, buttons are added next to each item where you can choose between:

* Disable - don't consider Profilerator when determining Theoretical max production for the item  
* Assembler setting - Use the assemblers current setting (more products or more speed) when calculating theoretical max
* Force speed - Calculate theoretical max assuming every assembler is in Production Speedup mode
* Force productivity - Calculate theoretical max assuming every assembler is in Extra Products mode. Only available for recipes that support extra products

![Proliferator](https://github.com/starfi5h/dsp-bottleneck/blob/master/Examples/stats_buttons.png?raw=true)

## Config

The config file can be found in `BepInEx\config\Bottleneck.cfg` after the mod load for the first time.  
If you're using mod manager, you can find the file in Config editor.  

```
## Settings file was created by plugin Bottleneck
## Plugin GUID: Bottleneck

[General]

## Number of production planets to show. Too many and tip gets very large
# Setting type: Int32
# Default value: 5
# Acceptable value range: From 2 to 35
ProductionPlanetCount = 5

## Disable to show only the direct consumers or producers. When enabled one extra level of consumer/producer will be included in results
# Setting type: Boolean
# Default value: true
Include Second Level Items = true

## Suppress item tooltip in stats window
# Setting type: Boolean
# Default value: false
Disable Item Hover Tip = false

## When planets with too little power are detected a message will be popped up (once per session)
# Setting type: Boolean
# Default value: true
PopupLowPowerWarnings = true

## When precursor/consumer filter is active filter planet list to only ones that produce/consume selected item
# Setting type: Boolean
# Default value: true
Planet Filter = true

## When planet filter is active include star systems item in list (requires Planet Filter enabled)
# Setting type: Boolean
# Default value: true
System Filter = true

## When consumption rises above the given ratio of max production, flag the text in red. (e.g. if set to '0.9' then you will be warned if you consume more than 90% of your max production)
# Setting type: Single
# Default value: 1
lackOfProductionRatio = 1

## If max consumption raises above the given max production ratio, flag the text in red. (e.g. if set to '1.5' then you will be warned if your max consumption is more than 150% of your max production)
# Setting type: Single
# Default value: 1.5
consumptionToProductionRatio = 1.5

[Stats]

## Disable Bottleneck functionality, use only BetterStats features
# Setting type: Boolean
# Default value: false
Disable Bottleneck = false

## Overwrite the maximum cargo stacking level. By default it uses the vanilla limit (4)
# Setting type: Int32
# Default value: -1
Overwrite Stacking Level = -1

## Overwrite the maximum proliferator level. By default it uses the highest proliferator unlocked
# Setting type: Int32
# Default value: -1
Overwrite Proliferator Level = -1

## Tells mod to ignore proliferator points completely. Can cause production rates to exceed theoretical max values
# Setting type: Boolean
# Default value: false
Disable Proliferator Calculation = false

## EM-Rail Ejector speed multiplier. Set this value to 2.0 when feeding proliferated sails.
# Setting type: Single
# Default value: 1
Ejector Speed Factor = 1

## Vertical Launching Silo speed multiplier. Set this value to 2.0 when feeding proliferated rockets.
# Setting type: Single
# Default value: 1
Silo Speed Factor = 1

## Maximum output limit (/min) of Mining Machine, Water Pump or Oil Extractor. Default value (0) is no limit
# Setting type: Single
# Default value: 0
Miner Output Limit = 0

[UI]

## Used by UI to persist the last selected value for checkbox
# Setting type: Boolean
# Default value: false
displayPerSecond = false

## Font size of the value text in UIProductEntry. Vanilla font size is 34
# Setting type: Int32
# Default value: 26
Font Size - Value = 26
```

* ProductionPlanetCount allows showing more "Produced on" planets in tooltip (max 35)
* 'Disable Bottleneck' lets you disable the Bottleneck functionality of this mod and just focus on stats
* 'Disable Proliferator Calculation' removes Proliferator from Theoretical max calculations completely
* 'Planet Filter' removes non-production (or non-consumption) planets from list when a precursor/consumer item filter is active
* 'System Filter' when Planet Filter is active add a "Star System" item the list for system with producers  
* 'Include Second Level Items' when a precursor/consumer item filter is active also include grandparent / grandchild precursor/consumer   

## Notes
This mod was originally planned as an enhancement to BetterStats by brokenmass. Now this fork continue the work of Semar and adpat to the Dark Fog update in the game.

Planetary consumption/production is only calculated one time after the statistics window is opened. If you add machines to your factory while the stats window is open then you'll have to close and re-open the window to see those values update to reflect the change.  

## Contact
Bugs? Create an issue in the github repository.