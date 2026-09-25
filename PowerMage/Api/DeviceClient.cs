using PowerMage.Repository;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PowerMage.Api
{
    public class DeviceClient(HttpClient httpClient)
    {
        public async Task<DeviceEnergyModel?> GetSKTMeasurement(DeviceModel deviceModel, CancellationToken cancellationToken = default)
        {
            var measurement = await httpClient.GetFromJsonAsync<SKTMeasurement>($"http://{deviceModel.IpAddress}/api/v1/data", cancellationToken);

            if (measurement == null)
                return null;

            var energy = new DeviceEnergyModel
            {
                DeviceId = deviceModel.Id,
                Timestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds(),

                WifiSsid = measurement.WifiSsid,
                WifiStrength = measurement.WifiStrength,

                TotalPowerImportKwh = measurement.TotalPowerImportKwh,
                TotalPowerImportT1Kwh = measurement.TotalPowerImportT1Kwh,
                TotalPowerExportKwh = measurement.TotalPowerExportKwh,
                TotalPowerExportT1Kwh = measurement.TotalPowerExportT1Kwh,

                ActivePowerW = measurement.ActivePowerW,
                ActivePowerL1W = measurement.ActivePowerL1W,
                ActiveVoltageV = measurement.ActiveVoltageV,
                ActiveCurrentA = measurement.ActiveCurrentA,
                ActiveReactivePowerVar = measurement.ActiveReactivePowerVar,
                ActiveApparentPowerVa = measurement.ActiveApparentPowerVa,
                ActivePowerFactor = measurement.ActivePowerFactor,
                ActiveFrequencyHz = measurement.ActiveFrequencyHz
            };

            return energy;
        }

        public async Task<DeviceP1EnergyModel?> GetP1Measurement(DeviceModel deviceModel, CancellationToken cancellationToken = default)
        {
            var measurement = await httpClient.GetFromJsonAsync<P1Measurement>($"http://{deviceModel.IpAddress}/api/v1/data", cancellationToken);

            if (measurement == null)
                return null;

            var energy = new DeviceP1EnergyModel
            {
                DeviceId = deviceModel.Id,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),

                WifiSsid = measurement.WifiSsid,
                WifiStrength = measurement.WifiStrength,

                SmrVersion = measurement.SmrVersion,
                MeterModel = measurement.MeterModel,
                UniqueId = measurement.UniqueId,
                ActiveTariff = measurement.ActiveTariff,

                TotalPowerImportKwh = measurement.TotalPowerImportKwh,
                TotalPowerImportT1Kwh = measurement.TotalPowerImportT1Kwh,
                TotalPowerImportT2Kwh = measurement.TotalPowerImportT2Kwh,
                TotalPowerImportT3Kwh = measurement.TotalPowerImportT3Kwh,
                TotalPowerImportT4Kwh = measurement.TotalPowerImportT4Kwh,

                TotalPowerExportKwh = measurement.TotalPowerExportKwh,
                TotalPowerExportT1Kwh = measurement.TotalPowerExportT1Kwh,
                TotalPowerExportT2Kwh = measurement.TotalPowerExportT2Kwh,
                TotalPowerExportT3Kwh = measurement.TotalPowerExportT3Kwh,
                TotalPowerExportT4Kwh = measurement.TotalPowerExportT4Kwh,

                ActivePowerW = measurement.ActivePowerW,
                ActivePowerL1W = measurement.ActivePowerL1W,
                ActivePowerL2W = measurement.ActivePowerL2W,
                ActivePowerL3W = measurement.ActivePowerL3W,

                ActiveVoltageL1V = measurement.ActiveVoltageL1V,
                ActiveVoltageL2V = measurement.ActiveVoltageL2V,
                ActiveVoltageL3V = measurement.ActiveVoltageL3V,

                ActiveCurrentA = measurement.ActiveCurrentA,
                ActiveCurrentL1A = measurement.ActiveCurrentL1A,
                ActiveCurrentL2A = measurement.ActiveCurrentL2A,
                ActiveCurrentL3A = measurement.ActiveCurrentL3A,

                ActiveFrequencyHz = measurement.ActiveFrequencyHz,

                VoltageSagL1Count = measurement.VoltageSagL1Count,
                VoltageSagL2Count = measurement.VoltageSagL2Count,
                VoltageSagL3Count = measurement.VoltageSagL3Count,

                VoltageSwellL1Count = measurement.VoltageSwellL1Count,
                VoltageSwellL2Count = measurement.VoltageSwellL2Count,
                VoltageSwellL3Count = measurement.VoltageSwellL3Count,

                AnyPowerFailCount = measurement.AnyPowerFailCount,
                LongPowerFailCount = measurement.LongPowerFailCount,

                ActivePowerAverageW = measurement.ActivePowerAverageW,

                MontlyPowerPeakW = measurement.MontlyPowerPeakW,
                MontlyPowerPeakTimestamp = measurement.MontlyPowerPeakTimestamp,

                TotalGasM3 = measurement.TotalGasM3,
                GasTimestamp = measurement.GasTimestamp,
                GasUniqueId = measurement.GasUniqueId,

                External = measurement.External?
                    .Select(x => new P1ExternalMeasurementModel
                    {
                        UniqueId = x.UniqueId,
                        Type = x.Type,
                        Timestamp = x.Timestamp,
                        Value = x.Value,
                        Unit = x.Unit
                    })
                    .ToList()
            };

            return energy;
        }
    }

    public class SKTMeasurement
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

    public class P1Measurement
    {
        [JsonPropertyName("wifi_ssid")]
        public string? WifiSsid { get; set; }

        [JsonPropertyName("wifi_strength")]
        public double? WifiStrength { get; set; }

        [JsonPropertyName("smr_version")]
        public int? SmrVersion { get; set; }

        [JsonPropertyName("meter_model")]
        public string? MeterModel { get; set; }

        [JsonPropertyName("unique_id")]
        public string? UniqueId { get; set; }

        [JsonPropertyName("active_tariff")]
        public int? ActiveTariff { get; set; }

        [JsonPropertyName("total_power_import_kwh")]
        public double? TotalPowerImportKwh { get; set; }

        [JsonPropertyName("total_power_import_t1_kwh")]
        public double? TotalPowerImportT1Kwh { get; set; }

        [JsonPropertyName("total_power_import_t2_kwh")]
        public double? TotalPowerImportT2Kwh { get; set; }

        [JsonPropertyName("total_power_import_t3_kwh")]
        public double? TotalPowerImportT3Kwh { get; set; }

        [JsonPropertyName("total_power_import_t4_kwh")]
        public double? TotalPowerImportT4Kwh { get; set; }

        [JsonPropertyName("total_power_export_kwh")]
        public double? TotalPowerExportKwh { get; set; }

        [JsonPropertyName("total_power_export_t1_kwh")]
        public double? TotalPowerExportT1Kwh { get; set; }

        [JsonPropertyName("total_power_export_t2_kwh")]
        public double? TotalPowerExportT2Kwh { get; set; }

        [JsonPropertyName("total_power_export_t3_kwh")]
        public double? TotalPowerExportT3Kwh { get; set; }

        [JsonPropertyName("total_power_export_t4_kwh")]
        public double? TotalPowerExportT4Kwh { get; set; }

        [JsonPropertyName("active_power_w")]
        public double? ActivePowerW { get; set; }

        [JsonPropertyName("active_power_l1_w")]
        public double? ActivePowerL1W { get; set; }

        [JsonPropertyName("active_power_l2_w")]
        public double? ActivePowerL2W { get; set; }

        [JsonPropertyName("active_power_l3_w")]
        public double? ActivePowerL3W { get; set; }

        [JsonPropertyName("active_voltage_l1_v")]
        public double? ActiveVoltageL1V { get; set; }

        [JsonPropertyName("active_voltage_l2_v")]
        public double? ActiveVoltageL2V { get; set; }

        [JsonPropertyName("active_voltage_l3_v")]
        public double? ActiveVoltageL3V { get; set; }

        [JsonPropertyName("active_current_a")]
        public double? ActiveCurrentA { get; set; }

        [JsonPropertyName("active_current_l1_a")]
        public double? ActiveCurrentL1A { get; set; }

        [JsonPropertyName("active_current_l2_a")]
        public double? ActiveCurrentL2A { get; set; }

        [JsonPropertyName("active_current_l3_a")]
        public double? ActiveCurrentL3A { get; set; }

        [JsonPropertyName("active_frequency_hz")]
        public double? ActiveFrequencyHz { get; set; }

        [JsonPropertyName("voltage_sag_l1_count")]
        public int? VoltageSagL1Count { get; set; }

        [JsonPropertyName("voltage_sag_l2_count")]
        public int? VoltageSagL2Count { get; set; }

        [JsonPropertyName("voltage_sag_l3_count")]
        public int? VoltageSagL3Count { get; set; }

        [JsonPropertyName("voltage_swell_l1_count")]
        public int? VoltageSwellL1Count { get; set; }

        [JsonPropertyName("voltage_swell_l2_count")]
        public int? VoltageSwellL2Count { get; set; }

        [JsonPropertyName("voltage_swell_l3_count")]
        public int? VoltageSwellL3Count { get; set; }

        [JsonPropertyName("any_power_fail_count")]
        public int? AnyPowerFailCount { get; set; }

        [JsonPropertyName("long_power_fail_count")]
        public int? LongPowerFailCount { get; set; }

        [JsonPropertyName("active_power_average_w")]
        public double? ActivePowerAverageW { get; set; }

        // Note: API documentation has a typo: "montly"
        [JsonPropertyName("montly_power_peak_w")]
        public double? MontlyPowerPeakW { get; set; }

        // Note: API documentation has a typo: "montly"
        [JsonPropertyName("montly_power_peak_timestamp")]
        public long? MontlyPowerPeakTimestamp { get; set; }

        [JsonPropertyName("total_gas_m3")]
        public double? TotalGasM3 { get; set; }

        [JsonPropertyName("gas_timestamp")]
        public long? GasTimestamp { get; set; }

        // The actual JSON uses "gas_unique_id"
        [JsonPropertyName("gas_unique_id")]
        public string? GasUniqueId { get; set; }

        [JsonPropertyName("external")]
        public List<P1ExternalMeasurement>? External { get; set; }
    }

    public class P1ExternalMeasurement
    {
        [JsonPropertyName("unique_id")]
        public string? UniqueId { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("timestamp")]
        public long? Timestamp { get; set; }

        [JsonPropertyName("value")]
        public double? Value { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }
    }

}
