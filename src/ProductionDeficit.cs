using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bottleneck.Stats;
using Bottleneck.UI;
using Bottleneck.Util;

namespace Bottleneck
{
    public class ProductionDeficitItem
    {
        public string RecipeName { get; set; }
        public int AssemblerCount { get; set; }
        public int LackingPowerCount { get; set; }
        public int JammedCount { get; set; }

        private readonly int[] needed = new int[10];
        private readonly int[] assemblersNeedingCount = new int[10];
        private readonly int[] assemblersMissingSprayCount = new int[10];
        private readonly string[] inputItemNames = new string[10];
        private readonly int[] inputItemId = new int[10];
        private readonly Dictionary<int, int> inputItemIndex = new();
        private int neededCount;
        private const double Threshold = 0.01;

        private static readonly Dictionary<int, Dictionary<int, ProductionDeficitItem>> _byItemByRecipeId = new();
        private static readonly Dictionary<int, ProductionDeficitItem> _byItemOnly = new();

        public void AddNeeded(int itemId, int count)
        {
            if (inputItemIndex.TryGetValue(itemId, out int index))
            {
                needed[index] += count;
                assemblersNeedingCount[index]++;
            }
        }

        public void AddMissingSpray(int inputItem, int count)
        {
            if (inputItemIndex.ContainsKey(inputItem))
            {
                assemblersMissingSprayCount[inputItemIndex[inputItem]] += count;
            }
        }

        public (string neededStr, string stackStr, string unpoweredStr, string unsprayStr) GetTopNeeded(int outputProductId)
        {
            if (AssemblerCount < 1) return ("", "", "", "");

            var neededMax = int.MinValue;
            var neededName = "";
            var secondNeededName = "";
            var assemblerNeedingCount = int.MinValue;
            var secondAssemblerNeedingCount = int.MinValue;
            var missingSprayCount = 0;
            var missingSprayName = "";
            var spraySetting = ItemCalculationRuntimeSetting.ForItemId(outputProductId);
            bool includeSpray = !PluginConfig.disableProliferatorCalc.Value && spraySetting.Enabled && spraySetting.Mode != ItemCalculationMode.None;
            for (int i = 0; i < neededCount; i++)
            {
                if (needed[i] > neededMax)
                {
                    neededMax = needed[i];
                    secondNeededName = neededName;
                    neededName = inputItemNames[i];
                    secondAssemblerNeedingCount = assemblerNeedingCount;
                    assemblerNeedingCount = assemblersNeedingCount[i];
                }

                if (includeSpray && assemblersMissingSprayCount[i] > 0 && assemblersMissingSprayCount[i] > missingSprayCount)
                {
                    missingSprayCount = assemblersMissingSprayCount[i];
                    missingSprayName = inputItemNames[i];
                }
            }

            var needingPercent = (double)assemblerNeedingCount / AssemblerCount;
            var stackingPercent = (double)JammedCount / AssemblerCount;
            var unpoweredPercent = (double)LackingPowerCount / AssemblerCount;
            var unsprayedPercent = (double)missingSprayCount / AssemblerCount;

            var neededStr = needingPercent < Threshold ? "" : $"{neededName} {needingPercent:P2}";
            var stackingStr = stackingPercent < Threshold ? "" : $"{stackingPercent:P2}";
            var unpoweredStr = unpoweredPercent < Threshold ? "" : $"{unpoweredPercent:P2}";
            var missingSprayStr = missingSprayCount > Threshold ? $"{missingSprayName} {unsprayedPercent:P2}" : "";

            if (!string.IsNullOrEmpty(secondNeededName) && (double)secondAssemblerNeedingCount / AssemblerCount > Threshold)
            {
                neededStr = $"{neededStr} (2nd: {secondNeededName})".Trim();
            }
            return (neededStr.Trim(), stackingStr.Trim(), unpoweredStr, missingSprayStr);
        }

