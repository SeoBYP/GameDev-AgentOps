namespace GameDev_AgentOps;

record WeatherResponse(
    string name,
    WeatherMain main,
    WeatherDescription[] weather,
    WindInfo wind
);

record WeatherMain(double temp, double feels_like, int humidity);
record WeatherDescription(string main, string description);
record WindInfo(double speed);