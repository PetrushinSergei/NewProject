using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using System.Globalization;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Linq;
using NModbus;

namespace PowerGridEditor
{
    public partial class NodeForm : Form
    {
        public Node MyNode { get; private set; }
        private Timer liveTimer;
        private Timer modelSyncTimer;
        private string[] keys = { "Number", "U", "Ufact", "Ucalc", "P", "Q", "Pg", "Qg", "Uf", "Qmin", "Qmax" };
        private NumericUpDown numericMeasurementInterval;
        private TextBox[] incrementStepBoxes;
        private TextBox[] incrementIntervalBoxes;
        private Button[] incrementToggleButtons;
        private bool suppressTelemetryUiEvents;
        private readonly System.Collections.Generic.Dictionary<Control, Point> fixedParamLocations = new System.Collections.Generic.Dictionary<Control, Point>();
        public event EventHandler TelemetryUpdated;

        public NodeForm(Node node)
        {
            InitializeComponent(); // Все контроллеры создаются здесь (в Designer.cs)
            BackColor = Color.FromArgb(245, 250, 255);
            tabParams.BackColor = Color.FromArgb(245, 250, 255);
            tabSettings.BackColor = Color.FromArgb(245, 250, 255);
            MyNode = node;

            liveTimer = new Timer { Interval = 2000 };
            liveTimer.Tick += async (s, e) => await PollModbusTask();
            liveTimer.Start();

            modelSyncTimer = new Timer { Interval = 700 };
            modelSyncTimer.Tick += (s, e) =>
            {
                if (ContainsFocus && ActiveControl is TextBox) return;
                RefreshFromModel();
            };
            modelSyncTimer.Start();

            // Привязка событий (контроллеры берутся из Designer)
            btnCheckIP.Click += async (s, e) => await RunPing();
            buttonSave.Click += (s, e) => SaveData();
            buttonCancel.Click += (s, e) => this.Close();
            this.FormClosing += (s, e) => { liveTimer.Stop(); modelSyncTimer.Stop(); };

            SetupParameterIncrementEditors();
            CaptureFixedParamLayout();
            WireTelemetryCheckboxes();
            WireNumericInputGuards();
            ApplyBoldFonts(this);

            LoadData();
        }

        private void WireTelemetryCheckboxes()
        {
            for (int i = 1; i < paramChecks.Length; i++)
            {
                int idx = i;
                if (paramChecks[idx] != null)
                {
                    paramChecks[idx].CheckedChanged += (s, e) =>
                    {
                        if (suppressTelemetryUiEvents) return;
                        ApplyTelemetryState(idx, paramChecks[idx]);
                    };
                }
            }
        }

        private void WireNumericInputGuards()
        {
            KeyPressEventHandler decimalGuard = (s, e) =>
            {
                char c = e.KeyChar;
                if (char.IsControl(c)) return;
                if (char.IsDigit(c)) return;
                if (c == '-' || c == ',' || c == '.') return;
                e.Handled = true;
            };

            KeyPressEventHandler intGuard = (s, e) =>
            {
                char c = e.KeyChar;
                if (char.IsControl(c) || char.IsDigit(c)) return;
                e.Handled = true;
            };

            for (int i = 0; i < paramBoxes.Length; i++)
            {
                if (paramBoxes[i] != null) paramBoxes[i].KeyPress += decimalGuard;
                if (i > 0 && addrBoxes[i] != null) addrBoxes[i].KeyPress += intGuard;
                if (i > 0 && incrementIntervalBoxes != null && incrementIntervalBoxes[i] != null) incrementIntervalBoxes[i].KeyPress += intGuard;
                if (i > 0 && incrementStepBoxes != null && incrementStepBoxes[i] != null) incrementStepBoxes[i].KeyPress += decimalGuard;
            }

            textBoxPort.KeyPress += intGuard;
            textBoxID.KeyPress += intGuard;
        }

        private void ApplyTelemetryState(int index, Control focusTarget)
        {
            if (index <= 0 || index >= paramBoxes.Length || paramChecks[index] == null) return;

            TextBox targetBox = paramBoxes[index];
            bool telemetryOn = paramChecks[index].Checked;

            targetBox.ReadOnly = telemetryOn;
            targetBox.BackColor = telemetryOn ? SystemColors.Control : SystemColors.Window;
            EnsureFixedParamLayout();

            if (focusTarget == null) return;

            var target = focusTarget;
            BeginInvoke(new Action(() =>
            {
                if (target != null && target.CanFocus)
                {
                    target.Focus();
                }
                else if (targetBox.CanFocus)
                {
                    targetBox.Focus();
                }
            }));
        }