        public HashSet<int> NeededItems()
        {
            var result = new HashSet<int>();

            for (int i = 0; i < neededCount; i++)
            {
                if (needed[i] > 0)
                {
                    // neededMax = needed[i];
                    var assemblerNeedingCount = assemblersNeedingCount[i];
                    var percent = (double)assemblerNeedingCount / AssemblerCount;
                    if (percent > 0.05)
                    {
                        result.Add(inputItemId[i]);
                    }
                }
            }
            return result;
        }

        public static List<ProductionDeficitItem> GetItemsById(int itemId)
        {
            return _byItemByRecipeId.TryGetValue(itemId, out var recipes)
                ? recipes.Values.ToList()
                : (_byItemOnly.TryGetValue(itemId, out var item) ? new List<ProductionDeficitItem> { item } : new List<ProductionDeficitItem>());
        }

        public static ProductionDeficitItem FromItem(int inputItemId, int outputItemId)
        {
            if (!_byItemOnly.TryGetValue(outputItemId, out ProductionDeficitItem item))
            {
                _byItemOnly[outputItemId] = item = new ProductionDeficitItem();
            }

            item.inputItemId[0] = inputItemId;
            item.inputItemIndex[inputItemId] = 0;
            item.needed[item.inputItemIndex[inputItemId]] = 0;
            item.RecipeName = "Ray receiver";
            return item;
        }

        public static ProductionDeficitItem FromItem(int itemId, in AssemblerComponent assemblerComponent)
        {
            var recipeId = assemblerComponent.recipeId;
            if (!_byItemByRecipeId.TryGetValue(itemId, out var byRecipe))
            {
                _byItemByRecipeId[itemId] = byRecipe = new Dictionary<int, ProductionDeficitItem>();
            }

            if (!byRecipe.TryGetValue(recipeId, out ProductionDeficitItem value))
            {
                var requiresLength = assemblerComponent.requires?.Length;
                var requires = requiresLength ?? 0;
                value = new ProductionDeficitItem
                {
                    neededCount = requires,
                    RecipeName = ItemUtil.GetRecipeName(recipeId),
                };
                if (assemblerComponent.requires != null && assemblerComponent.requires.Length > 0)
                    for (int i = 0; i < value.neededCount; i++)
                    {
                        var requiredItem = LDB.items.Select(assemblerComponent.requires[i]);
                        value.inputItemNames[i] = requiredItem.Name.Translate();
                        value.inputItemId[i] = requiredItem.ID;
                        value.inputItemIndex[assemblerComponent.requires[i]] = i;
                    }

                byRecipe[recipeId] = value;
            }

            return value;
        }

        public static ProductionDeficitItem FromItem(int itemId, in LabComponent assemblerComponent)
        {
            var recipeId = assemblerComponent.recipeId;
            if (!_byItemByRecipeId.TryGetValue(itemId, out var byRecipe))
            {
                _byItemByRecipeId[itemId] = byRecipe = new Dictionary<int, ProductionDeficitItem>();
            }

            if (!byRecipe.TryGetValue(recipeId, out ProductionDeficitItem value))
            {
                value = new ProductionDeficitItem
                {
                    neededCount = assemblerComponent.requires.Length,
                    RecipeName = LDB.recipes.Select(recipeId).Name.Translate()
                };
                for (int i = 0; i < value.neededCount; i++)
                {
                    var requiredItem = LDB.items.Select(assemblerComponent.requires[i]);
                    value.inputItemNames[i] = requiredItem.Name.Translate();
                    value.inputItemId[i] = requiredItem.ID;
                    value.inputItemIndex[assemblerComponent.requires[i]] = i;
                }

                byRecipe[recipeId] = value;
            }

            return value;
        }

        private void Clear()
        {
            Array.Clear(needed, 0, needed.Length);
            Array.Clear(assemblersNeedingCount, 0, assemblersNeedingCount.Length);
            Array.Clear(assemblersMissingSprayCount, 0, assemblersMissingSprayCount.Length);

            AssemblerCount = 0;
            JammedCount = 0;
            LackingPowerCount = 0;
        }

