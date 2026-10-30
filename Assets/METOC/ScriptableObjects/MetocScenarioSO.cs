using UnityEngine;

namespace DeepWater.Metoc
{
    /// <summary>
    /// A METOC scenario's start (E29): the UTC date and time, the place, the day speed and the weather. A fictional
    /// theatre's place is AUTHORED (E28); when Theatre bakes real landforms, the theatre's anchor replaces it.
    /// </summary>
    [CreateAssetMenu(menuName = "Deep Water/METOC/Scenario", fileName = "MetocScenario")]
    public sealed class MetocScenarioSO : ScriptableObject
    {
        [Header("Start (UTC)")]
        [Range(1800, 2100)] public int year = 2026;
        [Range(1, 12)] public int month = 10;
        [Range(1, 31)] public int day = 30;
        [Range(0, 23)] public int hour = 18;
        [Range(0, 59)] public int minute;
        [Range(0, 59)] public int second;

        [Header("Place")]
        [Range(-90f, 90f)] public double latitudeDeg = 30.0;
        [Range(-180f, 180f)] public double longitudeDeg = -40.0;
        [Tooltip("Where the place comes from; a fictional theatre's anchor is AUTHORED.")]
        public string placeProvenance = "AUTHORED: open ocean, a fictional theatre's anchor.";

        [Header("Time")]
        [Tooltip("Labelled factor between simulation time and the day's time (E29). 1 = real time.")]
        [Min(0f)] public double daySpeed = 1.0;

        [Header("Weather")]
        [Tooltip("The weather the scenario starts in.")]
        public MetocWeatherSO startWeather;

        public double StartUtSeconds => Calendar.UtSeconds(year, month, day, hour, minute, second);

        /// <summary>The scenario; throws when it names no start weather, since a missing preset must not read as clear.</summary>
        public MetocScenario ToScenario()
        {
            if (startWeather == null) throw new System.InvalidOperationException($"{name} names no start weather");
            return new MetocScenario(StartUtSeconds, new MetocPlace(latitudeDeg, longitudeDeg), daySpeed, startWeather.ToWeather());
        }
    }
}
