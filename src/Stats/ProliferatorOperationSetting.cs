﻿using System;
using System.Collections.Generic;
using Bottleneck.Nebula;
using Bottleneck.UI;
using Bottleneck.Util;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Object;

namespace Bottleneck.Stats
{
    /// <summary>
    /// Manages operation of proliferator modes
    /// </summary>
    public class ProliferatorOperationSetting
    {
        private UIButton _normalOperationButton;
        private UIButton _disableButton;
        private UIButton _forceSpeedButton;
        private UIButton _forceProductivityButton;
        private readonly List<UIButton> _availableButtons = new();
        private int _productId;

        private static readonly Dictionary<ItemCalculationMode, Sprite> OperationModeSprites = new();
        private static Sprite checkboxOffSprite;
        private static Sprite checkboxOnSprite;
        private static readonly Dictionary<UIProductEntry, ProliferatorOperationSetting> ByProductEntry = new();

        private ProliferatorOperationSetting()
        {
        }

        public static void Init()
        {
            InitSprites();
            ItemCalculationRuntimeSetting.InitConfig();
        }


        private static void InitSprites()
        {
            var productivityTexture = Resources.Load<Texture2D>("ui/textures/sprites/icons/plus");
            var normalProlifTexture = Resources.Load<Texture2D>("ui/textures/sprites/icons/voxel-icon");
            var speedTexture = Resources.Load<Texture2D>("ui/textures/sprites/sci-fi/arrow-mark-60px");
            var checkBoxOff = Resources.Load<Texture2D>("ui/textures/sprites/icons/checkbox-off");
            var checkBoxOn = Resources.Load<Texture2D>("ui/textures/sprites/icons/checkbox-on");
            OperationModeSprites[ItemCalculationMode.ForceSpeed] = Sprite.Create(speedTexture,
                new Rect(0, 0, speedTexture.width, speedTexture.height),
                new Vector2(0.5f, 0.5f));

            OperationModeSprites[ItemCalculationMode.ForceProductivity] = Sprite.Create(productivityTexture,
                new Rect(0, 0, productivityTexture.width, productivityTexture.height),
                new Vector2(0.5f, 0.5f));

            checkboxOnSprite = Sprite.Create(checkBoxOn,
                new Rect(0, 0, checkBoxOn.width, checkBoxOn.height),
                new Vector2(0.5f, 0.5f));
            checkboxOffSprite = Sprite.Create(checkBoxOff,
                new Rect(0, 0, checkBoxOff.width, checkBoxOff.height),
                new Vector2(0.5f, 0.5f));
            OperationModeSprites[ItemCalculationMode.Normal] = Sprite.Create(normalProlifTexture,
                new Rect(0, 0, normalProlifTexture.width, normalProlifTexture.height),
                new Vector2(0.5f, 0.5f));
        }

        public static ProliferatorOperationSetting ForProductEntry(UIProductEntry uiProductEntry)
        {
            if (!ResearchTechHelper.IsProliferatorUnlocked())
                return null;
            var itemId = uiProductEntry.entryData.itemId;
            ProliferatorOperationSetting result;

            if (ByProductEntry.ContainsKey(uiProductEntry))
            {
                result = ByProductEntry[uiProductEntry];
                result._productId = uiProductEntry.entryData.itemId;
                result.SyncButtons();
                return result;
            }

            result = new ProliferatorOperationSetting();
            ByProductEntry[uiProductEntry] = result;

            result._productId = uiProductEntry.entryData.itemId;

            var sourceButton = uiProductEntry.favoriteBtn1;
            var xOffset = 80;

            result._disableButton = CopyButton(uiProductEntry, sourceButton, new Vector2(xOffset, 85),
                result.OnModeDisable, itemId, checkboxOnSprite,
                Strings.ProliferatorCalculationDisabled,
                Strings.ProliferatorCalculationDisabledHover);
            result._availableButtons.Add(result._disableButton);

            result._normalOperationButton = CopyButton(uiProductEntry, sourceButton, new Vector2(xOffset, 60),
                result.OnNormalClicked, itemId, OperationModeSprites[ItemCalculationMode.Normal],
                Strings.AssemblerSelectionMode,
                Strings.AssemblerSelectionHover);
            result._availableButtons.Add(result._normalOperationButton);

            result._forceSpeedButton = CopyButton(uiProductEntry, sourceButton, new Vector2(xOffset, 35),
                result.OnForceSpeedClicked, itemId, OperationModeSprites[ItemCalculationMode.ForceSpeed],
                Strings.ForceSpeedMode,
                Strings.ForceSpeedModeHover);
            result._availableButtons.Add(result._forceSpeedButton);

            result._forceProductivityButton = CopyButton(uiProductEntry, sourceButton, new Vector2(xOffset, 10),
                result.OnForceProductivity, itemId, OperationModeSprites[ItemCalculationMode.ForceProductivity],
                Strings.ForceProductivityMode,
                Strings.ForceProductivityHover);
            result._availableButtons.Add(result._forceProductivityButton);
            result.SyncButtons();
            return result;
        }

