using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Bottleneck.Nebula;
using Bottleneck.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#pragma warning disable IDE0017

// Adapted from https://github.com/DysonSphereMod/QOL/blob/master/BetterStats/BetterStats.cs
namespace Bottleneck.Stats
{
    public partial class BetterStats : MonoBehaviour
    {
        private class EnhancedUIProductEntryElements
        {
            public int itemId;
            private int tipItemId;
            public Text maxProductionLabel;
            public Text maxProductionValue;
            public Text maxProductionUnit;

            public Text maxConsumptionLabel;
            public Text maxConsumptionValue;
            public Text maxConsumptionUnit;

            public Text counterProductionLabel;
            public Text counterProductionValue;

            public Text counterConsumptionLabel;
            public Text counterConsumptionValue;
            public ProliferatorOperationSetting proliferatorOperationSetting;
            public EventTrigger trigger;
            public UIProductEntry ProductEntry { get; set; }

            public UIItemTip tip;

            public void OnMouseOverItem(BaseEventData _)
            {
                if (PluginConfig.disableItemHoverTip.Value)
                    return;
                if (ProductEntry == null)
                    return;
                if (tip != null)
                {
                    if (tipItemId != itemId)
                    {
                        Destroy(tip.gameObject);
                        tip = null;
                    }
                    else
                    {
                        tip.gameObject.SetActive(true);
                        return;
                    }
                }

                // corner=9 is ABOVE_RIGHT (relative position of numpad)
                tip = UIItemTip.Create(itemId, 9, Vector2.zero, 
                    ProductEntry.itemIcon.transform,
                    0, 0, UIButton.ItemTipType.Other);
                tipItemId = itemId;
            }

            public void OnMouseOffItem(BaseEventData _)
            {
                if (tip != null)
                {
                    Destroy(tip.gameObject);
                    tip = null;
                    tipItemId = -1;
                }
            }
        }

        public static Dictionary<int, ProductMetrics> counter = new();
        private static GameObject txtGO, chxGO, filterGO;
        private static readonly Texture2D texOff = Resources.Load<Texture2D>("ui/textures/sprites/icons/checkbox-off");
        private static readonly Texture2D texOn = Resources.Load<Texture2D>("ui/textures/sprites/icons/checkbox-on");
        private static Sprite sprOn;
        private static Sprite sprOff;
        private static Image checkBoxImage;

        public static string filterStr = "";

        private const int initialXOffset = 70;
        private const int valuesWidth = 90;
        private const int unitsWidth = 20;
        private const int labelsWidth = valuesWidth + unitsWidth;
        private const int margin = 10;
        private const int maxOffset = labelsWidth + margin;

        private static int lastTimeLevel;
        private static int lastAstroFilter;

        private static readonly Dictionary<UIProductEntry, EnhancedUIProductEntryElements> enhancements = new();
        private static UIStatisticsWindow statWindow;
        public static ManualLogSource Log;

        internal void Awake()
        {
            try
            {
                ProliferatorOperationSetting.Init();
            }
            catch (Exception e)
            {
                Log.LogWarning(e.ToString());
            }
        }

        internal void OnDestroy()
        {
            // For ScriptEngine hot-reloading
            if (txtGO != null)
            {
                Destroy(txtGO);
                Destroy(chxGO);
                Destroy(filterGO);
                Destroy(sprOn);
                Destroy(sprOff);
            }

            var favoritesLabel = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Production Stat Window/product-bg/top/favorite-text");
            if (favoritesLabel != null)
            {
                favoritesLabel.SetActive(true);
            }

            ClearEnhancedUIProductEntries();
            ProliferatorOperationSetting.Unload();
        }

        public class ProductMetrics
        {
            public float production;
            public float consumption;
            public int producers;
            public int consumers;
        }

        private static void ClearEnhancedUIProductEntries()
        {
            if (statWindow == null) return;

            foreach (EnhancedUIProductEntryElements enhancement in enhancements.Values)
            {
                Destroy(enhancement.maxProductionLabel.gameObject);
                Destroy(enhancement.maxProductionValue.gameObject);
                Destroy(enhancement.maxProductionUnit.gameObject);

                Destroy(enhancement.maxConsumptionLabel.gameObject);
                Destroy(enhancement.maxConsumptionValue.gameObject);
                Destroy(enhancement.maxConsumptionUnit.gameObject);

                Destroy(enhancement.counterProductionLabel.gameObject);
                Destroy(enhancement.counterProductionValue.gameObject);

                Destroy(enhancement.counterConsumptionLabel.gameObject);
                Destroy(enhancement.counterConsumptionValue.gameObject);
                enhancement.trigger.triggers.Clear();
            }

            enhancements.Clear();
        }

