using HarmonyLib;

namespace Bottleneck.Stats
{
    public static class ResearchTechHelper
    {
        private static float maxProductivityIncrease;
        private static float maxSpeedIncrease;
        private static int maxIncIndex;
        private static int maxPilerStacking;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameMain), nameof(GameMain.Begin))]
        [HarmonyPatch(typeof(GameHistoryData), nameof(GameHistoryData.NotifyTechUnlock))]
        public static void RefreshTech()
        {
            if (PluginConfig.overwriteProliferatorLevel.Value >= 0)
            {
                maxPilerStacking = PluginConfig.overwriteProliferatorLevel.Value;
            }
            else
            {
                maxPilerStacking = 4;
                maxPilerStacking = GetMaxPilerStackingUnlocked(); //mod compatiblity
            }

            if (PluginConfig.overwriteProliferatorLevel.Value >= 0)
            {
                maxIncIndex = PluginConfig.overwriteProliferatorLevel.Value;
            }
            else if (GameMain.history != null)
            {
                if (GameMain.history.TechUnlocked(1153))
                    maxIncIndex = 4;
                else if (GameMain.history.TechUnlocked(1152))
                    maxIncIndex = 2;
                else if (GameMain.history.TechUnlocked(1151))
                    maxIncIndex = 1;
                else
                    maxIncIndex = 0;
                maxIncIndex = GetMaxIncIndex(); //ProjectGenesis compatiblity
            }
            maxProductivityIncrease = (float)Cargo.incTableMilli[maxIncIndex];
            maxSpeedIncrease = (float)Cargo.accTableMilli[maxIncIndex];
        }

        public static float GetMaxProductivityIncrease()
        {
            return maxProductivityIncrease;
        }

        public static float GetMaxSpeedIncrease()
        {
            return maxSpeedIncrease;
        }

        public static int GetMaxIncIndex()
        {
            return maxIncIndex;
        }

        public static bool IsProliferatorUnlocked()
        {
            return GetMaxIncIndex() > 0;
        }

        public static int GetMaxPilerStackingUnlocked()
        {
            return maxPilerStacking;
        }
    }
}