        private void OnForceProductivity(int itemId)
        {
            var setting = ItemCalculationRuntimeSetting.ForItemId(_productId);
            setting.Enabled = true;
            setting.Mode = ItemCalculationMode.ForceProductivity;
            OnRuntimeSettingChange();
        }

        private void OnForceSpeedClicked(int itemId)
        {
            // _productId = itemId;
            var setting = ItemCalculationRuntimeSetting.ForItemId(_productId);
            setting.Enabled = true;
            setting.Mode = ItemCalculationMode.ForceSpeed;
            OnRuntimeSettingChange();
        }

        private void OnNormalClicked(int itemId)
        {
            var setting = ItemCalculationRuntimeSetting.ForItemId(_productId);
            setting.Mode = ItemCalculationMode.Normal;
            OnRuntimeSettingChange();
        }

        private void OnModeDisable(int itemId)
        {
            var setting = ItemCalculationRuntimeSetting.ForItemId(_productId);
            Log.Debug($"switching item {itemId} to {!setting.Enabled} {setting.productId}");
            setting.Enabled = !setting.Enabled;
            if (setting.Enabled && setting.Mode == ItemCalculationMode.None)
            {
                setting.Mode = ItemCalculationMode.Normal;
            } // otherwise we'll just use whatever they had before disabling
            OnRuntimeSettingChange();
        }

        private void OnRuntimeSettingChange()
        {
            BottleneckPlugin.Instance.IsFactoryDataDirty = true;
            SyncButtons();
            if (NebulaCompat.IsClient)
                NebulaCompat.SendRequest(ERequest.BetterStats);
        }

        private void SyncButtons()
        {
            ReInitButtonStates();

            var runtimeSetting = ItemCalculationRuntimeSetting.ForItemId(_productId);

            if (!runtimeSetting.Enabled)
            {
                _disableButton.tips.tipTitle = Strings.ProliferatorCalculationDisabled;
                _disableButton.button.image.sprite = checkboxOffSprite;
            }
            else
            {
                _disableButton.tips.tipTitle = Strings.ProliferatorCalculationEnabled;
                _disableButton.button.image.sprite = checkboxOnSprite;
            }

            if (!runtimeSetting.Enabled)
            {
                foreach (var availableButton in _availableButtons)
                {
                    if (availableButton == _disableButton)
                        continue;
                    availableButton.gameObject
                        .SetActive(false);
                }
                return;
            }

            switch (runtimeSetting.Mode)
            {
                case ItemCalculationMode.Normal:
                {
                    _normalOperationButton.tips.tipTitle = $"({Strings.CurrentLabel}) {Strings.AssemblerSelectionMode}";
                    _normalOperationButton.highlighted = true;
                    break;
                }
                case ItemCalculationMode.ForceProductivity:
                {
                    _forceProductivityButton.tips.tipTitle = $"({Strings.CurrentLabel}) {Strings.ForceProductivityMode}";
                    _forceProductivityButton.highlighted = true;
                    break;
                }
                case ItemCalculationMode.ForceSpeed:
                {
                    _forceSpeedButton.tips.tipTitle = $"({Strings.CurrentLabel}) {Strings.ForceSpeedMode}";
                    _forceSpeedButton.highlighted = true;
                    break;
                }
            }
        }

        private void ReInitButtonStates()
        {
            var runtimeSetting = ItemCalculationRuntimeSetting.ForItemId(_productId);
            if (!runtimeSetting.SpeedSupported && !runtimeSetting.ProductivitySupported)
            {
                foreach (var button in _availableButtons)
                {
                    button.gameObject.SetActive(false);
                }
                return;
            }

            if (_forceProductivityButton != null)
            {
                _forceProductivityButton.tips.tipTitle = Strings.ForceProductivityMode;
                _forceProductivityButton.highlighted = false;
                _forceProductivityButton.button.interactable = true;
                _forceProductivityButton.gameObject.SetActive(runtimeSetting.ProductivitySupported);
            }

            if (_forceSpeedButton != null)
            {
                _forceSpeedButton.tips.tipTitle = Strings.ForceSpeedMode;
                _forceSpeedButton.highlighted = false;
                _forceSpeedButton.button.interactable = true;
                _forceSpeedButton.gameObject.SetActive(runtimeSetting.SpeedSupported);
            }

            if (_disableButton != null)
            {
                _disableButton.tips.tipTitle = Strings.ProliferatorCalculationDisabled;
                _disableButton.highlighted = true;
                _disableButton.button.interactable = true;
                _disableButton.gameObject.SetActive(true);
            }

            if (_normalOperationButton != null)
            {
                _normalOperationButton.tips.tipTitle = Strings.AssemblerSelectionMode;
                _normalOperationButton.highlighted = false;
                _normalOperationButton.button.interactable = true;
                _normalOperationButton.gameObject.SetActive(true);
            }
        }

