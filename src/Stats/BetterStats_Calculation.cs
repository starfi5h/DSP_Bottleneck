using System.Collections.Generic;

namespace Bottleneck.Stats
{
    public partial class BetterStats
    {
        // Count & Theoretical max calculation
        // This part of calculation will be used by BottleneckPlugin_Calculation.cs

        private static void EnsureId(ref Dictionary<int, ProductMetrics> dict, int id)
        {
            if (!dict.ContainsKey(id))
            {
                dict.Add(id, new ProductMetrics());
            }
        }

        // speed of fastest belt(mk3 belt) is 1800 items per minute, with 4 stack = 7200 per minute
        public const float TICKS_PER_SEC = 60.0f;
        private const float RAY_RECEIVER_GRAVITON_LENS_CONSUMPTION_RATE_PER_MIN = 0.1f;

        public static void AddPlanetFactoryData(PlanetFactory planetFactory)
        {
            if (planetFactory == null) return;
            var factorySystem = planetFactory.factorySystem;
            var transport = planetFactory.transport;
            var maxProductivityIncrease = ResearchTechHelper.GetMaxProductivityIncrease();
            var maxSpeedIncrease = ResearchTechHelper.GetMaxSpeedIncrease();
            int beltMaxStack = ResearchTechHelper.GetMaxPilerStackingUnlocked();

            if (PluginConfig.disableProliferatorCalc.Value)
            {
                maxProductivityIncrease = 0f;
                maxSpeedIncrease = 0f;
            }

            var waterItemId = planetFactory.planet.waterItemId;
            for (int i = 1; i < factorySystem.minerCursor; i++)
            {
                ref var miner = ref factorySystem.minerPool[i];
                if (i != miner.id) continue;
                RecordMinerStats(miner, planetFactory, waterItemId);
            }

            for (int i = 1; i < factorySystem.assemblerCursor; i++)
            {
                ref var assembler = ref factorySystem.assemblerPool[i];
                RecordAssemblerStats(assembler, maxSpeedIncrease, maxProductivityIncrease);
            }

            for (int i = 1; i < factorySystem.fractionatorCursor; i++)
            {
                ref var fractionator = ref factorySystem.fractionatorPool[i];
                RecordFractionatorStats(fractionator, maxSpeedIncrease, beltMaxStack);
            }

            for (int i = 1; i < factorySystem.ejectorCursor; i++)
            {
                ref var ejector = ref factorySystem.ejectorPool[i];
                if (ejector.id != i) continue;
                RecordEjectorStats(ejector);
            }

            for (int i = 1; i < factorySystem.siloCursor; i++)
            {
                ref var silo = ref factorySystem.siloPool[i];
                if (silo.id != i) continue;
                RecordSiloStats(silo);
            }

            for (int i = 1; i < factorySystem.labCursor; i++)
            {
                ref var lab = ref factorySystem.labPool[i];
                if (lab.id != i) continue;
                RecordLabStats(lab, maxSpeedIncrease, maxProductivityIncrease);
            }

            double gasTotalHeat = planetFactory.planet.gasTotalHeat;
            var collectorsWorkCost = transport.collectorsWorkCost;
            for (int i = 1; i < transport.stationCursor; i++)
            {
                var station = transport.stationPool[i];
                RecordOrbitalCollectorStats(station, gasTotalHeat, collectorsWorkCost);
            }

            for (int i = 1; i < planetFactory.powerSystem.genCursor; i++)
            {
                ref var generator = ref planetFactory.powerSystem.genPool[i];
                if (generator.id != i) continue;
                RecordGeneratorStats(generator);
            }

            for (int i = 1; i < planetFactory.powerSystem.excCursor; i++)
            {
                ref var exchanger = ref planetFactory.powerSystem.excPool[i];
                if (exchanger.id != i) continue;
                RecordPowerExchangerStats(exchanger, maxSpeedIncrease);
            }

            RecordSprayCoaterStats(planetFactory, maxProductivityIncrease);
        }

