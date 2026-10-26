using System.Text.Json.Nodes;

namespace PowerMage.Api
{
    public class EnphaseClient(HttpClient httpClient)
    {
        public async Task<EnphaseEnergyModel?> GetEnphaseProductionDetails(CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/production.json?details=1");
            using var response = await httpClient.SendAsync(request, cancellationToken);

            response.EnsureSuccessStatusCode();

            var measurement = await response.Content.ReadAsStringAsync(cancellationToken);

            var root = JsonNode.Parse(measurement);
            if (root is null)
                return null;

            var production = FindMeter(root, "production", "production");
            var consumption = FindMeter(root, "consumption", "total-consumption");
            var netConsumption = FindMeter(root, "consumption", "net-consumption");

            return new EnphaseEnergyModel
            {
                Timestamp = GetLong(production, "readingTime")
                            ?? GetLong(consumption, "readingTime")
                            ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),

                // Instantaneous active power (W)
                ProductionPowerW = GetDouble(production, "wNow"),
                ConsumptionPowerW = GetDouble(consumption, "wNow"),
                NetConsumptionPowerW = GetDouble(netConsumption, "wNow"),

                // Lifetime energy (kWh; JSON provides Wh)
                TotalProductionKwh = ToKwh(GetDouble(production, "whLifetime")),
                TotalConsumptionKwh = ToKwh(GetDouble(consumption, "whLifetime")),
                TotalNetConsumptionKwh = ToKwh(GetDouble(netConsumption, "whLifetime")),

                // Production per phase (W)
                ProductionL1W = GetLineDouble(production, 0, "wNow") ?? 0,
                ProductionL2W = GetLineDouble(production, 1, "wNow") ?? 0,
                ProductionL3W = GetLineDouble(production, 2, "wNow") ?? 0,

                // Consumption per phase (W)
                ConsumptionL1W = GetLineDouble(consumption, 0, "wNow") ?? 0,
                ConsumptionL2W = GetLineDouble(consumption, 1, "wNow") ?? 0,
                ConsumptionL3W = GetLineDouble(consumption, 2, "wNow") ?? 0,

                // Net consumption per phase (W)
                NetConsumptionL1W = GetLineDouble(netConsumption, 0, "wNow") ?? 0,
                NetConsumptionL2W = GetLineDouble(netConsumption, 1, "wNow") ?? 0,
                NetConsumptionL3W = GetLineDouble(netConsumption, 2, "wNow") ?? 0
            };
        }

        private static JsonNode? FindMeter(JsonNode root,string arrayName,string? measurementType = null)
        {
            if (root[arrayName] is not JsonArray meters)
                return null;

            foreach (var meter in meters)
            {
                if (meter is null)
                    continue;

                var type = meter["measurementType"]?.GetValue<string>();

                if (measurementType is null ||
                    string.Equals(type, measurementType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return meter;
                }
            }

            return null;
        }

        private static double? GetDouble(JsonNode? node, string property)
        {
            try
            {
                return node?[property]?.GetValue<double>();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static long? GetLong(JsonNode? node, string property)
        {
            try
            {
                return node?[property]?.GetValue<long>();
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static double? GetLineDouble(JsonNode? meter, int lineIndex, string property)
        {
            if (meter?["lines"] is not JsonArray lines ||
                lineIndex < 0 || lineIndex >= lines.Count)
            {
                return null;
            }

            return GetDouble(lines[lineIndex], property);
        }

        private static double? ToKwh(double? wattHours) => wattHours.HasValue ? wattHours.Value / 1000.0 : null;
    }

    public class EnphaseEnergyModel
    {
        public int Id { get; set; }
        public long Timestamp { get; set; }

        public double? ProductionPowerW { get; set; }
        public double? ConsumptionPowerW { get; set; }
        public double? NetConsumptionPowerW { get; set; }

        public double? TotalProductionKwh { get; set; }
        public double? TotalConsumptionKwh { get; set; }
        public double? TotalNetConsumptionKwh { get; set; }

        public double? ProductionL1W { get; set; }
        public double? ProductionL2W { get; set; }
        public double? ProductionL3W { get; set; }

        public double? ConsumptionL1W { get; set; }
        public double? ConsumptionL2W { get; set; }
        public double? ConsumptionL3W { get; set; }

        public double? NetConsumptionL1W { get; set; }
        public double? NetConsumptionL2W { get; set; }
        public double? NetConsumptionL3W { get; set; }
    }
}
