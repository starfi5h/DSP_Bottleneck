using System.Collections.Generic;
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
    public partial class BottleneckPlugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "Bottleneck";

        public static BottleneckPlugin Instance => instance;
        private Harmony _harmony;
        private static BottleneckPlugin instance;
        private GameObject _enablePrecursorGO;
        private Image _precursorCheckBoxImage;
        private static readonly Texture2D filterTexture = Resources.Load<Texture2D>("ui/textures/sprites/icons/filter-icon-16");
        private GameObject _enablePrecursorTextGO;
        private readonly HashSet<int> _itemFilter = new();
        private readonly List<GameObject> objsToDestroy = new();

        private readonly Dictionary<UIProductEntry, BottleneckProductEntryElement> _uiElements = new();
        private int _targetItemId = -1;
        private static bool _successor;
        private bool _deficientOnlyMode;
        private GameObject _textGo;
        private Button _btn;
        private Sprite _filterSprite;
        private BetterStats _betterStatsObj;


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

#if DEBUG
            Strings.ApplyLanguageChange();
#endif
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

        [HarmonyPrefix, HarmonyPriority(Priority.Low)]
        [HarmonyPatch(typeof(UIStatisticsWindow), nameof(UIStatisticsWindow.ValueToAstroBox))]
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

        [HarmonyPostfix, HarmonyPriority(Priority.Low)]
        [HarmonyPatch(typeof(UIStatisticsWindow), nameof(UIStatisticsWindow._OnClose))]
        public static void UIStatisticsWindow__OnClose_Postfix()
        {
            BetterStats.UIStatisticsWindow__OnClose_Postfix();
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Low)]
        [HarmonyPatch(typeof(UIStatisticsWindow), nameof(UIStatisticsWindow._OnOpen))]
        public static void UIStatisticsWindow__OnOpen_Postfix(UIStatisticsWindow __instance)
        {
            BetterStats.UIStatisticsWindow__OnOpen_Postfix(__instance);
            if (instance.gameObject != null && !PluginConfig.statsOnly.Value)
            {
                instance.AddEnablePrecursorFilterButton(__instance);
                // If it is client, set UI tip enable to true so it doesn't wait on local calculation
                instance._enableMadeOn = NebulaCompat.IsClient;
                instance.IsFactoryDataDirty = true;
                NebulaCompat.OnWindowOpen();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIStatisticsWindow), nameof(UIStatisticsWindow.OnTabButtonClick))]
        public static void UIStatisticsWindow_OnTabButtonClick_Postfix(UIStatisticsWindow __instance)
        {
            BetterStats.UIStatisticsWindow_OnTabButtonClick_Postfix(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIProductEntryList), nameof(UIProductEntryList.FilterEntries))]
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIKillEntryList), nameof(UIKillEntryList.FilterEntries))]
        public static void UIKillEntryList_FilterEntries_Postfix(UIKillEntryList __instance)
        {
            if (BetterStats.filterStr != "")
            {
                BetterStats.UIKillEntryList_FilterEntries_Postfix(__instance);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIStatisticsWindow), nameof(UIStatisticsWindow._OnUpdate))]
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

        [HarmonyPostfix, HarmonyPriority(Priority.Low)]
        [HarmonyPatch(typeof(UIProductEntry), nameof(UIProductEntry._OnUpdate))]
        public static void UIProductEntry__OnUpdate_Postfix(UIProductEntry __instance)
        {
            BetterStats.UIProductEntry__OnUpdate_Postfix(__instance);
            if (!PluginConfig.statsOnly.Value)
                instance.OnUpdateEnhance(__instance);
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Low)]
        [HarmonyPatch(typeof(UIKillEntry), nameof(UIKillEntry._OnUpdate))]
        public static void UIKillEntry__OnUpdate_Postfix(UIKillEntry __instance)
        {
            BetterStats.UIKillEntry__OnUpdate_Postfix(__instance);
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
            var precursorButton = UI.Util.CopyButton(productEntry, productEntry.favoriteBtn1, new Vector2(120 + 47, 60), productEntry.entryData.itemId,
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
            if (_targetItemId == itemId && _successor == successor && _deficientOnlyMode == VFInput.control)
            {
                // If the input parameter is exactly the same, clear the filter
                ClearFilter();
                return;
            }

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

    }
}