        private static Text CopyText(Text original, Vector2 positionDelta)
        {
            var copied = Instantiate(original);
            copied.transform.SetParent(original.transform.parent, false);
            var copiedRectTransform = copied.GetComponent<RectTransform>();
            var originalRectTransform = original.GetComponent<RectTransform>();

            copiedRectTransform.anchorMin = originalRectTransform.anchorMin;
            copiedRectTransform.anchorMax = originalRectTransform.anchorMax;
            copiedRectTransform.sizeDelta = originalRectTransform.sizeDelta;
            copiedRectTransform.anchoredPosition = originalRectTransform.anchoredPosition + positionDelta;

            return copied;
        }

        private static string FormatMetric(float value, bool skipFraction = false)
        {
            if (value >= 1000000.0)
                return (value / 1000000).ToString("F2") + " M";
            if (value >= 10000.0)
                return (value / 1000).ToString("F2") + " k";

            var fraction = value - (int)value;

            if (value >= 1000.0 || (skipFraction && fraction < 0.0001))
                return value.ToString("F0");
            if (value >= 100.0)
                return value.ToString("F1");
            if (value >= 1.0)
                return value.ToString("F2");
            if (value > 0.0)
                return value.ToString("F3");
            return value.ToString();
        }

        private static EnhancedUIProductEntryElements EnhanceUIProductEntry(UIProductEntry __instance)
        {
            var parent = __instance.itemIcon.transform.parent;
            parent.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 80);
            parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(22, 12);

            __instance.favoriteBtn1.GetComponent<RectTransform>().anchoredPosition = new Vector2(26, -32);
            __instance.favoriteBtn2.GetComponent<RectTransform>().anchoredPosition = new Vector2(49, -32);
            __instance.favoriteBtn3.GetComponent<RectTransform>().anchoredPosition = new Vector2(72, -32);
            __instance.itemName.transform.SetParent(parent, false);
            var itemNameRect = __instance.itemName.GetComponent<RectTransform>();

            itemNameRect.pivot = new Vector2(0.5f, 0f);
            itemNameRect.anchorMin = new Vector2(0, 0);
            itemNameRect.anchorMax = new Vector2(1f, 0);

            itemNameRect.anchoredPosition = new Vector2(0, 0);
            parent.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 80);

            __instance.itemName.resizeTextForBestFit = true;
            __instance.itemName.resizeTextMaxSize = 14;
            __instance.itemName.alignment = TextAnchor.MiddleCenter;
            __instance.itemName.alignByGeometry = true;
            __instance.itemName.horizontalOverflow = HorizontalWrapMode.Wrap;
            __instance.itemName.lineSpacing = 0.6f;