        public static void RecordSprayCoaterStats(PlanetFactory planetFactory, float maxProductivityIncrease)
        {
            var cargoTraffic = planetFactory.cargoTraffic;
            for (int i = 0; i < planetFactory.cargoTraffic.spraycoaterCursor; i++)
            {
                ref var sprayCoater = ref cargoTraffic.spraycoaterPool[i];
                if (sprayCoater.id != i || sprayCoater.incItemId < 1)
                    continue;
                ItemProto itemProto = LDB.items.Select(sprayCoater.incItemId);
                var beltComponent = cargoTraffic.beltPool[sprayCoater.cargoBeltId];
                // Belt running at 6 / s transports 360 cargos in 1 minute
                // Tooltip for spray lvl 1 shows: "Numbers of sprays = 12", which means that
                // each spray covers 12 cargos so 360 / 12 = 30 items are covered per minute
                // (HpMax from proto == Numbers of Sprays)
                // Assume proliferator is already sprayed
                var numbersOfSprays = (int)(itemProto.HpMax * (1 + maxProductivityIncrease));

                // BeltComponent.speed is 1,2,5 so must be multiplied by 6 to get 6,12,30 (cargo/s)
                // For mk3 belt, max cargo infeed speed = 1800(beltRatePerMin) * 4(beltMaxStack) = 7200/min
                // The mk3 spray usage = 7200 / (60+15)(numbersOfSprays) = 96/min
                // Note: Practically infeed speed rarely reach belt limit, so the theory max is often much higher than the real rate
                var beltRatePerMin = (6 * beltComponent.speed) * 60;
                int beltMaxStack = ResearchTechHelper.GetMaxPilerStackingUnlocked();
                var frequency = beltMaxStack * beltRatePerMin / (float)numbersOfSprays;
                var productId = sprayCoater.incItemId;
                EnsureId(ref counter, productId);

                counter[productId].consumption += frequency;
                counter[productId].consumers++;
            }
        }

        public static void RecordLabStats(in LabComponent lab, float maxSpeedIncrease, float maxProductivityIncrease)
        {
            (float baseFrequency, float productionFrequency) = DetermineLabFrequencies(in lab, maxProductivityIncrease, maxSpeedIncrease);

            if (lab.matrixMode)
            {
                for (int j = 0; j < lab.requires.Length; j++)
                {
                    var productId = lab.requires[j];
                    EnsureId(ref counter, productId);
                    counter[productId].consumption += baseFrequency * lab.requireCounts[j];
                    counter[productId].consumers++;
                }

                for (int j = 0; j < lab.products.Length; j++)
                {
                    var productId = lab.products[j];
                    EnsureId(ref counter, productId);
                    counter[productId].production += productionFrequency * lab.productCounts[j];
                    counter[productId].producers++;
                }
            }
            else if (lab.researchMode && lab.techId > 0)
            {
                // In this mode we can't just use lab.timeSpend to figure out how long it takes to consume 1 item (usually a cube)
                // So, we figure out how many hashes a single cube represents and use the research mode research speed to come up with what is basically a research rate
                // Note: maxProductivityIncrease only increase the amount of hash upload, it doesn't affect cube consumption rate
                var techProto = LDB.techs.Select(lab.techId);
                if (techProto == null) return;
                float hashPerMinute = 60.0f * GameMain.data.history.techSpeed;

                for (int index = 0; index < techProto.itemArray.Length; ++index)
                {                    
                    var item = techProto.Items[index];
                    var researchRateSec = (float)GameMain.history.techSpeed * GameMain.tickPerSec;
                    var researchFreq = (float)(techProto.ItemPoints[index] * hashPerMinute / researchRateSec);
                    EnsureId(ref counter, item);
                    counter[item].consumers++;
                    counter[item].consumption += researchFreq * GameMain.history.techSpeed;
                }
            }
        }

        public static void RecordSiloStats(in SiloComponent silo)
        {
            EnsureId(ref counter, silo.bulletId);
            counter[silo.bulletId].consumption += PluginConfig.siloSpeedFactor.Value * 60f / (silo.chargeSpend + silo.coldSpend) * 600000f;
            counter[silo.bulletId].consumers++;
        }

        public static void RecordEjectorStats(in EjectorComponent ejector)
        {
            EnsureId(ref counter, ejector.bulletId);
            counter[ejector.bulletId].consumption += PluginConfig.ejectorSpeedFactor.Value * 60f / (ejector.chargeSpend + ejector.coldSpend) * 600000f;
            counter[ejector.bulletId].consumers++;
        }

        public static void RecordOrbitalCollectorStats(in StationComponent station, double gasTotalHeat, double collectorsWorkCost)
        {
            if (station == null || station.id < 1 || !station.isCollector) return;
            var miningSpeedScale = (double)GameMain.history.miningSpeedScale;
            float collectSpeedRate = (gasTotalHeat - collectorsWorkCost > 0.0)
                ? ((float)((miningSpeedScale * gasTotalHeat - collectorsWorkCost) / (gasTotalHeat - collectorsWorkCost)))
                : 1f;

            for (int j = 0; j < station.collectionIds.Length; j++)
            {
                var productId = station.collectionIds[j];
                EnsureId(ref counter, productId);
                counter[productId].production += 60f * TICKS_PER_SEC * station.collectionPerTick[j] * collectSpeedRate;
                counter[productId].producers++;
            }
        }

