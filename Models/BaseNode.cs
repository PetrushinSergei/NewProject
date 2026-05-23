using System.Collections.Generic;

namespace PowerGridEditor
{
    public class BaseNode
    {
        public string Code { get; } = "0102 0";
        public int Number { get; set; }
        public double InitialVoltage { get; set; }
        public double ActualVoltage { get; set; }
        public double CalculatedVoltage { get; set; }
        public double NominalActivePower { get; set; }
        public double NominalReactivePower { get; set; }
        public double ActivePowerGeneration { get; set; }
        public double ReactivePowerGeneration { get; set; }
        public double FixedVoltageModule { get; set; }
        public double MinReactivePower { get; set; }
        public double MaxReactivePower { get; set; }
        public double PermissibleCurrent { get; set; } = 600;

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

        public BaseNode(int number)
        {
            Number = number;
            InitialVoltage = 525.0;
            ActualVoltage = InitialVoltage;
            CalculatedVoltage = InitialVoltage;

            string[] keys = { "U", "Ufact", "Ucalc", "P", "Q", "Pg", "Qg", "Uf", "Qmin", "Qmax", "Imax" };
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
            return $"{Code} {Number} {InitialVoltage} {NominalActivePower} {NominalReactivePower} {ActivePowerGeneration} {ReactivePowerGeneration} {FixedVoltageModule} {MinReactivePower} {MaxReactivePower}";
        }
    }
}
