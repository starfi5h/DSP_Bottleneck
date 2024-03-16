using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Bootstrap;
using Bottleneck.Nebula;
using Bottleneck.Stats;
using Bottleneck.UI;
using Bottleneck.Util;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Bottleneck
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("dsp.nebula-multiplayer-api", BepInDependency.DependencyFlags.SoftDependency)]

    public class BottleneckPlugin : BaseUnityPlugin
    {
        public static BottleneckPlugin Instance => instance;
        private Harmony _harmony;
        private static BottleneckPlugin instance;
        private GameObject _enablePrecursorGO;
        private Image _precursorCheckBoxImage;
        private static readonly Texture2D filterTexture = Resources.Load<Texture2D>("ui/textures/sprites/icons/filter-icon-16");
        private GameObject _enablePrecursorTextGO;
        private readonly HashSet<int> _itemFilter = new();
        private readonly Dictionary<int, PlanetaryProductionSummary> _productionLocations = new();
        private readonly List<GameObject> objsToDestroy = new();

        private readonly Dictionary<UIProductEntry, BottleneckProductEntryElement> _uiElements = new();
        private int _targetItemId = -1;
        private static bool _successor;
        private bool _deficientOnlyMode;
        private GameObject _textGo;
        private Button _btn;
        private Sprite _filterSprite;
        private bool _enableMadeOn;
        private readonly Dictionary<UIButton, FilterButtonItemAge> _buttonTipAge = new();
        private BetterStats _betterStatsObj;

        public bool IsFactoryDataDirty { get; set; }
        private int lastAstroFilter;
        private bool isCalculating;

        private void Awake()
        {
            Log.logger = Logger;
            instance = this;
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            _harmony.PatchAll(typeof(BottleneckPlugin));
            _harmony.PatchAll(typeof(Strings));
            _harmony.PatchAll(typeof(ResearchTechHelper));
            PluginConfig.InitConfig(Config);
            _betterStatsObj = gameObject.AddComponent<BetterStats>();
            Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} {PluginInfo.PLUGIN_VERSION} is loaded!");

            if (Chainloader.PluginInfos.ContainsKey("dsp.nebula-multiplayer"))
            {
                NebulaCompat.Init(_harmony);
            }
        }

        public void ProcessMadeOnTask()
        {
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
            var uiStatsWindow = UIRoot.instance.uiGame.statWindow;
            if (NebulaCompat.IsClient && uiStatsWindow.astroFilter != 0)
            {
                if (uiStatsWindow.astroFilter != NebulaCompat.LastAstroFilter)
                    NebulaCompat.SendRequest(ERequest.Bottleneck);
                return;
            }

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

#if DEBUG
        internal void OnDestroy()
        {
            // For ScriptEngine hot-reloading
            _itemFilter.Clear();
            _targetItemId = -1;
            _productionLocations.Clear();

            if (_enablePrecursorGO != null)
            {
                Destroy(_enablePrecursorTextGO);
                Destroy(_enablePrecursorGO);
            }

            if (_textGo != null)
            {
                Destroy(_textGo);
            }

            if (_precursorCheckBoxImage != null)
            {
                Destroy(_precursorCheckBoxImage.gameObject);
            }

            if (_btn != null && _btn.gameObject != null)
                Destroy(_btn.gameObject);

            Clear();
            _harmony.UnpatchSelf();
            if (_betterStatsObj != null)
            {
                Destroy(_betterStatsObj);
            }

            NebulaCompat.OnDestroy();
        }
#endif

        private void Clear()
        {
            foreach (var obj in objsToDestroy)
            {
                Destroy(obj);
            }

            objsToDestroy.Clear();

            foreach (BottleneckProductEntryElement element in _uiElements.Values)
            {
                if (element.precursorButton != null)
                    Destroy(element.precursorButton.gameObject);
                if (element.successorButton != null)
                    Destroy(element.successorButton.gameObject);
            }

            _uiElements.Clear();
        }

        [HarmonyPrefix, HarmonyPatch(typeof(UIStatisticsWindow), nameof(UIStatisticsWindow.ValueToAstroBox)), HarmonyPriority(Priority.Low)]
        public static void UIStatisticsWindow__ValueToAstroBox_Postfix(UIStatisticsWindow __instance)
        {
            if (!__instance.isStatisticsTab || NebulaCompat.IsClient)
                return;
            if (!PluginConfig.planetFilter.Value)
                return;

            var instanceAstroBox = __instance.astroBox;
            if (!__instance.isDysonTab && __instance.gameData.localPlanet != null && instanceAstroBox.Items.Count > 2)
            {
                int starId = __instance.gameData.localStar.id;
                if (instanceAstroBox.Items[2] != Strings.LocalSystemLabel)
                {
                    instanceAstroBox.Items.Insert(2, Strings.LocalSystemLabel);
                    instanceAstroBox.ItemsData.Insert(2, starId * 100);
                }
            }

            if (instance._itemFilter.Count == 0) return;

            var newItems = new List<string>();
            var newItemData = new List<int>();
            var currentSystemId = -1;
            var currentSystemName = "";
            for (int i = 0; i < instanceAstroBox.Items.Count; i++)
            {
                var astroId = instanceAstroBox.ItemsData[i];
                if (astroId <= 0)
                {
                    // gotta keep
                    newItemData.Add(astroId);
                    newItems.Add(instanceAstroBox.Items[i]);
                }
                else if (astroId % 100 == 0)
                {
                    // hide star systems, unless we get a hit for one of stars in system
                    currentSystemId = astroId;
                    var starName = UIRoot.instance.uiGame.statWindow.gameData.galaxy.StarById(astroId / 100).displayName;
                    currentSystemName = starName + "空格行星系".Translate();
                }
                else
                {
                    var planetData = GameMain.data.galaxy.PlanetById(astroId);
                    if (planetData != null)
                    {
                        if (instance._productionLocations.TryGetValue(instance._targetItemId, out var locationSummary))
                        {
                            if ((_successor && locationSummary.IsConsumerPlanet(planetData.id))
                                || (!_successor && locationSummary.IsProducerPlanet(planetData.id)))
                            {
                                // keep, but first add system
                                if (currentSystemId > 0 && PluginConfig.systemFilter.Value)
                                {
                                    newItemData.Add(currentSystemId);
                                    newItems.Add(currentSystemName);
                                    currentSystemId = -1;
                                }
                                newItemData.Add(astroId);
                                newItems.Add(instanceAstroBox.Items[i]);
                            }
                        }
                    }
                }
            }

            lock (__instance.astroBox.Items)
            {
                __instance.astroBox.Items.Clear();
                __instance.astroBox.ItemsData.Clear();
                __instance.astroBox.Items.AddRange(newItems);
                __instance.astroBox.ItemsData.AddRange(newItemData);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIStatisticsWindow), nameof(UIStatisticsWindow._OnClose)), HarmonyPriority(Priority.Low)]
        public static void UIStatisticsWindow__OnClose_Postfix()
        {
            BetterStats.UIStatisticsWindow__OnClose_Postfix();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIStatisticsWindow), "_OnOpen"), HarmonyPriority(Priority.Low)]
        public static void UIStatisticsWindow__OnOpen_Postfix(UIStatisticsWindow __instance)
        {
            BetterStats.UIStatisticsWindow__OnOpen_Postfix(__instance);
            if (instance.gameObject != null && !PluginConfig.statsOnly.Value)
            {
                instance.AddEnablePrecursorFilterButton(__instance);
                instance._enableMadeOn = false;
                instance.IsFactoryDataDirty = true;
                if (NebulaCompat.IsClient)
                    NebulaCompat.SendRequest(ERequest.Open);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIProductEntryList), "FilterEntries")]
        public static void UIProductEntryList_FilterEntries_Postfix(UIProductEntryList __instance)
        {
            if (BetterStats.filterStr != "")
            {
                var itemsToShow = instance.GetItemsToShow(__instance);
                BetterStats.UIProductEntryList_FilterEntries_Postfix(__instance, itemsToShow);
                return;
            }

            instance.FilterEntries(__instance);
        }


        [HarmonyPrefix, HarmonyPatch(typeof(UIStatisticsWindow), nameof(UIStatisticsWindow._OnUpdate))]
        public static void UIStatisticsWindow__OnUpdate_Prefix(UIStatisticsWindow __instance)
        {
            BetterStats.UIStatisticsWindow__OnUpdate_Prefix(__instance);
            if (!PluginConfig.statsOnly.Value)
                instance.UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            if (_btn == null || _btn.gameObject == null || _textGo == null)
                return;
            if (_targetItemId == -1)
            {
                _btn.gameObject.SetActive(false);
                _textGo.SetActive(false);
            }
            else
            {
                _btn.gameObject.SetActive(true);
                _textGo.SetActive(true);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIProductEntry), "_OnUpdate"), HarmonyPriority(Priority.Low)]
        public static void UIProductEntry__OnUpdate_Postfix(UIProductEntry __instance)
        {
            BetterStats.UIProductEntry__OnUpdate_Postfix(__instance);
            if (!PluginConfig.statsOnly.Value)
                instance.OnUpdate(__instance);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(UIStatisticsWindow), nameof(UIStatisticsWindow.ComputeDisplayProductEntries))]
        public static void UIProductionStatWindow_ComputeDisplayEntries_Prefix(UIStatisticsWindow __instance)
        {
            if (__instance == null)
                return;
            if (instance._betterStatsObj != null && PluginConfig.statsOnly.Value)
                BetterStats.UIProductionStatWindow_ComputeDisplayProductEntries_Prefix(__instance);
            else
            {
                instance.RecordEntryData(__instance);
            }
        }

        private void RecordEntryData(UIStatisticsWindow uiStatsWindow)
        {
            //bool planetUsageMode = Time.frameCount % 500 == 0;
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

        private void OnUpdate(UIProductEntry productEntry)
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

        private void ClearFilter()
        {
            _itemFilter.Clear();
            _targetItemId = -1;
            UIRoot.instance.uiGame.statWindow.RefreshAstroBox();
            SetFilterHighLight(-1, false);
        }

        private void SetFilterHighLight(int itemId, bool successor)
        {
            foreach (var pair in _uiElements)
            {
                if (pair.Value.precursorButton != null)
                    pair.Value.precursorButton.highlighted = !successor && pair.Key.entryData?.itemId == itemId;
                if (pair.Value.successorButton != null)
                    pair.Value.successorButton.highlighted = successor && pair.Key.entryData?.itemId == itemId;
            }
        }

        private BottleneckProductEntryElement EnhanceElement(UIProductEntry productEntry)
        {
            var precursorButton = UI.Util.CopyButton(productEntry, productEntry.favoriteBtn1, new Vector2(120 + 47, 80), productEntry.entryData.itemId,
                _ => { UpdatePrecursorFilter(productEntry.entryData.itemId); }, _filterSprite);

            objsToDestroy.Add(precursorButton.gameObject);
            var successorButton = UI.Util.CopyButton(productEntry, productEntry.favoriteBtn1, new Vector2(120 + 47, 0), productEntry.entryData.itemId,
                _ => { UpdatePrecursorFilter(productEntry.entryData.itemId, true); }, _filterSprite);
            objsToDestroy.Add(successorButton.gameObject);
            var result = new BottleneckProductEntryElement
            {
                precursorButton = precursorButton,
                successorButton = successorButton
            };

            _uiElements.Add(productEntry, result);

            return result;
        }

        private void UpdatePrecursorFilter(int itemId, bool successor = false)
        {
            _itemFilter.Clear();
            _itemFilter.Add(itemId);
            _targetItemId = itemId;
            _successor = successor;
            _deficientOnlyMode = VFInput.control;

            if (!successor)
            {
                var directPrecursorItems = ItemUtil.DirectPrecursorItems(itemId);
                foreach (var directPrecursorItem in directPrecursorItems)
                {
                    _itemFilter.Add(directPrecursorItem);
                }

                if (PluginConfig.includeSecondLevelConsumerProducer.Value)
                {
                    foreach (var directPrecursorItem in directPrecursorItems)
                    {
                        var grandParentItems = ItemUtil.DirectPrecursorItems(directPrecursorItem);
                        foreach (var gpItem in grandParentItems)
                        {
                            _itemFilter.Add(gpItem);
                        }
                    }
                }
            }
            else
            {
                var successorItems = ItemUtil.DirectSuccessorItems(itemId);
                foreach (var successorItem in successorItems)
                {
                    _itemFilter.Add(successorItem);
                }

                if (PluginConfig.includeSecondLevelConsumerProducer.Value)
                {
                    foreach (var successorItem in successorItems)
                    {
                        var grandChildItems = ItemUtil.DirectSuccessorItems(successorItem);
                        foreach (var gcItem in grandChildItems)
                        {
                            _itemFilter.Add(gcItem);
                        }
                    }
                }
            }

            UIRoot.instance.uiGame.statWindow.RefreshAstroBox();
            BetterStats.filterStr = "";
        }

        private HashSet<int> GetItemsToShow(UIProductEntryList uiProductEntryList)
        {
            var result = new HashSet<int>();
            if (_itemFilter.Count == 0) return result;
            for (int pIndex = uiProductEntryList.entryDatasCursor - 1; pIndex >= 0; --pIndex)
            {
                UIProductEntryData entryData = uiProductEntryList.entryDatas[pIndex];

                var hideItem = !_itemFilter.Contains(entryData.itemId);
                if (_deficientOnlyMode && entryData.itemId != _targetItemId)
                {
                    hideItem = !ProductionDeficit.IsDeficitItemFor(entryData.itemId, _targetItemId);
                }

                if (!hideItem)
                {
                    result.Add(entryData.itemId);
                }
            }

            return result;
        }

        private void FilterEntries(UIProductEntryList uiProductEntryList)
        {
            if (_itemFilter.Count == 0) return;
            for (int pIndex = uiProductEntryList.entryDatasCursor - 1; pIndex >= 0; --pIndex)
            {
                UIProductEntryData entryData = uiProductEntryList.entryDatas[pIndex];

                var hideItem = !_itemFilter.Contains(entryData.itemId);
                if (_deficientOnlyMode && entryData.itemId != _targetItemId)
                {
                    hideItem = !ProductionDeficit.IsDeficitItemFor(entryData.itemId, _targetItemId);
                }

                // hide the filtered item by moving it to the cursor location and decrementing cursor by one
                if (hideItem)
                {
                    uiProductEntryList.Swap(pIndex, uiProductEntryList.entryDatasCursor - 1);
                    --uiProductEntryList.entryDatasCursor;
                }
            }

            SetFilterHighLight(_targetItemId, _successor);
        }

        private void AddEnablePrecursorFilterButton(UIStatisticsWindow uiStatisticsWindow)
        {
            if (_enablePrecursorGO != null)
                return;
            _filterSprite = Sprite.Create(filterTexture, new Rect(0, 0, filterTexture.width * 0.75f, filterTexture.height * 0.75f), new Vector2(0.5f, 0.5f));
            _enablePrecursorGO = new GameObject("enablePrecursor");
            RectTransform rect = _enablePrecursorGO.AddComponent<RectTransform>();
            rect.SetParent(uiStatisticsWindow.productSortBox.transform.parent, false);

            rect.anchorMax = new Vector2(0, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(20, 20);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(350, -33);
            objsToDestroy.Add(rect.gameObject);
            _btn = rect.gameObject.AddComponent<Button>();
            _btn.onClick.AddListener(ClearFilter);

            _precursorCheckBoxImage = _btn.gameObject.AddComponent<Image>();
            _precursorCheckBoxImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
            _precursorCheckBoxImage.sprite = _filterSprite;


            _enablePrecursorTextGO = new GameObject("enablePrecursorText");
            RectTransform rectTxt = _enablePrecursorTextGO.AddComponent<RectTransform>();

            rectTxt.SetParent(_enablePrecursorGO.transform, false);

            rectTxt.anchorMax = new Vector2(0, 0.5f);
            rectTxt.anchorMin = new Vector2(0, 0.5f);
            rectTxt.sizeDelta = new Vector2(100, 20);
            rectTxt.pivot = new Vector2(0, 0.5f);
            rectTxt.anchoredPosition = new Vector2(20, 0);
            objsToDestroy.Add(rectTxt.gameObject);
            Text text = rectTxt.gameObject.AddComponent<Text>();
            text.text = Strings.ClearFilterLabel;
            text.fontStyle = FontStyle.Normal;
            text.fontSize = 12;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.color = new Color(0.8f, 0.8f, 0.8f, 1);
            Font fnt = Resources.Load<Font>("ui/fonts/SAIRASB");
            if (fnt != null)
                text.font = fnt;
            _textGo = text.gameObject;
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
                ref var fractionator = ref factorySystem .fractionatorPool[i];
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
                ref var silo = ref factorySystem .siloPool[i];
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
                var generator = planetFactory.powerSystem.genPool[i];
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
                BetterStats.RecordSprayCoaterStats(planetFactory);
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
}
