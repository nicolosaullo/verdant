namespace GardenAI;

/// <summary>
/// Simulates realistic Ecowitt sensor readings for a Dunedin garden in late summer.
/// Replace with EcowittClient.GetLatestReadingAsync() once you have API keys.
/// </summary>
public static class MockSensorProvider
{
    private static readonly Random _rng = new(42);

    public static SensorReading GetCurrentReading()
    {
        // Dunedin late-summer afternoon: warm but not hot, moderate humidity
        var temp = 18.4 + (_rng.NextDouble() * 2 - 1);
        var humidity = 68 + _rng.Next(-5, 6);
        var dewPoint = temp - ((100 - humidity) / 5.0);

        return new SensorReading(
            Timestamp: DateTimeOffset.Now,
            Outdoor: new OutdoorReading(
                TemperatureCelsius: Math.Round(temp, 1),
                HumidityPercent: humidity,
                DewPointCelsius: Math.Round(dewPoint, 1),
                FeelsLikeCelsius: Math.Round(temp - 1.2, 1)
            ),
            SoilChannel1: new SoilReading(
                ChannelName: "Tomatoes (North bed)",
                MoisturePercent: 34,       // Getting dry — action needed
                BatteryVoltage: 1.52
            ),
            SoilChannel2: new SoilReading(
                ChannelName: "Courgettes (South bed)",
                MoisturePercent: 58,       // Healthy range
                BatteryVoltage: 1.49
            )
        );
    }

    /// <summary>
    /// Simulates 7 days of rolling history for trend analysis in the AI prompt.
    /// </summary>
    public static List<DailyHistory> GetSevenDayHistory()
    {
        var history = new List<DailyHistory>();
        var baseTemp = 18.0;
        var baseSoil1 = 52;  // Ch1 has been gradually drying out
        var baseSoil2 = 60;

        for (int i = 6; i >= 0; i--)
        {
            var day = DateTimeOffset.Now.AddDays(-i);
            history.Add(new DailyHistory(
                Date: day,
                TempMin: Math.Round(baseTemp - 4 + (_rng.NextDouble() * 2), 1),
                TempMax: Math.Round(baseTemp + 4 + (_rng.NextDouble() * 2), 1),
                TempAvg: Math.Round(baseTemp + (_rng.NextDouble() * 2 - 1), 1),
                HumidityAvg: 65 + _rng.Next(-8, 9),
                SoilCh1Avg: Math.Max(20, baseSoil1 - (6 - i) * 3 + _rng.Next(-3, 3)),
                SoilCh2Avg: Math.Max(40, baseSoil2 - (6 - i) * 1 + _rng.Next(-2, 3))
            ));
        }

        return history;
    }
}