        private static UIButton CopyButton(UIProductEntry uiProductEntry,
            UIButton button,
            Vector2 positionDelta,
            Action<int> action,
            int productId,
            Sprite btnSprite,
            string buttonHoverTitle,
            string buttonHoverText)
        {
            var rectTransform = button.GetComponent<RectTransform>();
            var copied = Instantiate(rectTransform, uiProductEntry.transform, false);
            var copiedImage = copied.transform.GetComponent<Image>();
            copiedImage.sprite = btnSprite;
            copiedImage.fillAmount = 0;

            copied.anchorMin = rectTransform.anchorMin;
            copied.anchorMax = rectTransform.anchorMax;
            copied.sizeDelta = rectTransform.sizeDelta * 0.75f;
            copied.anchoredPosition = rectTransform.anchoredPosition + positionDelta;
            var newActionButton = copied.GetComponentInChildren<UIButton>();
            if (newActionButton != null)
            {
                newActionButton.tips.tipTitle = buttonHoverTitle;
                newActionButton.tips.tipText = buttonHoverText;
                newActionButton.button.onClick.RemoveAllListeners();
                newActionButton.button.onClick.AddListener(() => action.Invoke(productId));
                newActionButton.highlighted = false;
                newActionButton.Init();
            }

            return newActionButton;
        }

        public static void Unload()
        {
            foreach (var operationModeSprite in OperationModeSprites.Values)
            {
                Destroy(operationModeSprite);
            }
            foreach (var setting in ByProductEntry.Values)
            {
                DestroySetting(setting);
            }
        }

        private static void DestroySetting(ProliferatorOperationSetting setting)
        {
            try
            {
                if (setting._disableButton != null && setting._disableButton.gameObject != null)
                {
                    Destroy(setting._disableButton.gameObject);
                }

                if (setting._forceProductivityButton != null && setting._forceProductivityButton.gameObject != null)
                {
                    Destroy(setting._forceProductivityButton.gameObject);
                }

                if (setting._normalOperationButton != null && setting._normalOperationButton.gameObject != null)
                {
                    Destroy(setting._normalOperationButton.gameObject);
                }

                if (setting._forceSpeedButton != null && setting._forceSpeedButton.gameObject != null)
                {
                    Destroy(setting._forceSpeedButton.gameObject);
                }
            }
            catch (Exception e)
            {
                Log.Warn($"exception while disabling setting: {e.Message}");
            }
        }

        public void UpdateItemId(int newItemId)
        {
            if (_productId == newItemId)
            {
                // nothing to do
                return;
            }

            _productId = newItemId;
            SyncButtons();
        }

        private static readonly Dictionary<int, int> _chosenItemIdForRecipeId = new();

        public static ItemCalculationRuntimeSetting ForRecipe(int assemblerRecipeId)
        {
            if (_chosenItemIdForRecipeId.TryGetValue(assemblerRecipeId, out var chosenItemId))
            {
                return ItemCalculationRuntimeSetting.ForItemId(chosenItemId);
            }

            var recipeProto = LDB.recipes.Select(assemblerRecipeId);
            if (recipeProto == null) return ItemCalculationRuntimeSetting.None;
            chosenItemId = recipeProto.Results[0];
            if (recipeProto.Results.Length == 1)
            {
                _chosenItemIdForRecipeId[assemblerRecipeId] = chosenItemId;
                return ItemCalculationRuntimeSetting.ForItemId(chosenItemId);
            }

            // todo figure out better way to manage this
            _chosenItemIdForRecipeId[assemblerRecipeId] = chosenItemId;
            if (chosenItemId == 1120)
            {
                // hydrogen is kind of boring
                chosenItemId = recipeProto.Results[1];
            }
            Log.Debug($"chose {chosenItemId} for recipe {recipeProto.name} (total: {recipeProto.Results.Length})");
            return ItemCalculationRuntimeSetting.ForItemId(chosenItemId);
        }
    }
}
