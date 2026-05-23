using System.Collections.Generic;

namespace PowerGridEditor
{
    public class Shunt
    {
        public string Code { get; } = "0301 0";
        public int StartNodeNumber { get; set; }
        public int EndNodeNumber { get; set; } = 0; // Всегда 0 для шунта
        public double ActiveResistance { get; set; }
        public double ReactiveResistance { get; set; }

        public string IPAddress { get; set; } = "127.0.0.1";
        public string Port { get; set; } = "502";
        public string DeviceID { get; set; } = "1";
        public string Protocol { get; set; } = "Modbus TCP";
        public int MeasurementIntervalSeconds { get; set; } = 2;
        public double IncrementStep { get; set; } = 1;
        public int IncrementIntervalSeconds { get; set; } = 2;
        public Dictionary<string, string> ParamRegisters { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, bool> ParamAutoModes { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, double> ParamIncrementSteps { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, int> ParamIncrementIntervals { get; set; } = new Dictionary<string, int>();

        public Shunt(int startNode)
        {
            StartNodeNumber = startNode;
            EndNodeNumber = 0; // Фиксированное значение

            string[] keys = { "R", "X" };
            foreach (var key in keys)
            {
                ParamRegisters[key] = "0";
                ParamAutoModes[key] = false;
                ParamIncrementSteps[key] = 1;
                ParamIncrementIntervals[key] = 2;
            }
        }

        public string ToFileString()
        {
            return $"{Code} {StartNodeNumber} {EndNodeNumber} {ActiveResistance:F1} {ReactiveResistance}";
        }
    }
}