        public static void RecordFractionatorStats(in FractionatorComponent fractionator, float maxSpeedIncrease, int beltMaxStack)
        {
            if (fractionator.id < 1) return;
            var speed = 30f;
            if (fractionator.fluidInputCargoCount * 2 > fractionator.fluidInputCount)
            {
                // for whatever reason the belt doesn't have a stacked input so discount back to 30 cargo / s rate 
                beltMaxStack = 1;
            }
            var runtimeSetting = ProliferatorOperationSetting.ForRecipe(115);

            if (runtimeSetting.Enabled)
            {
                speed += maxSpeedIncrease * speed;
            }

            if (fractionator.fluidId != 0)
            {
                var productId = fractionator.fluidId;
                EnsureId(ref counter, productId);
                counter[productId].consumption += 60f * speed * fractionator.produceProb * beltMaxStack;
                counter[productId].consumers++;
            }

            if (fractionator.productId != 0)
            {
                var productId = fractionator.productId;
                EnsureId(ref counter, productId);
                counter[productId].production += 60f * speed * fractionator.produceProb * beltMaxStack;
                counter[productId].producers++;
            }
        }

        public static void RecordAssemblerStats(in AssemblerComponent assembler, float maxSpeedIncrease, float maxProductivityIncrease)
        {
            if (assembler.id < 1 || assembler.recipeId == 0)
                return;
            var baseFrequency = 60f / (float)(assembler.timeSpend / 600000.0);
            var productionFrequency = baseFrequency;
            var speed = (float)(0.0001 * assembler.speed);

            var runtimeSetting = ProliferatorOperationSetting.ForRecipe(assembler.recipeId);

            // forceAccMode is 'Production Speedup' mode. It just adds a straight increase to both production and consumption rate
            if (runtimeSetting.Enabled)
            {
                switch (runtimeSetting.Mode)
                {
                    case ItemCalculationMode.Normal:
                        if (!assembler.forceAccMode && assembler.productive)
                            productionFrequency += productionFrequency * maxProductivityIncrease;
                        else
                            speed += speed * maxSpeedIncrease;
                        break;

                    case ItemCalculationMode.ForceSpeed:
                        speed += speed * maxSpeedIncrease;
                        break;

                    case ItemCalculationMode.ForceProductivity:
                        productionFrequency += productionFrequency * maxProductivityIncrease;
                        break;
                }
            }

            for (int j = 0; j < assembler.requires.Length; j++)
            {
                var productId = assembler.requires[j];
                EnsureId(ref counter, productId);
                counter[productId].consumption += baseFrequency * speed * assembler.requireCounts[j];
                counter[productId].consumers++;
            }

            for (int j = 0; j < assembler.products.Length; j++)
            {
                var productId = assembler.products[j];
                EnsureId(ref counter, productId);
                counter[productId].production += productionFrequency * speed * assembler.productCounts[j];
                counter[productId].producers++;
            }
        }

        public static void RecordGeneratorStats(in PowerGeneratorComponent generator)
        {
            var isFuelConsumer = generator.curFuelId > 0 && generator.productId == 0;

            if (isFuelConsumer)
            {
                // account for fuel consumption by power generator
                var fuelHeat = generator.fuelHeat;
                if (fuelHeat <= 0L) // fuel item count = 0, burning the last fuel
                {
                    fuelHeat = LDB.items.Select(generator.curFuelId)?.HeatValue ?? 0L;
                    if (fuelHeat == 0L) return; // Should not reach in normal case
                }

                var productId = generator.curFuelId; // The itemId of fuel that is burning currently
                EnsureId(ref counter, productId);
                counter[productId].consumption += 60.0f * TICKS_PER_SEC * generator.useFuelPerTick / fuelHeat;
                counter[productId].consumers++;
            }
            else if (generator.productId > 0 && generator.productHeat > 0)
            {
                var productId = generator.productId;
                EnsureId(ref counter, productId);
                counter[productId].production += 60.0f * TICKS_PER_SEC * generator.capacityCurrentTick / generator.productHeat;
                counter[productId].producers++;
                if (generator.catalystId > 0)
                {
                    // account for consumption of critical photons by ray receivers
                    EnsureId(ref counter, generator.catalystId);
                    counter[generator.catalystId].consumption += RAY_RECEIVER_GRAVITON_LENS_CONSUMPTION_RATE_PER_MIN;
                    counter[generator.catalystId].consumers++;
                }
            }
        }

