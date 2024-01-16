using Bottleneck.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bottleneck
{
    public class PlanetaryProductionSummary
    {
        private readonly Dictionary<int, int> _planetProducerCount = new();
        private readonly Dictionary<int, int> _planetCosumerCount = new(); 
        private bool _prodSummaryTextDirty = true;
        private string _prodSummary = "";
        private bool _consumerSummaryTextDirty = true;
        private string _consumerSummary = "";

        public void AddProduction(int planetId, int producerCount)
        {
            if (_planetProducerCount.TryGetValue(planetId, out var count))
                _planetProducerCount[planetId] = count + producerCount;
            else
                _planetProducerCount[planetId] = producerCount;
            _prodSummaryTextDirty = true;
        }

        public void AddConsumption(int planetId, int consumerCount)
        {
            if (_planetCosumerCount.TryGetValue(planetId, out var count))
                _planetCosumerCount[planetId] = count + consumerCount;
            else
                _planetCosumerCount[planetId] = consumerCount;
            _consumerSummaryTextDirty = true;
        }

        public string GetProducerSummary()
        {
            if (!_prodSummaryTextDirty)
                return _prodSummary;

            var producersLabel = Strings.ProducersLabel;
            var includedElements = _planetProducerCount
                .OrderByDescending(pair => pair.Value)
                .Take(PluginConfig.productionPlanetCount.Value)
                .Select(prod => $"{GameMain.galaxy.PlanetById(prod.Key).displayName}: {producersLabel}={prod.Value}");
            _prodSummary = string.Join("\n", includedElements);
            _prodSummaryTextDirty = false;
            return _prodSummary;
        }

        public string GetConsumerSummary()
        {
            if (!_consumerSummaryTextDirty)
                return _consumerSummary;

            var consLabel = Strings.ConsumersLabel;
            var includedElements = _planetCosumerCount
                .OrderByDescending(pair => pair.Value)
                .Take(PluginConfig.productionPlanetCount.Value)
                .Select(prod => $"{GameMain.galaxy.PlanetById(prod.Key).displayName}: {consLabel}={prod.Value}");
            _consumerSummary = string.Join("\n", includedElements);
            _consumerSummaryTextDirty = false;
            return _consumerSummary;
        }

        public int ProducerPlanetCount()
        {
            return _planetProducerCount.Count;
        }

        public int ConsumerPlanetCount()
        {
            return _planetCosumerCount.Count;
        }

        public bool IsProducerPlanet(int planetId)
        {
            return _planetProducerCount.ContainsKey(planetId);
        }

        public bool IsConsumerPlanet(int planetId)
        {
            return _planetCosumerCount.ContainsKey(planetId);
        }

    }
}