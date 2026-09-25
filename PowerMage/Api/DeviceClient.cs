using PowerMage.Repository;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PowerMage.Api
{
    public class DeviceClient(HttpClient httpClient)
    {
        public async Task<DeviceEnergyModel?> GetMeasurement(DeviceModel deviceModel, CancellationToken cancellationToken = default)
        {
            var measurement = await httpClient.GetFromJsonAsync<DeviceMeasurement>(
                $"http://{deviceModel.IpAddress}/api/v1/data",
                cancellationToken);

            if (measurement == null)
                return null;

            var energy = new DeviceEnergyModel
            {
                DeviceId = deviceModel.Id,
                Timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),

                WifiSsid = measurement.WifiSsid,
                WifiStrength = measurement.WifiStrength,

                TotalPowerImportKwh =
                    measurement.TotalPowerImportKwh,

                TotalPowerImportT1Kwh =
                    measurement.TotalPowerImportT1Kwh,

                TotalPowerExportKwh =
                    measurement.TotalPowerExportKwh,

                TotalPowerExportT1Kwh =
                    measurement.TotalPowerExportT1Kwh,

                ActivePowerW =
                    measurement.ActivePowerW,

                ActivePowerL1W =
                    measurement.ActivePowerL1W,

                ActiveVoltageV =
                    measurement.ActiveVoltageV,

                ActiveCurrentA =
                    measurement.ActiveCurrentA,

                ActiveReactivePowerVar =
                    measurement.ActiveReactivePowerVar,

                ActiveApparentPowerVa =
                    measurement.ActiveApparentPowerVa,

                ActivePowerFactor =
                    measurement.ActivePowerFactor,

                ActiveFrequencyHz =
                    measurement.ActiveFrequencyHz
            };

            return energy;


        }

    }


    public class DeviceMeasurement
    {
        [JsonPropertyName("wifi_ssid")]
        public string? WifiSsid { get; set; }

        [JsonPropertyName("wifi_strength")]
        public double? WifiStrength { get; set; }

        [JsonPropertyName("total_power_import_kwh")]
        public double? TotalPowerImportKwh { get; set; }

        [JsonPropertyName("total_power_import_t1_kwh")]
        public double? TotalPowerImportT1Kwh { get; set; }

        [JsonPropertyName("total_power_export_kwh")]
        public double? TotalPowerExportKwh { get; set; }

        [JsonPropertyName("total_power_export_t1_kwh")]
        public double? TotalPowerExportT1Kwh { get; set; }

        [JsonPropertyName("active_power_w")]
        public double? ActivePowerW { get; set; }

        [JsonPropertyName("active_power_l1_w")]
        public double? ActivePowerL1W { get; set; }

        [JsonPropertyName("active_voltage_v")]
        public double? ActiveVoltageV { get; set; }

        [JsonPropertyName("active_current_a")]
        public double? ActiveCurrentA { get; set; }

        [JsonPropertyName("active_reactive_power_var")]
        public double? ActiveReactivePowerVar { get; set; }

        [JsonPropertyName("active_apparent_power_va")]
        public double? ActiveApparentPowerVa { get; set; }

        [JsonPropertyName("active_power_factor")]
        public double? ActivePowerFactor { get; set; }

        [JsonPropertyName("active_frequency_hz")]
        public double? ActiveFrequencyHz { get; set; }
    }

}
