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
_Note: If brokenmass [merges the changes](https://github.com/DysonSphereMod/QOL/pull/125) into BetterStats then this fork will go away_

For production items that can be proliferated, buttons are added next to each item where you can choose between:

* Disable - don't consider Profilerator when determining Theoretical max production for the item  
* Assembler setting - Use the assemblers current setting (more products or more speed) when calculating theoretical max
* Force speed - Calculate theoretical max assuming every assembler is in Production Speedup mode
* Force productivity - Calculate theoretical max assuming every assembler is in Extra Products mode. Only available for recipes that support extra products

![Proliferator](https://github.com/starfi5h/dsp-bottleneck/blob/master/Examples/stats_buttons.png?raw=true)

## Config

* ProductionPlanetCount allows showing more "Produced on" planets in tooltip (max 15)
* 'Disable Bottleneck' lets you disable the Bottleneck functionality of this mod and just focus on stats
* 'Disable Proliferator Calculation' removes Proliferator from Theoretical max calculations completely
* 'Planet Filter' removes non-production (or non-consumption) planets from list when a precursor/consumer item filter is active
* 'System Filter' when Planet Filter is active add a "Star System" item the list for system with producers  
* 'Include Second Level Items' when a precursor/consumer item filter is active also include grandparent / grandchild precursor/consumer   

## Notes
This mod was originally planned as an enhancement to BetterStats by brokenmass. Now this fork continue the work of Semar and adpat to the Dark Fog update in the game.

Planetary consumption/production is only calculated one time after the statistics window is opened. If you add machines to your factory while the stats window is
open (maybe you're running at a very high resolution?) then you'll have to close and re-open the window to see those values update to reflect the change

## Contact
Bugs? Create an issue in the github repository.