        public static void RecordPowerExchangerStats(in PowerExchangerComponent powerExchanger, float maxSpeedIncrease)
        {
            if (powerExchanger.state == 1.0f) // Input
            {
                float rate = (powerExchanger.energyPerTick * (1f + maxSpeedIncrease)) / powerExchanger.maxPoolEnergy;
                EnsureId(ref counter, powerExchanger.fullId);
                counter[powerExchanger.fullId].production += 60.0f * TICKS_PER_SEC * rate;
                counter[powerExchanger.fullId].producers++;
                EnsureId(ref counter, powerExchanger.emptyId);
                counter[powerExchanger.emptyId].consumption += 60.0f * TICKS_PER_SEC * rate;
                counter[powerExchanger.emptyId].consumers++;
            }
            else if (powerExchanger.state == -1.0f) // Output
            {
                float rate = (powerExchanger.energyPerTick * (1f + maxSpeedIncrease)) / powerExchanger.maxPoolEnergy;
                EnsureId(ref counter, powerExchanger.emptyId);
                counter[powerExchanger.emptyId].production += 60.0f * TICKS_PER_SEC * rate;
                counter[powerExchanger.emptyId].producers++;
                EnsureId(ref counter, powerExchanger.fullId);
                counter[powerExchanger.fullId].consumption += 60.0f * TICKS_PER_SEC * rate;
                counter[powerExchanger.fullId].consumers++;
            }
        }

        public static void RecordMinerStats(in MinerComponent miner, PlanetFactory planetFactory, int waterItemId)
        {
            if (miner.id < 1) return;
            var veinPool = planetFactory.veinPool;
            var miningSpeedScale = (double)GameMain.history.miningSpeedScale;
            var productId = miner.productId;
            var veinId = (miner.veinCount != 0) ? miner.veins[miner.currentVeinIndex] : 0;

            if (miner.type == EMinerType.Water)
            {
                productId = waterItemId;
            }
            else if (productId == 0)
            {
                productId = veinPool[veinId].productId;
            }
            if (productId == 0) return;
            EnsureId(ref counter, productId);

            var frequency = 60f / (float)(miner.period / 600000.0);
            var speed = (float)(0.0001 * miner.speed * miningSpeedScale);
            var production = 0f;
            switch (miner.type)
            {
                case EMinerType.Water:
                    production = frequency * speed;
                    break;
                case EMinerType.Oil:
                    production = frequency * speed * (float)(veinPool[veinId].amount * (double)VeinData.oilSpeedMultiplier);
                    break;
                case EMinerType.Vein:
                    production = frequency * speed * miner.veinCount;
                    break;
            }
            if (PluginConfig.minerOutputLimit.Value > 0.0f)
            {
                var isStation = miner.entityId > 0 && planetFactory.entityPool[miner.entityId].stationId > 0;
                if (!isStation && PluginConfig.minerOutputLimit.Value < production)
                {
                    production = PluginConfig.minerOutputLimit.Value;
                }
            }

            counter[productId].production += production;
            counter[productId].producers++;
        }

        private static (float, float) DetermineLabFrequencies(in LabComponent lab, float maxProductivityIncrease, float maxSpeedIncrease)
        {
            // lab timeSpend is in game ticks, here we are figuring out the same number shown in lab window, example: 2.5 / m
            // when we are in Production Speedup mode `speedOverride` is increased.
            float baseFrequency = 0f, productionFrequency = 0;

            var runtimeSetting = ProliferatorOperationSetting.ForRecipe(lab.recipeId);

            if (runtimeSetting != null && runtimeSetting.Enabled)
            {
                if (runtimeSetting.Mode == ItemCalculationMode.Normal)
                {
                    // use whatever setting the lab has decide
                    if (!lab.forceAccMode)
                    {
                        // productivity bonuses are in Cargo table in the incTableMilli array
                        baseFrequency = (float)(1f / (lab.timeSpend / GameMain.tickPerSec / (60f * lab.speed)));
                        productionFrequency = baseFrequency + baseFrequency * maxProductivityIncrease;
                    }
                    else
                    {
                        var labSpeed = lab.speed * (1.0 + maxSpeedIncrease) + 0.1;
                        baseFrequency = (float)(1f / (lab.timeSpend / GameMain.tickPerSec / (60f * labSpeed)));
                        productionFrequency = baseFrequency;
                    }
                }
                else if (runtimeSetting.Mode == ItemCalculationMode.ForceSpeed)
                {
                    var labSpeed = lab.speed * (1.0 + maxSpeedIncrease) + 0.1;
                    baseFrequency = (float)(1f / (lab.timeSpend / GameMain.tickPerSec / (60f * labSpeed)));
                    productionFrequency = baseFrequency;
                }
                else if (runtimeSetting.Mode == ItemCalculationMode.ForceProductivity)
                {
                    baseFrequency = (float)(1f / (lab.timeSpend / GameMain.tickPerSec / (60f * lab.speed)));
                    productionFrequency = baseFrequency + baseFrequency * maxProductivityIncrease;
                }
            }
            else
            {
                // regular calculation
                baseFrequency = (float)(1f / (lab.timeSpend / GameMain.tickPerSec / (60f * lab.speed)));
                productionFrequency = baseFrequency;
            }

            return (baseFrequency, productionFrequency);
        }
    }
}