        private void CaptureFixedParamLayout()
        {
            fixedParamLocations.Clear();
            foreach (Control control in tabParams.Controls)
            {
                fixedParamLocations[control] = control.Location;
            }
        }

        private void EnsureFixedParamLayout()
        {
            if (fixedParamLocations.Count == 0) return;

            tabParams.SuspendLayout();
            foreach (var pair in fixedParamLocations)
            {
                var control = pair.Key;
                if (control != null && !control.IsDisposed)
                {
                    control.Location = pair.Value;
                }
            }
            tabParams.ResumeLayout(false);
        }

        private void SetupParameterIncrementEditors()
        {
            incrementStepBoxes = new TextBox[keys.Length];
            incrementIntervalBoxes = new TextBox[keys.Length];
            incrementToggleButtons = new Button[keys.Length];

            tabParams.Controls.Add(new Label { Text = "Шаг:", Location = new Point(520, 2), Size = new Size(45, 18) });
            tabParams.Controls.Add(new Label { Text = "Инт.,с:", Location = new Point(585, 2), Size = new Size(55, 18) });
            tabParams.Controls.Add(new Label { Text = "Авто изм.", Location = new Point(650, 2), Size = new Size(80, 18) });

            for (int i = 1; i < keys.Length; i++)
            {
                var stepBox = new TextBox { Size = new Size(55, 23), Text = "1" };
                var intervalBox = new TextBox { Size = new Size(50, 23), Text = "2" };
                if (addrBoxes[i] != null)
                {
                    stepBox.Location = new Point(addrBoxes[i].Right + 10, addrBoxes[i].Top);
                    intervalBox.Location = new Point(stepBox.Right + 8, addrBoxes[i].Top);
                }
                var toggleButton = new Button { Size = new Size(75, 23), Text = "Старт" };
                toggleButton.Location = new Point(intervalBox.Right + 8, addrBoxes[i].Top);
                int idx = i;
                toggleButton.Click += (s, e) => ToggleIncrement(idx);

                incrementStepBoxes[i] = stepBox;
                incrementIntervalBoxes[i] = intervalBox;
                incrementToggleButtons[i] = toggleButton;
                tabParams.Controls.Add(stepBox);
                tabParams.Controls.Add(intervalBox);
                tabParams.Controls.Add(toggleButton);
            }
        }

        public void RefreshFromModel()
        {
            LoadData();
        }

        private void LoadData()
        {
            var inv = CultureInfo.InvariantCulture;
            double[] vals = {
                MyNode.Number, MyNode.InitialVoltage, MyNode.ActualVoltage, MyNode.CalculatedVoltage,
                MyNode.NominalActivePower, MyNode.NominalReactivePower, MyNode.ActivePowerGeneration, MyNode.ReactivePowerGeneration,
                MyNode.FixedVoltageModule, MyNode.MinReactivePower, MyNode.MaxReactivePower
            };

            suppressTelemetryUiEvents = true;
            try
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    paramBoxes[i].Text = vals[i].ToString(inv);
                    // Проверяем на null, так как для i=0 чекбокс и адрес не создавались
                    if (i > 0 && paramChecks[i] != null)
                    {
                        if (MyNode.ParamAutoModes.ContainsKey(keys[i]))
                            paramChecks[i].Checked = MyNode.ParamAutoModes[keys[i]];
                        if (MyNode.ParamRegisters.ContainsKey(keys[i]))
                            addrBoxes[i].Text = MyNode.ParamRegisters[keys[i]];
                    }
                }
            }
            finally
            {
                suppressTelemetryUiEvents = false;
            }

            for (int i = 1; i < keys.Length; i++)
            {
                ApplyTelemetryState(i, null);
            }

            if (paramChecks[1] != null)
            {
                paramChecks[1].Checked = false;
                paramChecks[1].Enabled = false;
            }
            if (addrBoxes[1] != null)
            {
                addrBoxes[1].Text = string.Empty;
                addrBoxes[1].ReadOnly = true;
                addrBoxes[1].BackColor = SystemColors.Control;
            }

            if (paramChecks[3] != null)
            {
                paramChecks[3].Checked = false;
                paramChecks[3].Enabled = false;
            }
            if (addrBoxes[3] != null)
            {
                addrBoxes[3].Text = string.Empty;
                addrBoxes[3].ReadOnly = true;
                addrBoxes[3].BackColor = SystemColors.Control;
            }
            paramBoxes[3].ReadOnly = true;
            paramBoxes[3].BackColor = SystemColors.Control;

