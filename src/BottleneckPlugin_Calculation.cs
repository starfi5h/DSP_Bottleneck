using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bottleneck.Nebula;
using Bottleneck.Stats;
using Bottleneck.UI;
using Bottleneck.Util;
using HarmonyLib;

namespace Bottleneck
{
    public partial class BottleneckPlugin
    {
        // The calculating part for count producters and generate PlanetaryProductionSummary

        internal readonly Dictionary<int, PlanetaryProductionSummary> _productionLocations = new(); // Result, use in ButtonTip
        private bool isCalculating;  // Is in the process of calculating task (multi-thread)
        private int lastAstroFilter; // To determinate if uiStatsWindow.astroFilter is changed
        internal bool _enableMadeOn; // Show ProductionSummary tip on the buttons
        public bool IsFactoryDataDirty { get; set; } // True if stat is just open, or proliferator settings change

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIStatisticsWindow), nameof(UIStatisticsWindow.ComputeDisplayProductEntries))]
        public static void UIProductionStatWindow_ComputeDisplayEntries_Prefix(UIStatisticsWindow __instance)
        {
            if (__instance == null) return;
            if (instance._betterStatsObj != null && PluginConfig.statsOnly.Value)
                BetterStats.UIProductionStatWindow_ComputeDisplayProductEntries_Prefix(__instance);
            else
                instance.RecordEntryData(__instance);
        }

        private void RecordEntryData(UIStatisticsWindow uiStatsWindow)
        {
            //bool planetUsageMode = Time.frameCount % 500 == 0;
            if (NebulaCompat.IsClient && uiStatsWindow.astroFilter != lastAstroFilter)
            {
                if (!_enableMadeOn)
                {
                    _productionLocations.Clear();
                    _enableMadeOn = true;
                    return;
                }
                lastAstroFilter = uiStatsWindow.astroFilter;
                if (uiStatsWindow.astroFilter != NebulaCompat.LastAstroFilter)
                    NebulaCompat.SendRequest(ERequest.Bottleneck);
                return;
            }
            if (isCalculating) return;

            if (!_enableMadeOn)
            {
                // Update when stat window open
                isCalculating = true;
                Task.Run(ProcessMadeOnTask);
            }
            else if (IsFactoryDataDirty || uiStatsWindow.astroFilter != lastAstroFilter)
            {
                // Update when settings or filter change
                isCalculating = true;
                lastAstroFilter = uiStatsWindow.astroFilter;
                IsFactoryDataDirty = false;
                Task.Run(ProcessDeficitTask);
            }
        }

        public void ProcessMadeOnTask()
        {
            isCalculating = true;
            _productionLocations.Clear();
            for (int i = 0; i < GameMain.data.factoryCount; i++)
            {
                AddPlanetFactoryData(GameMain.data.factories[i], true);
            }
            _enableMadeOn = true;
            isCalculating = false;
        }

        private void ProcessDeficitTask()
        {
            isCalculating = true;
            var uiStatsWindow = UIRoot.instance.uiGame.statWindow;
            ProductionDeficit.Clear();
            BetterStats.counter.Clear();

            if (uiStatsWindow.astroFilter == -1)
            {
                int factoryCount = uiStatsWindow.gameData.factoryCount;
                for (int i = 0; i < factoryCount; i++)
                {
                    AddPlanetFactoryData(uiStatsWindow.gameData.factories[i], false);
                }
            }
            else if (uiStatsWindow.astroFilter == 0)
            {
                if (uiStatsWindow.gameData.localPlanet.factory != null)
                {
                    AddPlanetFactoryData(uiStatsWindow.gameData.localPlanet.factory, false);
                }
            }
            else if (uiStatsWindow.astroFilter % 100 > 0)
            {
                PlanetData planetData = uiStatsWindow.gameData.galaxy.PlanetById(uiStatsWindow.astroFilter);
                if (planetData != null)
                    AddPlanetFactoryData(planetData.factory, false);
            }
            else if (uiStatsWindow.astroFilter % 100 == 0)
            {
                int starId = uiStatsWindow.astroFilter / 100;
                StarData starData = uiStatsWindow.gameData.galaxy.StarById(starId);
                for (int j = 0; j < starData.planetCount; j++)
                {
                    if (starData.planets[j].factory != null)
                    {
                        AddPlanetFactoryData(starData.planets[j].factory, false);
                    }
                }
            }
            isCalculating = false;
        }

        public void AddPlanetFactoryData(PlanetFactory planetFactory, bool planetUsage)
        {
            if (planetFactory == null) return;
            int beltMaxStack = ResearchTechHelper.GetMaxPilerStackingUnlocked();
            var factorySystem = planetFactory.factorySystem;
            var veinPool = planetFactory.planet.factory.veinPool;
            var waterItemId = planetFactory.planet.waterItemId;
            for (int i = 1; i < factorySystem.minerCursor; i++)
            {
                ref var miner = ref factorySystem.minerPool[i];
                if (i != miner.id) continue;
                if (!planetUsage)
                {
                    BetterStats.RecordMinerStats(miner, planetFactory, waterItemId);
                    continue;
                }
                var productId = miner.productId;
                var veinId = (miner.veinCount != 0) ? miner.veins[miner.currentVeinIndex] : 0;
                if (miner.type == EMinerType.Water)
                    productId = planetFactory.planet.waterItemId;
                else if (productId == 0)
                    productId = veinPool[veinId].productId;

                if (productId == 0) continue;
                AddPlanetaryUsage(productId, planetFactory.planet);
            }

            var maxProductivityIncrease = ResearchTechHelper.GetMaxProductivityIncrease();
            var maxSpeedIncrease = ResearchTechHelper.GetMaxSpeedIncrease();

            for (int i = 1; i < factorySystem.assemblerCursor; i++)
            {
                ref var assembler = ref factorySystem.assemblerPool[i];
                if (assembler.id != i || assembler.recipeId == 0) continue;

                if (planetUsage)
                {
                    foreach (var productId in assembler.requires)
                    {
                        AddPlanetaryUsage(productId, planetFactory.planet, true);
                    }
                }
                else
                {
                    BetterStats.RecordAssemblerStats(assembler, maxSpeedIncrease, maxProductivityIncrease);
                }

                foreach (var productId in assembler.products)
                {
                    if (planetUsage) AddPlanetaryUsage(productId, planetFactory.planet);
                    else ProductionDeficit.RecordDeficit(productId, assembler, planetFactory);
                }
            }

            for (int i = 1; i < factorySystem.fractionatorCursor; i++)
            {
                ref var fractionator = ref factorySystem.fractionatorPool[i];
                if (fractionator.id != i) continue;
                if (!planetUsage)
                {
                    BetterStats.RecordFractionatorStats(fractionator, maxSpeedIncrease, beltMaxStack);
                    continue;
                }

                if (fractionator.fluidId != 0)
                    AddPlanetaryUsage(fractionator.fluidId, planetFactory.planet, true);
                if (fractionator.productId != 0)
                    AddPlanetaryUsage(fractionator.productId, planetFactory.planet);
            }

            for (int i = 1; i < factorySystem.ejectorCursor; i++)
            {
                ref var ejector = ref factorySystem.ejectorPool[i];
                if (ejector.id != i) continue;
                if (!planetUsage)
                    BetterStats.RecordEjectorStats(ejector);
                else
                    AddPlanetaryUsage(ejector.bulletId, planetFactory.planet, true);
            }

            for (int i = 1; i < factorySystem.siloCursor; i++)
            {
                ref var silo = ref factorySystem.siloPool[i];
                if (silo.id != i) continue;
                if (!planetUsage)
                    BetterStats.RecordSiloStats(silo);
                else
                    AddPlanetaryUsage(silo.bulletId, planetFactory.planet, true);
            }

            for (int i = 1; i < factorySystem.labCursor; i++)
            {
                ref var lab = ref factorySystem.labPool[i];
                if (lab.id != i) continue;
                if (!planetUsage)
                {
                    BetterStats.RecordLabStats(lab, maxSpeedIncrease, maxProductivityIncrease);
                }

                if (lab.matrixMode)
                {
                    if (planetUsage)
                        foreach (var productId in lab.requires)
                        {
                            AddPlanetaryUsage(productId, planetFactory.planet, true);
                        }

                    foreach (var productId in lab.products)
                    {
                        if (planetUsage) AddPlanetaryUsage(productId, planetFactory.planet);
                        else ProductionDeficit.RecordDeficit(productId, lab, planetFactory);
                    }
                }
                else if (lab.researchMode && planetUsage && lab.techId > 0)
                {
                    var techProto = LDB.techs.Select(lab.techId);
                    for (int index = 0; index < techProto.itemArray.Length; ++index)
                    {
                        var item = techProto.Items[index];
                        AddPlanetaryUsage(item, planetFactory.planet, true);
                    }
                }
            }

            double gasTotalHeat = planetFactory.planet.gasTotalHeat;
            var collectorsWorkCost = planetFactory.transport.collectorsWorkCost;
            if (!planetUsage)
                for (int i = 1; i < planetFactory.transport.stationCursor; i++)
                {
                    var station = planetFactory.transport.stationPool[i];
                    BetterStats.RecordOrbitalCollectorStats(station, gasTotalHeat, collectorsWorkCost);
                }


            for (int i = 1; i < planetFactory.powerSystem.genCursor; i++)
            {
                ref var generator = ref planetFactory.powerSystem.genPool[i];
                if (generator.id != i) continue;

                if (!planetUsage)
                    BetterStats.RecordGeneratorStats(generator);

                var isFuelConsumer = generator.fuelHeat > 0 && generator.fuelId > 0 && generator.productId == 0;
                if ((generator.productId == 0 || generator.productHeat == 0) && !isFuelConsumer)
                {
                    continue;
                }

                if (isFuelConsumer)
                {
                    // account for fuel consumption by power generator
                    var productId = generator.fuelId;
                    if (planetUsage) AddPlanetaryUsage(productId, planetFactory.planet, true);
                }
                else
                {
                    var productId = generator.productId;
                    if (planetUsage) AddPlanetaryUsage(productId, planetFactory.planet);

                    if (generator.catalystId > 0)
                    {
                        if (planetUsage) AddPlanetaryUsage(productId, planetFactory.planet);
                        else // this should be critical photons 
                            ProductionDeficit.RecordDeficit(generator.productId, generator, planetFactory);
                    }
                }
            }

            if (!planetUsage)
            {
                BetterStats.RecordSprayCoaterStats(planetFactory, maxProductivityIncrease);
            }
            else
            {
                for (int i = 1; i < planetFactory.cargoTraffic.spraycoaterCursor; i++)
                {
                    ref var sprayCoater = ref planetFactory.cargoTraffic.spraycoaterPool[i];
                    if (sprayCoater.id != i || sprayCoater.incItemId < 1)
                        continue;
                    AddPlanetaryUsage(sprayCoater.incItemId, planetFactory.planet, true);
                }
            }
        }

        private void AddPlanetaryUsage(int productId, PlanetData planet, bool consumption = false)
        {
            // An entity can have multiple components
            if (!_productionLocations.ContainsKey(productId))
            {
                _productionLocations[productId] = new PlanetaryProductionSummary();
            }

            if (consumption)
                _productionLocations[productId].AddConsumption(planet.id, 1);
            else
                _productionLocations[productId].AddProduction(planet.id, 1);
        }
    }

    public partial class BottleneckPlugin
    {
        // The part of showing PlanetaryProductionSummary on filter button tip

        private readonly Dictionary<UIButton, FilterButtonItemAge> _buttonTipAge = new();

        private void OnUpdateEnhance(UIProductEntry productEntry)
        {
            if (productEntry.productionStatWindow == null || !productEntry.productionStatWindow.isProductionTab) return;

            var elt = GetEnhanceElement(productEntry);

            if (elt.precursorButton != null && ButtonOutOfDate(elt.precursorButton, productEntry.entryData.itemId))
            {
                int productId = productEntry.entryData.itemId;
                GetPrecursorButtonTip(productId, out elt.precursorButton.tips.tipTitle, out elt.precursorButton.tips.tipText);
                UpdateButtonUpdateDate(elt.precursorButton, productId);
            }

            if (elt.successorButton != null && ButtonOutOfDate(elt.successorButton, productEntry.entryData.itemId))
            {
                int productId = productEntry.entryData.itemId;
                GetSuccessorButtonTip(productId, out elt.successorButton.tips.tipTitle, out elt.successorButton.tips.tipText);
                UpdateButtonUpdateDate(elt.successorButton, productId);
            }
        }

        public BottleneckProductEntryElement GetEnhanceElement(UIProductEntry productEntry)
        {
            if (!_uiElements.TryGetValue(productEntry, out BottleneckProductEntryElement elt))
            {
                elt = EnhanceElement(productEntry);
            }

            return elt;
        }

        public void GetPrecursorButtonTip(int productId, out string tipTitle, out string tipText)
        {
            tipTitle = Strings.ProdDetailsLabel;
            tipText = "";

            if (NebulaCompat.IsClient)
            {
                tipText = "(...)";
                NebulaCompat.SendEntryRequest(productId, true);
                return;
            }

            if (ItemUtil.HasPrecursors(productId))
                tipTitle += Strings.ClickPrecursorText;
            if (_productionLocations.ContainsKey(productId))
            {
                if (_enableMadeOn)
                {
                    var parensMessage = ItemUtil.HasPrecursors(productId) ? Strings.ControlClickLacking : "";
                    var producedOnText = Strings.ProducedOnLabel;
                    tipText = $"{parensMessage}<b>{producedOnText}</b>\r\n" + _productionLocations[productId].GetProducerSummary();
                    if (_productionLocations[productId].ProducerPlanetCount() > PluginConfig.productionPlanetCount.Value)
                        tipTitle += $" (top {PluginConfig.productionPlanetCount.Value} / {_productionLocations[productId].ProducerPlanetCount()} planets)";
                }
                else
                {
                    tipText = "Calculating...";
                }

                var deficitItemName = ProductionDeficit.MostNeeded(productId);
                if (deficitItemName.Length > 0)
                    tipText += $"\r\n<b>{Strings.BottlenecksLabel}</b>\r\n{deficitItemName}";
            }
        }

        public void GetSuccessorButtonTip(int productId, out string tipTitle, out string tipText)
        {
            tipTitle = Strings.ConDetailsLabel;
            tipText = "";

            if (NebulaCompat.IsClient)
            {
                tipText = "(...)";
                NebulaCompat.SendEntryRequest(productId, false);
                return;
            }

            if (ItemUtil.HasConsumers(productId))
                tipTitle += Strings.ClickConsumingText;
            if (_productionLocations.ContainsKey(productId) && _enableMadeOn)
            {
                var consumedOnText = Strings.ConsumedOnLabel;

                tipText = $"<b>{consumedOnText}</b>\r\n" + _productionLocations[productId].GetConsumerSummary();
                if (_productionLocations[productId].ConsumerPlanetCount() > PluginConfig.productionPlanetCount.Value)
                    tipTitle += $" (top {PluginConfig.productionPlanetCount.Value} / {_productionLocations[productId].ConsumerPlanetCount()} planets)";
            }
        }

        private void UpdateButtonUpdateDate(UIButton uiButton, int productId)
        {
            _buttonTipAge[uiButton] = new FilterButtonItemAge(uiButton, productId)
            {
                lastUpdated = DateTime.Now
            };
        }

        private bool ButtonOutOfDate(UIButton uiButton, int entryDataItemId)
        {
            if (_buttonTipAge.TryGetValue(uiButton, out FilterButtonItemAge itemAge))
            {
                if (itemAge.itemId != entryDataItemId)
                    return true;
                if (NebulaCompat.IsClient)
                    return false;
                return (DateTime.Now - itemAge.lastUpdated).TotalSeconds > 4;
            }

            _buttonTipAge[uiButton] = new FilterButtonItemAge(uiButton, entryDataItemId)
            {
                lastUpdated = DateTime.Now
            };
            return true;
        }
    }
}
