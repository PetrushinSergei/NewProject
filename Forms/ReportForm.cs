using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PowerGridEditor
{
    public partial class ReportForm : Form
    {
        private List<object> _elements;
        private List<GraphicBranch> _branches;
        private List<GraphicShunt> _shunts;
        private readonly Timer refreshTimer;
        private string _modeBurdeningInfo = "нет данных";
        private string _researchModeInfo = "нет данных";

        public ReportForm()
        {
            InitializeComponent();
            refreshTimer = new Timer { Interval = 1000 };
            refreshTimer.Tick += (s, e) => RefreshReport();
            refreshTimer.Start();
            FormClosing += (s, e) => refreshTimer.Stop();
        }

        public void SetNetworkSummary(List<object> elements, List<GraphicBranch> branches, List<GraphicShunt> shunts)
        {
            _elements = elements;
            _branches = branches;
            _shunts = shunts;
            RefreshReport();
        }

        public void SetModeBurdeningInfo(string info)
        {
            _modeBurdeningInfo = string.IsNullOrWhiteSpace(info) ? "нет данных" : info;
            if (txtModeBurdening != null)
            {
                txtModeBurdening.Text = _modeBurdeningInfo;
            }
        }

        public void SetResearchModeInfo(string info)
        {
            _researchModeInfo = string.IsNullOrWhiteSpace(info) ? "нет данных" : info;
            if (txtResearchMode != null)
            {
                txtResearchMode.Text = _researchModeInfo;
            }
        }

        private void RefreshReport()
        {
            if (_elements == null || _branches == null || _shunts == null)
            {
                return;
            }

            var cduLines = BuildCduLines();
            var engine = new ConsoleApplicationEngine(cduLines, CalculationOptions.Precision, CalculationOptions.MaxIterations);
            var result = engine.Run();

            txtOverview.Text = result.NetworkCdu;
            txtErr.Text = result.NetworkErr;
            txtInput.Text = result.NetworkOut;
            txtResults.Text = result.NetworkRez;
            txtLoss.Text = result.NetworkRip;
            txtBreakdown.Text = result.LossesRez;
            txtLoadCurrent.Text = BuildCurrentLoadingReport(result.LossesRez);
            txtLoadCurrentAmp.Text = BuildCurrentLoadingAmpReport(result.LossesRez);
            txtVoltageAnalysis.Text = BuildVoltageAnalysisReport(result.NetworkRez);
            txtModeBurdening.Text = _modeBurdeningInfo;
            txtResearchMode.Text = _researchModeInfo;
        }

        private sealed class BranchCurrentRow
        {
            public double Active;
            public double Reactive;
        }

        private sealed class LoadMeaning
        {
            public string Color;
            public string Description;
        }


        private sealed class NodeVoltageRow
        {
            public int Number;
            public double UNom;
            public double UFact;
            public double UCalc;
        }

        private string BuildVoltageAnalysisReport(string networkRez)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Анализ напряжения");
            sb.AppendLine("Формула:");
            sb.AppendLine("ΔU = Uном - Uфакт");
            sb.AppendLine();
            sb.AppendLine("Узел      Uном,кВ    Uфакт,кВ      ΔU,кВ     ΔU,%      Статус");

            foreach (var row in BuildNodeVoltageRows().OrderBy(x => x.Number))
            {
                double deltaKv = row.UNom - row.UFact;
                double deltaPercent = Math.Abs(row.UNom) < 1e-9 ? 0 : (deltaKv / row.UNom) * 100.0;
                string status = Math.Abs(deltaPercent) > 10.0 ? "Критическое (>10%)" : "Допустимое";

                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,4}    {1,9:F2}  {2,9:F2}  {3,10:F2}  {4,7:F2}%  {5}",
                    row.Number,
                    row.UNom,
                    row.UFact,
                    deltaKv,
                    deltaPercent,
                    status));
            }

            sb.AppendLine();
            sb.AppendLine("Критерий: |ΔU|/Uном > 10% — критическое отклонение.");
            return sb.ToString();
        }

        private List<NodeVoltageRow> BuildNodeVoltageRows()
        {
            var rows = new List<NodeVoltageRow>();
            foreach (var element in _elements)
            {
                var node = element as GraphicNode;
                if (node != null)
                {
                    rows.Add(new NodeVoltageRow
                    {
                        Number = node.Data.Number,
                        UNom = node.Data.InitialVoltage,
                        UFact = node.Data.ActualVoltage,
                        UCalc = node.Data.CalculatedVoltage
                    });
                    continue;
                }

                var baseNode = element as GraphicBaseNode;
                if (baseNode != null)
                {
                    rows.Add(new NodeVoltageRow
                    {
                        Number = baseNode.Data.Number,
                        UNom = baseNode.Data.InitialVoltage,
                        UFact = baseNode.Data.ActualVoltage,
                        UCalc = baseNode.Data.CalculatedVoltage
                    });
                }
            }

            return rows;
        }

        private string BuildCurrentLoadingReport(string lossesRez)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Расчет загрузки ветвей по току (в относительных единицах)");
            sb.AppendLine("Формулы:");
            sb.AppendLine("Iр = sqrt(Iак^2 + Iре^2)");
            sb.AppendLine("Kзагр = Iр / Iдоп * 100%");
            sb.AppendLine();
            sb.AppendLine("Ветвь      Iак       Iре       Iр        Iдоп      Kзагр     Запас    Цвет      Смысл");

            var currents = ParseBranchCurrents(lossesRez);
            foreach (var branch in _branches.OrderBy(b => b.Data.StartNodeNumber).ThenBy(b => b.Data.EndNodeNumber))
            {
                int start = branch.Data.StartNodeNumber;
                int end = branch.Data.EndNodeNumber;
                BranchCurrentRow c;
                if (!currents.TryGetValue(BuildBranchKey(start, end), out c) && !currents.TryGetValue(BuildBranchKey(end, start), out c))
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,4}-{1,-4}  нет данных тока в losses.rez", start, end));
                    continue;
                }

                double id = branch.Data.PermissibleCurrent <= 0 ? 600 : branch.Data.PermissibleCurrent;
                double ir = Math.Sqrt(c.Active * c.Active + c.Reactive * c.Reactive);
                double load = id > 0 ? ir / id * 100.0 : 0;
                double reserve = 100.0 - load;

                var meaning = GetLoadMeaning(load);
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,4}-{1,-4}  {2,7:F3}  {3,8:F3}  {4,8:F3}  {5,8:F2}  {6,7:F2}%  {7,7:F2}%  {8,-9} {9}",
                    start,
                    end,
                    c.Active,
                    c.Reactive,
                    ir,
                    id,
                    load,
                    reserve,
                    meaning.Color,
                    meaning.Description));
            }

            sb.AppendLine();
            sb.AppendLine("Шкала интерпретации:");
            sb.AppendLine("0% - 50%   : Синий    - Линия недогружена (холодная)");
            sb.AppendLine("50% - 80%  : Зеленый  - Оптимальный режим");
            sb.AppendLine("80% - 95%  : Желтый   - Внимание! Близко к пределу");
            sb.AppendLine("95% - 100% : Оранжевый- Предупреждение (МУН должен реагировать)");
            sb.AppendLine(">100%      : Красный  - ПЕРЕГРУЗКА! Риск повреждения провода");

            return sb.ToString();
        }

        private string BuildCurrentLoadingAmpReport(string lossesRez)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Расчет загрузки ветвей по току в амперах");
            sb.AppendLine("Формулы:");
            sb.AppendLine("I_pu = sqrt(re^2 + im^2)");
            sb.AppendLine("S_base = 1000");
            sb.AppendLine("I_amp = (I_pu * S_base) / (sqrt(3) * Uном_кВ) * 1000");
            sb.AppendLine("Kзагр = I_amp / I_max * 100%");
            sb.AppendLine();
            sb.AppendLine("Ветвь      re(pu)   im(pu)   Uном,кВ   Ipu      I_amp,А    Imax,А    Kзагр     Перегруз");

            var currents = ParseBranchCurrents(lossesRez);
            foreach (var branch in _branches.OrderBy(b => b.Data.StartNodeNumber).ThenBy(b => b.Data.EndNodeNumber))
            {
                int start = branch.Data.StartNodeNumber;
                int end = branch.Data.EndNodeNumber;
                BranchCurrentRow c;
                if (!currents.TryGetValue(BuildBranchKey(start, end), out c) && !currents.TryGetValue(BuildBranchKey(end, start), out c))
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,4}-{1,-4}  нет данных тока в losses.rez", start, end));
                    continue;
                }

                double uNomKv = GetNodeNominalVoltageKv(start);
                double iMax = branch.Data.PermissibleCurrent <= 0 ? 600 : branch.Data.PermissibleCurrent;
                bool overloaded;
                double iPu;
                double iAmp = ConvertTokToAmperes(c.Active, c.Reactive, uNomKv, iMax, out overloaded, out iPu);
                double load = iMax > 0 ? iAmp / iMax * 100.0 : 0;

                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,4}-{1,-4}  {2,7:F3}  {3,7:F3}  {4,8:F2}  {5,7:F3}  {6,9:F2}  {7,8:F2}  {8,7:F2}%  {9}",
                    start,
                    end,
                    c.Active,
                    c.Reactive,
                    uNomKv,
                    iPu,
                    iAmp,
                    iMax,
                    load,
                    overloaded ? "Да" : "Нет"));
            }

            sb.AppendLine();
            sb.AppendLine("Шкала интерпретации:");
            sb.AppendLine("0% - 50%   : Синий    - Линия недогружена (холодная)");
            sb.AppendLine("50% - 80%  : Зеленый  - Оптимальный режим");
            sb.AppendLine("80% - 95%  : Желтый   - Внимание! Близко к пределу");
            sb.AppendLine("95% - 100% : Оранжевый- Предупреждение (МУН должен реагировать)");
            sb.AppendLine(">100%      : Красный  - ПЕРЕГРУЗКА! Риск повреждения провода");
            return sb.ToString();
        }

        private double GetNodeNominalVoltageKv(int nodeNumber)
        {
            foreach (var element in _elements)
            {
                var node = element as GraphicNode;
                if (node != null && node.Data.Number == nodeNumber)
                {
                    return node.Data.InitialVoltage > 0 ? node.Data.InitialVoltage : 110;
                }

                var baseNode = element as GraphicBaseNode;
                if (baseNode != null && baseNode.Data.Number == nodeNumber)
                {
                    return baseNode.Data.InitialVoltage > 0 ? baseNode.Data.InitialVoltage : 110;
                }
            }

            return 110;
        }

        private double ConvertTokToAmperes(double re, double im, double uNomKv, double iMax, out bool overloaded, out double iPu)
        {
            iPu = Math.Sqrt(re * re + im * im);
            const double sBase = 1000.0;
            double u = uNomKv <= 0 ? 110.0 : uNomKv;
            double iAmp = (iPu * sBase) / (Math.Sqrt(3.0) * u) * 1000.0;
            overloaded = iAmp > iMax;
            return iAmp;
        }

        private static string BuildBranchKey(int start, int end)
        {
            return start.ToString(CultureInfo.InvariantCulture) + "-" + end.ToString(CultureInfo.InvariantCulture);
        }

        private Dictionary<string, BranchCurrentRow> ParseBranchCurrents(string lossesRez)
        {
            var result = new Dictionary<string, BranchCurrentRow>();
            if (string.IsNullOrWhiteSpace(lossesRez))
            {
                return result;
            }

            bool inTable = false;
            var lines = lossesRez.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("Ветвь Нач.", StringComparison.OrdinalIgnoreCase))
                {
                    inTable = true;
                    continue;
                }

                if (!inTable || string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!char.IsDigit(line[0]))
                {
                    if (line.StartsWith("Задающие токи узлов", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    continue;
                }

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4)
                {
                    continue;
                }

                if (!int.TryParse(parts[0], out int start) || !int.TryParse(parts[1], out int end))
                {
                    continue;
                }

                if (!double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double ia))
                {
                    continue;
                }

                if (!double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double ir))
                {
                    continue;
                }

                result[BuildBranchKey(start, end)] = new BranchCurrentRow { Active = ia, Reactive = ir };
            }

            return result;
        }

        private LoadMeaning GetLoadMeaning(double load)
        {
            if (load <= 50) return new LoadMeaning { Color = "Синий", Description = "Линия недогружена (холодная)" };
            if (load <= 80) return new LoadMeaning { Color = "Зеленый", Description = "Оптимальный режим" };
            if (load <= 95) return new LoadMeaning { Color = "Желтый", Description = "Внимание! Близко к пределу" };
            if (load <= 100) return new LoadMeaning { Color = "Оранжевый", Description = "Предупреждение (МУН должен реагировать)" };
            return new LoadMeaning { Color = "Красный", Description = "ПЕРЕГРУЗКА! Риск повреждения провода" };
        }

        private List<string> BuildCduLines()
        {
            var lines = new List<string>();

            var baseNodeNumbers = new HashSet<int>();
            foreach (var element in _elements)
            {
                var baseNode = element as GraphicBaseNode;
                if (baseNode != null)
                {
                    baseNodeNumbers.Add(baseNode.Data.Number);
                }
            }

            foreach (var element in _elements)
            {
                var node = element as GraphicNode;
                if (node == null || baseNodeNumbers.Contains(node.Data.Number))
                {
                    continue;
                }

                string line = $"0201 0   {node.Data.Number,3}  {node.Data.InitialVoltage,3}     " +
                              $"{FormatIntValue(node.Data.NominalActivePower),4}  {FormatIntValue(node.Data.NominalReactivePower),3}  " +
                              $"{FormatIntValue(node.Data.ActivePowerGeneration),1} {FormatIntValue(node.Data.ReactivePowerGeneration),1}  " +
                              $"{FormatIntValue(node.Data.FixedVoltageModule),3} {FormatIntValue(node.Data.MinReactivePower),1} {FormatIntValue(node.Data.MaxReactivePower),1}";
                lines.Add(line);
            }

            foreach (var element in _elements)
            {
                var baseNode = element as GraphicBaseNode;
                if (baseNode == null)
                {
                    continue;
                }

                string line = $"0102 0   {baseNode.Data.Number,3}  {baseNode.Data.InitialVoltage,3}       " +
                              $"{FormatIntValue(baseNode.Data.NominalActivePower),1}    " +
                              $"{FormatIntValue(baseNode.Data.NominalReactivePower),1}  " +
                              $"{FormatIntValue(baseNode.Data.ActivePowerGeneration),1} {FormatIntValue(baseNode.Data.ReactivePowerGeneration),1}   " +
                              $"{FormatIntValue(baseNode.Data.FixedVoltageModule),1} " +
                              $"{FormatIntValue(baseNode.Data.MinReactivePower),1} " +
                              $"{FormatIntValue(baseNode.Data.MaxReactivePower),1}";
                lines.Add(line);
            }

            foreach (var shunt in _shunts)
            {
                string shuntLine = $"0301 0   {shunt.Data.StartNodeNumber,3}      {shunt.Data.EndNodeNumber,2}    " +
                                   $"{FormatDoubleValue(shunt.Data.ActiveResistance),4}   " +
                                   $"{FormatDoubleValue(shunt.Data.ReactiveResistance),5}";
                lines.Add(shuntLine);
            }

            foreach (var branch in _branches)
            {
                string branchLine = $"0301 0   {branch.Data.StartNodeNumber,3}      {branch.Data.EndNodeNumber,2}    " +
                                    $"{FormatDoubleValue(branch.Data.ActiveResistance),4}   " +
                                    $"{FormatDoubleValue(branch.Data.ReactiveResistance),5}   " +
                                    $"{FormatDoubleValue(branch.Data.ReactiveConductivity, true),6}     " +
                                    $"{FormatDoubleValue(branch.Data.TransformationRatio),5} " +
                                    $"{FormatIntValue(branch.Data.ActiveConductivity),1} 0 0 {FormatDoubleValue(branch.Data.PermissibleCurrentLimit)}";
                lines.Add(branchLine);
            }

            return lines;
        }

        private string FormatIntValue(double number)
        {
            if (number == 0)
            {
                return "0";
            }

            return ((int)number).ToString(CultureInfo.InvariantCulture);
        }

        private string FormatDoubleValue(double number, bool isConductivity = false)
        {
            if (number == 0)
            {
                return "0";
            }

            if (isConductivity && number < 0)
            {
                return $"-{Math.Abs(number).ToString("F1", CultureInfo.InvariantCulture)}";
            }

            string formatted = number.ToString("F2", CultureInfo.InvariantCulture);
            if (formatted.StartsWith("0."))
            {
                formatted = "." + formatted.Substring(2);
            }

            return formatted;
        }

        // Совместимость для старых вызовов после частичных/ручных мерджей.
        private string FormatInt(object number)
        {
            double value;
            if (number == null || !double.TryParse(Convert.ToString(number, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                value = 0;
            }

            return FormatIntValue(value);
        }

        // Совместимость для старых вызовов после частичных/ручных мерджей.
        private string FormatDouble(object number, bool isConductivity = false)
        {
            double value;
            if (number == null || !double.TryParse(Convert.ToString(number, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                value = 0;
            }

            return FormatDoubleValue(value, isConductivity);
        }
    }
}