            var sepLine = __instance.consumeUnitLabel.transform.parent.Find("sep-line");
            sepLine.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            sepLine.GetComponent<RectTransform>().rotation = Quaternion.Euler(0f, 0f, 90f);
            sepLine.GetComponent<RectTransform>().sizeDelta = new Vector2(1, 336);
            sepLine.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -50);


            __instance.productLabel.alignment = TextAnchor.UpperRight;
            __instance.productLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(labelsWidth, 24);
            __instance.productLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(initialXOffset, 0);
            __instance.productLabel.GetComponent<RectTransform>().ForceUpdateRectTransforms();

            __instance.productText.alignByGeometry = true;
            __instance.productText.resizeTextForBestFit = true;
            __instance.productText.resizeTextMaxSize = 34;
            __instance.productText.alignment = TextAnchor.LowerRight;
            __instance.productText.GetComponent<RectTransform>().sizeDelta = new Vector2(valuesWidth, 40);
            __instance.productText.GetComponent<RectTransform>().anchoredPosition = new Vector2(initialXOffset, 56);

            __instance.productUnitLabel.alignByGeometry = true;
            __instance.productUnitLabel.alignment = TextAnchor.LowerLeft;
            __instance.productUnitLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(unitsWidth, 24);
            __instance.productUnitLabel.GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);
            __instance.productUnitLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(initialXOffset + valuesWidth + 4, -42);

            __instance.consumeLabel.alignment = TextAnchor.UpperRight;
            __instance.consumeLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(labelsWidth, 24);
            __instance.consumeLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(initialXOffset, -60);

            __instance.consumeText.alignByGeometry = true;
            __instance.consumeText.resizeTextForBestFit = true;
            __instance.consumeText.resizeTextMaxSize = 34;
            __instance.consumeText.alignment = TextAnchor.LowerRight;
            __instance.consumeText.GetComponent<RectTransform>().sizeDelta = new Vector2(valuesWidth, 40);
            __instance.consumeText.GetComponent<RectTransform>().anchoredPosition = new Vector2(initialXOffset, -4);

            __instance.consumeUnitLabel.alignByGeometry = true;
            __instance.consumeUnitLabel.alignment = TextAnchor.LowerLeft;
            __instance.consumeUnitLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(unitsWidth, 24);
            __instance.consumeUnitLabel.GetComponent<RectTransform>().anchorMin = new Vector2(0f, 0f);
            __instance.consumeUnitLabel.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 0f);
            __instance.consumeUnitLabel.GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);
            __instance.consumeUnitLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(initialXOffset + valuesWidth + 4, -4);

            var maxProductionLabel = CopyText(__instance.productLabel, new Vector2(maxOffset, 0));
            maxProductionLabel.text = Strings.TheoreticalMaxLabel; 
            var maxProductionValue = CopyText(__instance.productText, new Vector2(maxOffset, 0));
            maxProductionValue.text = "0";
            var maxProductionUnit = CopyText(__instance.productUnitLabel, new Vector2(maxOffset, 0));
            maxProductionUnit.text = Strings.PerMinLabel;

            var maxConsumptionLabel = CopyText(__instance.consumeLabel, new Vector2(maxOffset, 0));
            maxConsumptionLabel.text = Strings.TheoreticalMaxLabel;
            var maxConsumptionValue = CopyText(__instance.consumeText, new Vector2(maxOffset, 0));
            maxConsumptionValue.text = "0";
            var maxConsumptionUnit = CopyText(__instance.consumeUnitLabel, new Vector2(maxOffset, 0));
            maxConsumptionUnit.text = Strings.PerMinLabel;

            var counterProductionLabel = CopyText(__instance.productLabel, new Vector2(-initialXOffset, 0));
            counterProductionLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 40);
            counterProductionLabel.text = Strings.ProducersLabel;
            var counterProductionValue = CopyText(__instance.productText, new Vector2(-initialXOffset, 0));
            counterProductionValue.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 40);
            counterProductionValue.text = "0";

            var counterConsumptionLabel = CopyText(__instance.consumeLabel, new Vector2(-initialXOffset, 0));
            counterConsumptionLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 40);
            counterConsumptionLabel.text = Strings.ConsumersLabel;
            var counterConsumptionValue = CopyText(__instance.consumeText, new Vector2(-initialXOffset, 0));
            counterConsumptionValue.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 40);
            counterConsumptionValue.text = "0";
            var proliferatorOpSetting = ProliferatorOperationSetting.ForProductEntry(__instance);
            var enhancement = new EnhancedUIProductEntryElements
            {
                itemId = __instance.entryData.itemId,
                maxProductionLabel = maxProductionLabel,
                maxProductionValue = maxProductionValue,
                maxProductionUnit = maxProductionUnit,

                maxConsumptionLabel = maxConsumptionLabel,
                maxConsumptionValue = maxConsumptionValue,
                maxConsumptionUnit = maxConsumptionUnit,

                counterProductionLabel = counterProductionLabel,
                counterProductionValue = counterProductionValue,

                counterConsumptionLabel = counterConsumptionLabel,
                counterConsumptionValue = counterConsumptionValue,
                proliferatorOperationSetting = proliferatorOpSetting,
                ProductEntry = __instance,
            };

            __instance.itemIcon.raycastTarget = true;
            enhancement.trigger = __instance.itemIcon.gameObject.AddComponent<EventTrigger>();
            // var eventRectTrigger = eventTriggerItem.GetComponent<RectTransform>();
            // // eventRectTrigger.anchoredPosition = __instance.itemIcon.transform.position;
            // // eventRectTrigger.sizeDelta = new Vector2(100, 100);

            var enter = new EventTrigger.Entry();
            enter.eventID = EventTriggerType.PointerEnter;
            enter.callback.AddListener(enhancement.OnMouseOverItem);
            enhancement.trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry();
            exit.eventID = EventTriggerType.PointerExit;
            exit.callback.AddListener(enhancement.OnMouseOffItem);
            enhancement.trigger.triggers.Add(exit);

            enhancements.Add(__instance, enhancement);

            if (PluginConfig.fontSizeValue.Value > 0)
            {
                __instance.productText.fontSize
                    = __instance.consumeText.fontSize
                    = enhancement.counterProductionValue.fontSize
                    = enhancement.counterConsumptionValue.fontSize
                    = enhancement.maxProductionValue.fontSize
                    = enhancement.maxConsumptionValue.fontSize = PluginConfig.fontSizeValue.Value;
            }

            return enhancement;
        }

        #region UI patches

        public static void UIStatisticsWindow__OnClose_Postfix()
        {
            foreach (var element in enhancements.Values)
            {
                element.OnMouseOffItem(null);
            }
            lastTimeLevel = lastAstroFilter = -1;
        }

        public static void UIStatisticsWindow__OnOpen_Postfix(UIStatisticsWindow __instance)
        {
            if (statWindow == null)
            {
                statWindow = __instance;
            }

            if (chxGO == null)
            {
                CreateObjects(__instance);
            }
            UIStatisticsWindow_OnTabButtonClick_Postfix(__instance);
        }

        public static void UIStatisticsWindow_OnTabButtonClick_Postfix(UIStatisticsWindow __instance)
        {
            if (chxGO == null || filterGO == null) return;

            if (__instance.isProductionTab)
            {
                chxGO.transform.SetParent(__instance.productSortBox.transform.parent, false);
                filterGO.transform.SetParent(__instance.productSortBox.transform.parent, false);
            }
            else if (__instance.isKillTab)
            {
                chxGO.transform.SetParent(__instance.killSortBox.transform.parent, false);
                filterGO.transform.SetParent(__instance.killSortBox.transform.parent, false);
            }
        }
        private static void CreateObjects(UIStatisticsWindow __instance)
        {
            var favoritesLabel = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Statistics Window/product-bg/top/favorite-text");
            if (favoritesLabel != null) favoritesLabel.SetActive(false);
            favoritesLabel = GameObject.Find("UI Root/Overlay Canvas/In Game/Windows/Statistics Window/kill-bg/top/favorite-text");
            if (favoritesLabel != null) favoritesLabel.SetActive(false);

            sprOn = Sprite.Create(texOn, new Rect(0, 0, texOn.width, texOn.height), new Vector2(0.5f, 0.5f));
            sprOff = Sprite.Create(texOff, new Rect(0, 0, texOff.width, texOff.height), new Vector2(0.5f, 0.5f));

            chxGO = new GameObject("displaySec");

            RectTransform rect = chxGO.AddComponent<RectTransform>();
            rect.SetParent(__instance.productSortBox.transform.parent, false);

            rect.anchorMax = new Vector2(0, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(20, 20);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(250, -33);

            Button _btn = rect.gameObject.AddComponent<Button>();
            _btn.onClick.AddListener(() =>
            {
                PluginConfig.displayPerSecond.Value = !PluginConfig.displayPerSecond.Value;
                checkBoxImage.sprite = PluginConfig.displayPerSecond.Value ? sprOn : sprOff;
            });

            checkBoxImage = _btn.gameObject.AddComponent<Image>();
            checkBoxImage.color = new Color(0.8f, 0.8f, 0.8f, 1);

            checkBoxImage.sprite = PluginConfig.displayPerSecond.Value ? sprOn : sprOff;


            txtGO = new GameObject("displaySecTxt");
            RectTransform rectTxt = txtGO.AddComponent<RectTransform>();

            rectTxt.SetParent(chxGO.transform, false);

            rectTxt.anchorMax = new Vector2(0, 0.5f);
            rectTxt.anchorMin = new Vector2(0, 0.5f);
            rectTxt.sizeDelta = new Vector2(100, 20);
            rectTxt.pivot = new Vector2(0, 0.5f);
            rectTxt.anchoredPosition = new Vector2(20, 0);

            Text text = rectTxt.gameObject.AddComponent<Text>();
            text.text = Strings.DispPerSecLabel;
            text.fontStyle = FontStyle.Normal;
            text.fontSize = 14;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.color = new Color(0.8f, 0.8f, 0.8f, 1);
            Font fnt = Resources.Load<Font>("ui/fonts/SAIRASB");
            if (fnt != null)
                text.font = fnt;

            filterGO = new GameObject("filterGo");
            RectTransform rectFilter = filterGO.AddComponent<RectTransform>();

            rectFilter.SetParent(__instance.productSortBox.transform.parent, false);

            rectFilter.anchorMax = new Vector2(0, 1);
            rectFilter.anchorMin = new Vector2(0, 1);
            rectFilter.sizeDelta = new Vector2(100, 30);
            rectFilter.pivot = new Vector2(0, 0.5f);
            rectFilter.anchoredPosition = new Vector2(120, -33);

            var _image = filterGO.AddComponent<Image>();
            _image.transform.SetParent(rectFilter, false);
            _image.color = new Color(0f, 0f, 0f, 0.5f);

            var textContainer = new GameObject();
            textContainer.name = "Text";
            textContainer.transform.SetParent(rectFilter, false);
            var _text = textContainer.AddComponent<Text>();
            _text.supportRichText = false;
            _text.color = new Color(0.8f, 0.8f, 0.8f, 1);
            _text.font = fnt;
            _text.fontSize = 16;
            _text.alignment = TextAnchor.MiddleLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            (_text.transform as RectTransform).sizeDelta = new Vector2(90, 30);
            (_text.transform as RectTransform).anchoredPosition = new Vector2(5, 0);

            var placeholderContainer = new GameObject();
            placeholderContainer.name = "Placeholder";
            placeholderContainer.transform.SetParent(rectFilter, false);
            var _placeholder = placeholderContainer.AddComponent<Text>();
            _placeholder.color = new Color(0.8f, 0.8f, 0.8f, 1);
            _placeholder.font = fnt;
            _placeholder.fontSize = 16;
            _placeholder.fontStyle = FontStyle.Italic;
            _placeholder.alignment = TextAnchor.MiddleLeft;
            _placeholder.supportRichText = false;
            _placeholder.horizontalOverflow = HorizontalWrapMode.Overflow;
            _placeholder.text = Strings.FilterLabel;
            (_placeholder.transform as RectTransform).sizeDelta = new Vector2(90, 30);
            (_placeholder.transform as RectTransform).anchoredPosition = new Vector2(5, 0);

            var _inputField = filterGO.AddComponent<InputField>();
            _inputField.transform.SetParent(rectFilter, false);
            _inputField.targetGraphic = _image;
            _inputField.textComponent = _text;
            _inputField.placeholder = _placeholder;


            _inputField.onValueChanged.AddListener(value =>
            {
                // taken from thecodershome's PR on github: https://github.com/DysonSphereMod/QOL/pull/128
                if (_inputField.wasCanceled)
                {
                    // When escape key is pressed keep the current value. The default behavior was to reset/restore value to the previous submitted text
                    _inputField.text = filterStr;
                }
                else
                {
                    filterStr = value;
                }

                if (__instance.isProductionTab)
                {
                    __instance.ComputeDisplayProductEntries();
                }
                else if (__instance.isKillTab)
                {
                    __instance.ComputeDisplayKillEntries();
                }
            });
            // taken from thecodershome's PR on github: https://github.com/DysonSphereMod/QOL/pull/128
            _inputField.onEndEdit.AddListener(value =>
            {
                // Reset focus to allow pressing escape key to close production panel after entering value into filter inputField
                EventSystem.current.SetSelectedGameObject(null);
            });

            chxGO.transform.SetParent(__instance.productSortBox.transform.parent, false);
            txtGO.transform.SetParent(chxGO.transform, false);
            filterGO.transform.SetParent(__instance.productSortBox.transform.parent, false);
        }

        public static void UIProductEntryList_FilterEntries_Postfix(UIProductEntryList __instance, HashSet<int> itemsToShow)
        {
            if (filterStr == "") return;
            var uiProductEntryList = __instance;
            for (int pIndex = uiProductEntryList.entryDatasCursor - 1; pIndex >= 0; --pIndex)
            {
                UIProductEntryData entryData = uiProductEntryList.entryDatas[pIndex];
                var proto = LDB.items.Select(entryData.itemId);
                if (proto.name.IndexOf(filterStr, StringComparison.OrdinalIgnoreCase) < 0 && !itemsToShow.Contains(entryData.itemId))
                {
                    uiProductEntryList.Swap(pIndex, uiProductEntryList.entryDatasCursor - 1);
                    --uiProductEntryList.entryDatasCursor;
                }
            }
        }

        public static void UIKillEntryList_FilterEntries_Postfix(UIKillEntryList __instance)
        {
            if (filterStr == "") return;
            var uiKillEntryList = __instance;
            for (int kIndex = uiKillEntryList.entryDatasCursor - 1; kIndex >= 0; --kIndex)
            {
                var entryData = uiKillEntryList.entryDatas[kIndex];
                var proto = LDB.models.Select(entryData.modelId);
                if (proto.displayName.IndexOf(filterStr, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    uiKillEntryList.Swap(kIndex, uiKillEntryList.entryDatasCursor - 1);
                    --uiKillEntryList.entryDatasCursor;
                }
            }
        }
        public static void UIStatisticsWindow__OnUpdate_Prefix(UIStatisticsWindow __instance)
        {
            if (statWindow == null)
            {
                statWindow = __instance;
            }
        }

        public static void UIProductEntry__OnUpdate_Postfix(UIProductEntry __instance)
        {
            if (__instance.productionStatWindow == null || !__instance.productionStatWindow.isProductionTab) return;

            if (!enhancements.TryGetValue(__instance, out EnhancedUIProductEntryElements enhancement))
            {
                enhancement = EnhanceUIProductEntry(__instance);
            }

            enhancement.itemId = __instance.entryData.itemId;

            bool isTotalTimeWindow = __instance.productionStatWindow.timeLevel == 5;

            float lvDivisor = isTotalTimeWindow ? 1f : (float)__instance.lvDivisors[__instance.productionStatWindow.timeLevel];
            float originalProductValue = __instance.entryData.production / lvDivisor;
            float originalConsumeValue = __instance.entryData.consumption / lvDivisor;

            string producers = "0";
            string consumers = "0";
            string maxProduction = "0";
            string maxConsumption = "0";
            string unitRate = PluginConfig.displayPerSecond.Value ? Strings.PerSecLabel : Strings.PerMinLabel;
            string unit = isTotalTimeWindow ? "" : Strings.PerMinLabel;
            int divider = 1;
            bool alertOnLackOfProduction = false;
            bool warnOnHighMaxConsumption = false;
            bool highlightOnMaxProduction = false;
            bool highlightOnMaxConsumption = false;

            //add values per second
            if (PluginConfig.displayPerSecond.Value)
            {
                divider = 60;
                unit = !isTotalTimeWindow ? Strings.PerSecLabel : unit;

                if (!isTotalTimeWindow)
                {
                    originalProductValue /= divider;
                    originalConsumeValue /= divider;
                    __instance.productText.text = FormatMetric(originalProductValue);
                    __instance.consumeText.text = FormatMetric(originalConsumeValue);
                }
            }

            __instance.productUnitLabel.text = __instance.consumeUnitLabel.text = unit;
            enhancement.maxProductionUnit.text = enhancement.maxConsumptionUnit.text = unitRate;
            enhancement.maxProductionUnit.color = enhancement.maxConsumptionUnit.color = __instance.productUnitLabel.color;

            if (counter.TryGetValue(__instance.entryData.itemId, out var productMetrics) && productMetrics != null)
            {
                float maxProductValue = productMetrics.production / divider;
                float maxConsumeValue = productMetrics.consumption / divider;
                maxProduction = FormatMetric(maxProductValue, true);
                maxConsumption = FormatMetric(maxConsumeValue, true);

                producers = productMetrics.producers.ToString();
                consumers = productMetrics.consumers.ToString();

                if (originalConsumeValue > (maxProductValue * PluginConfig.lackOfProductionRatioTrigger.Value))
                    alertOnLackOfProduction = true;

                if (maxConsumeValue > (maxProductValue * PluginConfig.consumptionToProductionRatioTrigger.Value))
                    warnOnHighMaxConsumption = true;

                if (originalProductValue > maxProductValue * PluginConfig.maximumReachRatioTrigger.Value)
                    highlightOnMaxProduction = true;

                if (originalConsumeValue > maxConsumeValue * PluginConfig.maximumReachRatioTrigger.Value)
                    highlightOnMaxConsumption = true;
            }

            enhancement.maxProductionValue.text = maxProduction;
            enhancement.maxConsumptionValue.text = maxConsumption;

            enhancement.counterProductionValue.text = producers;
            enhancement.counterConsumptionValue.text = consumers;

            enhancement.maxProductionValue.color = enhancement.counterProductionValue.color = __instance.productColor;
            enhancement.maxConsumptionValue.color = enhancement.counterConsumptionValue.color = __instance.consumeColor;

            if (!isTotalTimeWindow)
            {
                if (alertOnLackOfProduction)
                    enhancement.maxProductionValue.color = __instance.consumeText.color = new Color(1f, .25f, .25f, .5f);

                if (warnOnHighMaxConsumption)
                    enhancement.maxConsumptionValue.color = new Color(1f, 1f, .25f, .5f);

                if (highlightOnMaxProduction)
                    enhancement.maxProductionUnit.color = __instance.productColor;

                if (highlightOnMaxConsumption)
                    enhancement.maxConsumptionUnit.color = __instance.consumeColor;
            }
            enhancement.proliferatorOperationSetting?.UpdateItemId(__instance.entryData.itemId);
        }

        public static void UIKillEntry__OnUpdate_Postfix(UIKillEntry __instance)
        {
            if (__instance.productionStatWindow == null || !__instance.productionStatWindow.isKillTab) return;

            var timeLevel = __instance.productionStatWindow.timeLevel;
            if (timeLevel >= 5) return; // total time window
            
            if (PluginConfig.displayPerSecond.Value)
            {
                // Modify from UIKillEntry.ShowInText
                var originalValue = __instance.entryData.kill / (float)__instance.lvDivisors[timeLevel];
                __instance.killText.text = FormatMetric(originalValue / 60.0f);
                __instance.killUnitLabel.text = Strings.PerSecLabel;
            }
            else
            {
                __instance.killUnitLabel.text = Strings.PerMinLabel;
            }
        }

        // statsOnly
        public static void UIProductionStatWindow_ComputeDisplayProductEntries_Prefix(UIStatisticsWindow __instance)
        {
            if (lastAstroFilter == __instance.lastAstroFilter && lastTimeLevel == __instance.timeLevel)
            {
                // No need to refresh stat data if the filter condition is same
                return;
            }
            lastAstroFilter = __instance.lastAstroFilter;
            lastTimeLevel = __instance.timeLevel;

            if (NebulaCompat.IsClient && __instance.astroFilter != 0)
            {
                if (__instance.astroFilter != NebulaCompat.LastAstroFilter)
                    NebulaCompat.SendRequest(ERequest.BetterStats);
                return;
            }

            counter.Clear();

            if (__instance.astroFilter == -1)
            {
                int factoryCount = __instance.gameData.factoryCount;
                for (int i = 0; i < factoryCount; i++)
                {
                    AddPlanetFactoryData(__instance.gameData.factories[i]);
                }
            }
            else if (__instance.astroFilter == 0)
            {
                if (__instance.gameData.localPlanet.factory != null)
                {
                    AddPlanetFactoryData(__instance.gameData.localPlanet.factory);
                }
            }
            else if (__instance.astroFilter % 100 > 0)
            {
                PlanetData planetData = __instance.gameData.galaxy.PlanetById(__instance.astroFilter);
                AddPlanetFactoryData(planetData.factory);
            }
            else if (__instance.astroFilter % 100 == 0)
            {
                int starId = __instance.astroFilter / 100;
                StarData starData = __instance.gameData.galaxy.StarById(starId);
                for (int j = 0; j < starData.planetCount; j++)
                {
                    if (starData.planets[j].factory != null)
                    {
                        AddPlanetFactoryData(starData.planets[j].factory);
                    }
                }
            }
        }

        #endregion
    }
}