            textBoxIP.Text = MyNode.IPAddress;
            textBoxPort.Text = MyNode.Port;
            textBoxID.Text = MyNode.NodeID;
            comboBoxProtocol.SelectedItem = MyNode.Protocol;
            numericMeasurementInterval.Value = MyNode.MeasurementIntervalSeconds;

            for (int i = 1; i < keys.Length; i++)
            {
                if (MyNode.ParamIncrementSteps.ContainsKey(keys[i])) incrementStepBoxes[i].Text = MyNode.ParamIncrementSteps[keys[i]].ToString(inv);
                if (MyNode.ParamIncrementIntervals.ContainsKey(keys[i])) incrementIntervalBoxes[i].Text = MyNode.ParamIncrementIntervals[keys[i]].ToString(inv);
                UpdateIncrementButtonState(i);
            }
        }


        private void ToggleIncrement(int index)
        {
            var inv = CultureInfo.InvariantCulture;
            double step = 1;
            int interval = 2;
            if (double.TryParse(incrementStepBoxes[index].Text.Replace(',', '.'), NumberStyles.Any, inv, out double parsedStep)) step = parsedStep;
            if (int.TryParse(incrementIntervalBoxes[index].Text, out int parsedInterval)) interval = Math.Max(1, parsedInterval);

            MyNode.ParamIncrementSteps[keys[index]] = step;
            MyNode.ParamIncrementIntervals[keys[index]] = interval;

            string id = ParameterAutoChangeService.BuildId(MyNode, keys[index]);
            bool running = ParameterAutoChangeService.TryGet(id, out _, out _, out bool isRunning) && isRunning;
            bool enable = !running;

            ParameterAutoChangeService.Configure(
                id,
                step,
                interval,
                enable,
                () => GetParamValue(index),
                value => SetParamValue(index, value),
                () => BeginInvoke(new Action(() =>
                {
                    paramBoxes[index].Text = GetParamValue(index).ToString(inv);
                    UpdateIncrementButtonState(index);
                })));

            UpdateIncrementButtonState(index);
        }

        private double GetParamValue(int index)
        {
            if (index == 1) return MyNode.InitialVoltage;
            if (index == 2) return MyNode.ActualVoltage;
            if (index == 3) return MyNode.CalculatedVoltage;
            if (index == 4) return MyNode.NominalActivePower;
            if (index == 5) return MyNode.NominalReactivePower;
            if (index == 6) return MyNode.ActivePowerGeneration;
            if (index == 7) return MyNode.ReactivePowerGeneration;
            if (index == 8) return MyNode.FixedVoltageModule;
            if (index == 9) return MyNode.MinReactivePower;
            if (index == 10) return MyNode.MaxReactivePower;
            return 0;
        }

        private void SetParamValue(int index, double value)
        {
            if (index == 1) MyNode.InitialVoltage = value;
            else if (index == 2) MyNode.ActualVoltage = value;
            else if (index == 3) MyNode.CalculatedVoltage = value;
            else if (index == 4) MyNode.NominalActivePower = value;
            else if (index == 5) MyNode.NominalReactivePower = value;
            else if (index == 6) MyNode.ActivePowerGeneration = value;
            else if (index == 7) MyNode.ReactivePowerGeneration = value;
            else if (index == 8) MyNode.FixedVoltageModule = value;
            else if (index == 9) MyNode.MinReactivePower = value;
            else if (index == 10) MyNode.MaxReactivePower = value;
            paramBoxes[index].Text = value.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateIncrementButtonState(int index)
        {
            if (incrementToggleButtons == null || incrementToggleButtons[index] == null) return;
            string id = ParameterAutoChangeService.BuildId(MyNode, keys[index]);
            bool running = ParameterAutoChangeService.TryGet(id, out _, out _, out bool isRunning) && isRunning;
            incrementToggleButtons[index].Text = running ? "Стоп" : "Старт";
            incrementToggleButtons[index].BackColor = running ? Color.FromArgb(252, 165, 165) : Color.FromArgb(191, 219, 254);
            incrementToggleButtons[index].Font = new Font(incrementToggleButtons[index].Font, FontStyle.Bold);
        }

        private void ApplyBoldFonts(Control root)
        {
            root.Font = new Font(root.Font, FontStyle.Bold);
            foreach (Control c in root.Controls) ApplyBoldFonts(c);
        }

        private async Task PollModbusTask()
        {
            if (string.IsNullOrWhiteSpace(textBoxIP.Text)) return;

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(textBoxIP.Text, int.Parse(textBoxPort.Text));
                    if (await Task.WhenAny(connectTask, Task.Delay(1000)) != connectTask) return;

                    var factory = new ModbusFactory();
                    IModbusMaster master = factory.CreateMaster(client);
                    byte slaveId = byte.Parse(textBoxID.Text);

                    for (int i = 1; i < keys.Length; i++) // Начинаем с 1
                    {
                        if (paramChecks[i] != null && paramChecks[i].Checked)
                        {
                            ushort addr;
                            if (ushort.TryParse(addrBoxes[i].Text, out addr))
                            {
                                ushort[] res = await master.ReadHoldingRegistersAsync(slaveId, addr, 1);
                                paramBoxes[i].Text = res[0].ToString();
                                paramBoxes[i].ReadOnly = true;

                                double value = res[0];
                                if (i == 1) MyNode.InitialVoltage = value;
                                else if (i == 2) MyNode.ActualVoltage = value;
                                else if (i == 3) MyNode.CalculatedVoltage = value;
                                else if (i == 4) MyNode.NominalActivePower = value;
                                else if (i == 5) MyNode.NominalReactivePower = value;
                                else if (i == 6) MyNode.ActivePowerGeneration = value;
                                else if (i == 7) MyNode.ReactivePowerGeneration = value;
                                else if (i == 8) MyNode.FixedVoltageModule = value;
                                else if (i == 9) MyNode.MinReactivePower = value;
                                else if (i == 10) MyNode.MaxReactivePower = value;
                            }
                        }
                        else
                        {
                            if (i == 3)
                            {
                                paramBoxes[i].ReadOnly = true;
                                paramBoxes[i].BackColor = SystemColors.Control;
                            }
                            else
                            {
                                paramBoxes[i].ReadOnly = false;
                                paramBoxes[i].BackColor = SystemColors.Window;
                            }
                        }
                    }
                }

                TelemetryUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch { /* Ошибка связи */ }
        }

        private void SaveData()
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                MyNode.Number = int.Parse(paramBoxes[0].Text);
                if (MyNode.Number <= 0)
                {
                    MessageBox.Show("Номер узла должен быть больше 0.");
                    return;
                }
                MyNode.InitialVoltage = double.Parse(paramBoxes[1].Text.Replace(',', '.'), inv);
                MyNode.ActualVoltage = double.Parse(paramBoxes[2].Text.Replace(',', '.'), inv);
                MyNode.NominalActivePower = double.Parse(paramBoxes[4].Text.Replace(',', '.'), inv);
                MyNode.NominalReactivePower = double.Parse(paramBoxes[5].Text.Replace(',', '.'), inv);
                MyNode.ActivePowerGeneration = double.Parse(paramBoxes[6].Text.Replace(',', '.'), inv);
                MyNode.ReactivePowerGeneration = double.Parse(paramBoxes[7].Text.Replace(',', '.'), inv);
                MyNode.FixedVoltageModule = double.Parse(paramBoxes[8].Text.Replace(',', '.'), inv);
                MyNode.MinReactivePower = double.Parse(paramBoxes[9].Text.Replace(',', '.'), inv);
                MyNode.MaxReactivePower = double.Parse(paramBoxes[10].Text.Replace(',', '.'), inv);

                for (int i = 1; i < keys.Length; i++)
                {
                    if (paramChecks[i] != null && i != 1 && i != 3)
                    {
                        MyNode.ParamAutoModes[keys[i]] = paramChecks[i].Checked;
                        MyNode.ParamRegisters[keys[i]] = addrBoxes[i].Text;
                    }
                }

                MyNode.IPAddress = textBoxIP.Text;
                MyNode.Port = textBoxPort.Text;
                MyNode.NodeID = textBoxID.Text;
                MyNode.Protocol = comboBoxProtocol.Text;
                MyNode.MeasurementIntervalSeconds = (int)numericMeasurementInterval.Value;

                for (int i = 1; i < keys.Length; i++)
                {
                    if (double.TryParse(incrementStepBoxes[i].Text.Replace(',', '.'), NumberStyles.Any, inv, out double step))
                        MyNode.ParamIncrementSteps[keys[i]] = step;
                    if (int.TryParse(incrementIntervalBoxes[i].Text, out int interval))
                        MyNode.ParamIncrementIntervals[keys[i]] = Math.Max(1, interval);
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch { MessageBox.Show("Ошибка в числовых полях!"); }
        }

        private async Task RunPing()
        {
            try
            {
                using (Ping p = new Ping())
                {
                    var reply = await p.SendPingAsync(textBoxIP.Text, 1000);
                    textBoxIP.BackColor = (reply.Status == IPStatus.Success) ? Color.LightGreen : Color.LightPink;
                }
            }
            catch { textBoxIP.BackColor = Color.LightPink; }
        }
    }
}