        public static void ClearCounts()
        {
            foreach (var itemId in _byItemByRecipeId.Keys)
            {
                var productionDeficitItems = GetItemsById(itemId);
                foreach (var deficitItem in productionDeficitItems)
                {
                    deficitItem.Clear();
                }
            }

            foreach (var itemId in _byItemOnly.Keys)
            {
                var deficitItem = _byItemOnly[itemId];
                deficitItem.Clear();
            }
        }
    }

    public static class ProductionDeficit
    {
        public static void Clear()
        {
            ProductionDeficitItem.ClearCounts();
        }

        public static string MostNeeded(int recipeProductId)
        {
            var result = new StringBuilder();
            var productionDeficitItems = ProductionDeficitItem.GetItemsById(recipeProductId);
            foreach (var deficitItem in productionDeficitItems)
            {
                var (neededStr, stackingStr, unpoweredStr, unsprayedStr) = deficitItem.GetTopNeeded(recipeProductId);

                if (neededStr.Length == 0 && stackingStr.Length == 0 && unpoweredStr.Length == 0 && unsprayedStr.Length == 0)
                    continue;
                if (result.Length > 0)
                    result.Append("\r\n");
                var tmpResultStr = new StringBuilder();
                if (neededStr.Length > 0)
                {
                    tmpResultStr.Append($"{Strings.NeedLabel}: {neededStr}");
                    if (stackingStr.Length > 0)
                        tmpResultStr.Append($", {Strings.StackingLabel}: {stackingStr}");
                    if (unpoweredStr.Length > 0)
                        tmpResultStr.Append($", {Strings.UnderPoweredLabel}: {unpoweredStr}");
                    if (unsprayedStr.Length > 0)
                        tmpResultStr.Append($", {Strings.MissingSprayLabel}: {unsprayedStr}");
                }
                else if (stackingStr.Length > 0)
                {
                    tmpResultStr.Append($"{Strings.StackingLabel}: {stackingStr}");
                    if (unpoweredStr.Length > 0)
                        tmpResultStr.Append($", {Strings.UnderPoweredLabel}: {unpoweredStr}");
                    if (unsprayedStr.Length > 0)
                        tmpResultStr.Append($", {Strings.MissingSprayLabel}: {unsprayedStr}");
                }
                else if (unpoweredStr.Length > 0)
                {
                    tmpResultStr.Append($"{Strings.UnderPoweredLabel}: {unpoweredStr}");
                }
                else
                {
                    tmpResultStr.Append($"{Strings.MissingSprayLabel}: {unsprayedStr}");
                }

                if (productionDeficitItems.Count > 1)
                {
                    result.Append($"{Strings.RecipePreText}: {deficitItem.RecipeName}, {tmpResultStr}");
                }
                else
                {
                    result.Append(tmpResultStr);
                }
            }

            return result.ToString();
        }

        private static readonly HashSet<int> _loggedLowPowerByPlanetId = new();

