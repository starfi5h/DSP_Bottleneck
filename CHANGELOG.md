## Changelog

### v1.1.6
* Add: config `maximumReachRatioTrigger`  
* Bugfix: Fix theory max consumption of researching lab for non-white matrix.  
* Bugfix: Fix nebula client doesn't send query when opening stats window.  

### v1.1.5
* Bugfix: Fix theory max consumption of researching lab.  
* Change: Theory max consumption of spray coaster now use sprayed proliferator.  

### v1.1.4
* Bugfix: Fix nebula client doesn't send query when switching tabs.
* Change: Config `Font Size` default value is set to 26.
* Update: Slightly optimization. (0.10.30.22292)  

### v1.1.3
* Add: config `Font Size - Value`  
* Add: precursor/successor button now can click again to clear filter   

### v1.1.2
* Add: config `Ejector Speed Factor`, `Silo Speed Factor`, `Miner Output Limit`
* Add: filter and display per second in killtab.
* Change: refine decimal place in rate numbers.

### v1.1.1
* Update: Adpat NebulaAPIv2.0.0.  

### v1.1.0
* Update: Refactor and add multithreading to improve performance.  
* Bugfix: Fix recipe of non-productive product not set to production speedup mode when using assembler setting.  

### v1.0.18
* Add: config `Overwrite Stacking Level`, `Overwrite Proliferator Level`

### v1.0.17
* Update: Fix for game version 0.10.28.21150  

### v1.0.16 
* Update: Support for DSP Dark Fog update. (0.10.28.21014) 
* Bugfix: Fix production rate is not precise when displayed in per second. 

<details>
<summary>Previous Changelog</summary>

### v1.0.15 
* Update: add translated readme provided by Ximu-Luya on Github (thanks for contribution)  

### v1.0.14 
* Update: add zhCn translations provided by Ximu-Luya on Github (thanks for contribution)  

### v1.0.13 
* Update: change fractionator theoretical max calculation to account for stacked belts & spray 

### v1.0.12 
* Update: adjust item tooltip to get rid of cannot craft in replicator message 

### v1.0.11 
* Update: add item tooltip when item icon is hovered in stats window (disable with "Disable Item Hover Tip" config property) 

### v1.0.10 
* Bugfix: Fix bug with planet filtering when matched planet count is smaller than 2 
* Update: Add config property to disable filtering of planet list by precursor/consumer target
* Update: Add config property to control whether 2nd level precursor/consumers are shown

### v1.0.9
* Update: Add support for Nebula (thanks starfi5h) 
* Bugfix: Fix issue where Local System is added to astro list twice 

### v1.0.8
* Update: change so that when pre-cursor or successor filter is enabled, planet list is filtered to only planets that are producers or consumers of the item
* Add "Local System" to planet dropdown

### v1.0.7
* Update: update to sync with latest changes in game. 

### v1.0.6
* Bugfix: fix labs not detecting stacking condition 

### v1.0.5
* Bugfix: fix detection of non-productive assembler recipe default mode. Assemblers for antimatter treated as if they supported productivity mode 

### v1.0.4
* Bugfix: fix initialization issue with enhanced stats version

### v1.0.3
* Bugfix: resolve issue with initialization of Proliferator info when using BetterStats official was enabled

### v1.0.2
* Update: combined stats collection with bottleneck calculations
* Update: added 'Disable Bottleneck' config to allow only BetterStats functionality to be used. Removes precursors, made on, etc.
* Update: added detection for unsprayed items in bottleneck calculation

### v1.0.1
* Bugfix: handle modded items that are created after this plugin is initted

### v1.0.0
* Update: removed dependency on BetterStats. Now when that plugin is not installed a local fork of it will be used instead 
* Update: Account for usage of proliferator in local BetterStats fork
* Update: Detect stacking for Ray Receivers generating critical photons

</details>