        public static void RecordDeficit(int itemId, in AssemblerComponent assembler, PlanetFactory planetFactory)
        {
            var maxIncLevel = ResearchTechHelper.GetMaxIncIndex();
            var item = ProductionDeficitItem.FromItem(itemId, assembler);
            var networkId = planetFactory.powerSystem.consumerPool[assembler.pcId].networkId;
            PowerNetwork powerNetwork = planetFactory.powerSystem.netPool[networkId];

            float ratio = powerNetwork == null || networkId <= 0 ? 1f : (float)powerNetwork.consumerRatio;
            if (ratio < 0.98f)
            {
                item.LackingPowerCount++;
                if (!_loggedLowPowerByPlanetId.Contains(planetFactory.planet.id))
                {
                    if (PluginConfig.popupLowPowerWarnings.Value)
                    {
                        Log.LogAndPopupMessage($"Planet '{planetFactory.planet.displayName}' low on power");
                        var assemblerPos = planetFactory.entityPool[assembler.entityId].pos;
                        Maths.GetLatitudeLongitude(assemblerPos, out int latd, out int latf, out int logd, out int logf,
                            out bool north, out bool _, out bool west, out bool _);
                        Log.Warn($"{latd}.{latf} {north}, {logd}.{logf} {west}");
                    }
                    else
                    {
                        Log.Warn($"Planet is low on power {planetFactory.planet.displayName}");
                    }

                    _loggedLowPowerByPlanetId.Add(planetFactory.planet.id);
                }
            }

            item.AssemblerCount++;
            for (int index = 0; index < assembler.requireCounts.Length; ++index)
            {
                if (assembler.served[index] < assembler.requireCounts[index])
                {
                    item.AddNeeded(assembler.requires[index], Math.Max(1, assembler.needs[index]));
                }

                if (assembler.incServed[index] < assembler.served[index] * maxIncLevel)
                {
                    item.AddMissingSpray(assembler.requires[index], 1);
                }
            }

            for (int i = 0; i < assembler.products.Length; i++)
            {
                if (assembler.produced[i] >= assembler.productCounts[i] * 8)
                {
                    item.JammedCount++;
                    break;
                }
            }
        }

        public static void RecordDeficit(int itemId, in LabComponent lab, PlanetFactory planetFactory)
        {
            var maxIncLevel = ResearchTechHelper.GetMaxIncIndex();
            var item = ProductionDeficitItem.FromItem(itemId, lab);
            item.AssemblerCount++;
            int networkId = planetFactory.powerSystem.consumerPool[lab.pcId].networkId;
            PowerNetwork powerNetwork = planetFactory.powerSystem.netPool[networkId];
            float ratio = powerNetwork == null || networkId <= 0 ? 1f : (float)powerNetwork.consumerRatio;
            if (ratio < 0.98f)
            {
                item.LackingPowerCount++;
                if (!_loggedLowPowerByPlanetId.Contains(planetFactory.planet.id))
                {
                    if (PluginConfig.popupLowPowerWarnings.Value)
                    {
                        Log.LogAndPopupMessage($"Planet '{planetFactory.planet.displayName}' low on power");
                    }
                    else
                    {
                        Log.Warn($"Planet is low on power {planetFactory.planet.displayName}");
                    }

                    _loggedLowPowerByPlanetId.Add(planetFactory.planet.id);
                }
            }

            for (int k = 0; k < lab.requires.Length; k++)
            {
                if (lab.served[k] < lab.requireCounts[k])
                {
                    item.AddNeeded(lab.requires[k], Math.Max(1, lab.needs[k]));
                }

                if (lab.incServed[k] < lab.served[k] * maxIncLevel)
                {
                    item.AddMissingSpray(lab.requires[k], 1);
                }
            }

            if (lab.time >= lab.timeSpend)
            {
                item.JammedCount++;
            }
        }

        public static bool IsDeficitItemFor(int precursorItem, int targetItem)
        {
            var productionDeficitItems = ProductionDeficitItem.GetItemsById(targetItem);
            // this is a list because there can be a most needed item for each recipe for a target production item
            foreach (var productionDeficitItem in productionDeficitItems)
            {
                var neededItems = productionDeficitItem.NeededItems();
                if (neededItems.Contains(precursorItem))
                {
                    return true;
                }
            }

            return false;
        }

        public static void RecordDeficit(int rayReceiverProductId, in PowerGeneratorComponent generator, PlanetFactory _)
        {
            var item = ProductionDeficitItem.FromItem(generator.catalystId, rayReceiverProductId);
            item.AssemblerCount++;

            if (generator.productCount > 5)
            {
                item.JammedCount++;
            }
        }
    }
}
