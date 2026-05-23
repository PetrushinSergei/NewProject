using System;
using System.Drawing;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace PowerGridEditor
{
    public partial class BaseNodeForm : Form
    {
        public BaseNode MyBaseNode { get; private set; }
        private readonly string[] keys = { "Number", "U", "Ufact", "Ucalc", "P", "Q", "Pg", "Qg", "Uf", "Qmin", "Qmax" };
        private NumericUpDown numericMeasurementInterval;
        private Timer modelSyncTimer;
        private TextBox[] incrementStepBoxes;
        private TextBox[] incrementIntervalBoxes;
        private Button[] incrementToggleButtons;

        public TextBox NodeNumberTextBox => paramBoxes[0];
        public TextBox InitialVoltageTextBox => paramBoxes[1];
        public TextBox NominalActivePowerTextBox => paramBoxes[2];
        public TextBox NominalReactivePowerTextBox => paramBoxes[3];
        public TextBox ActivePowerGenerationTextBox => paramBoxes[4];
        public TextBox ReactivePowerGenerationTextBox => paramBoxes[5];
        public TextBox FixedVoltageModuleTextBox => paramBoxes[6];
        public TextBox MinReactivePowerTextBox => paramBoxes[7];
        public TextBox MaxReactivePowerTextBox => paramBoxes[8];


        public void BindModel(BaseNode baseNode)
        {
            MyBaseNode = baseNode;
            LoadData();
        }

        public BaseNodeForm()
        {
            InitializeComponent();
            BackColor = Color.FromArgb(245, 250, 255);
            tabParams.BackColor = Color.FromArgb(245, 250, 255);
            tabSettings.BackColor = Color.FromArgb(245, 250, 255);
            MyBaseNode = new BaseNode(0);

            buttonSave.Click += (s, e) => SaveData();
            buttonCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            btnCheckIP.Click += async (s, e) => await RunPing();

            SetupParameterIncrementEditors();
            ApplyBoldFonts(this);

            modelSyncTimer = new Timer { Interval = 700 };
            modelSyncTimer.Tick += (s, e) =>
            {
                if (ContainsFocus && ActiveControl is TextBox) return;
                RefreshFromModel();
            };
            modelSyncTimer.Start();
            FormClosing += (s, e) => modelSyncTimer.Stop();

            LoadData();
        }

        private void SetupExtendedConnectionSettings()
        {
            int sy = 150;
            tabSettings.Controls.Add(new Label { Text = "Интервал измерения (сек):", Location = new Point(15, sy + 3), Size = new Size(170, 20) });
            numericMeasurementInterval = new NumericUpDown { Location = new Point(190, sy), Size = new Size(80, 23), Minimum = 1, Maximum = 3600 };
            tabSettings.Controls.Add(numericMeasurementInterval);
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
                MyBaseNode.Number, MyBaseNode.InitialVoltage, MyBaseNode.ActualVoltage, MyBaseNode.CalculatedVoltage,
                MyBaseNode.NominalActivePower, MyBaseNode.NominalReactivePower, MyBaseNode.ActivePowerGeneration, MyBaseNode.ReactivePowerGeneration,
                MyBaseNode.FixedVoltageModule, MyBaseNode.MinReactivePower, MyBaseNode.MaxReactivePower
            };

            for (int i = 0; i < vals.Length; i++)
            {
                paramBoxes[i].Text = vals[i].ToString(inv);
                if (i > 0)
                {
                    if (MyBaseNode.ParamAutoModes.ContainsKey(keys[i])) paramChecks[i].Checked = MyBaseNode.ParamAutoModes[keys[i]];
                    if (MyBaseNode.ParamRegisters.ContainsKey(keys[i])) addrBoxes[i].Text = MyBaseNode.ParamRegisters[keys[i]];
                }
            }

            textBoxIP.Text = MyBaseNode.IPAddress;
            textBoxPort.Text = MyBaseNode.Port;
            textBoxID.Text = MyBaseNode.DeviceID;
            comboBoxProtocol.SelectedItem = MyBaseNode.Protocol;
            if (comboBoxProtocol.SelectedIndex < 0) comboBoxProtocol.SelectedIndex = 0;
            numericMeasurementInterval.Value = MyBaseNode.MeasurementIntervalSeconds;
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

            for (int i = 1; i < keys.Length; i++)
            {
                if (MyBaseNode.ParamIncrementSteps.ContainsKey(keys[i])) incrementStepBoxes[i].Text = MyBaseNode.ParamIncrementSteps[keys[i]].ToString(inv);
                if (MyBaseNode.ParamIncrementIntervals.ContainsKey(keys[i])) incrementIntervalBoxes[i].Text = MyBaseNode.ParamIncrementIntervals[keys[i]].ToString(inv);
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

            MyBaseNode.ParamIncrementSteps[keys[index]] = step;
            MyBaseNode.ParamIncrementIntervals[keys[index]] = interval;

            string id = ParameterAutoChangeService.BuildId(MyBaseNode, keys[index]);
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
            if (index == 1) return MyBaseNode.InitialVoltage;
            if (index == 2) return MyBaseNode.ActualVoltage;
            if (index == 3) return MyBaseNode.CalculatedVoltage;
            if (index == 4) return MyBaseNode.NominalActivePower;
            if (index == 5) return MyBaseNode.NominalReactivePower;
            if (index == 6) return MyBaseNode.ActivePowerGeneration;
            if (index == 7) return MyBaseNode.ReactivePowerGeneration;
            if (index == 8) return MyBaseNode.FixedVoltageModule;
            if (index == 9) return MyBaseNode.MinReactivePower;
            if (index == 10) return MyBaseNode.MaxReactivePower;
            return 0;
        }

        private void SetParamValue(int index, double value)
        {
            if (index == 1) MyBaseNode.InitialVoltage = value;
            else if (index == 2) MyBaseNode.ActualVoltage = value;
            else if (index == 3) MyBaseNode.CalculatedVoltage = value;
            else if (index == 4) MyBaseNode.NominalActivePower = value;
            else if (index == 5) MyBaseNode.NominalReactivePower = value;
            else if (index == 6) MyBaseNode.ActivePowerGeneration = value;
            else if (index == 7) MyBaseNode.ReactivePowerGeneration = value;
            else if (index == 8) MyBaseNode.FixedVoltageModule = value;
            else if (index == 9) MyBaseNode.MinReactivePower = value;
            else if (index == 10) MyBaseNode.MaxReactivePower = value;
            paramBoxes[index].Text = value.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateIncrementButtonState(int index)
        {
            if (incrementToggleButtons == null || incrementToggleButtons[index] == null) return;
            string id = ParameterAutoChangeService.BuildId(MyBaseNode, keys[index]);
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

        private void SaveData()
        {
            try
            {
                var inv = CultureInfo.InvariantCulture;
                MyBaseNode.Number = int.Parse(paramBoxes[0].Text);
                if (MyBaseNode.Number <= 0)
                {
                    MessageBox.Show("Номер базисного узла должен быть больше 0.");
                    return;
                }
                MyBaseNode.InitialVoltage = double.Parse(paramBoxes[1].Text.Replace(',', '.'), inv);
                MyBaseNode.ActualVoltage = double.Parse(paramBoxes[2].Text.Replace(',', '.'), inv);
                MyBaseNode.NominalActivePower = double.Parse(paramBoxes[4].Text.Replace(',', '.'), inv);
                MyBaseNode.NominalReactivePower = double.Parse(paramBoxes[5].Text.Replace(',', '.'), inv);
                MyBaseNode.ActivePowerGeneration = double.Parse(paramBoxes[6].Text.Replace(',', '.'), inv);
                MyBaseNode.ReactivePowerGeneration = double.Parse(paramBoxes[7].Text.Replace(',', '.'), inv);
                MyBaseNode.FixedVoltageModule = double.Parse(paramBoxes[8].Text.Replace(',', '.'), inv);
                MyBaseNode.MinReactivePower = double.Parse(paramBoxes[9].Text.Replace(',', '.'), inv);
                MyBaseNode.MaxReactivePower = double.Parse(paramBoxes[10].Text.Replace(',', '.'), inv);

                for (int i = 1; i < keys.Length; i++)
                {
                    if (i != 1 && i != 3)
                    {
                        MyBaseNode.ParamAutoModes[keys[i]] = paramChecks[i].Checked;
                        MyBaseNode.ParamRegisters[keys[i]] = addrBoxes[i].Text;
                    }
                }

                MyBaseNode.Protocol = comboBoxProtocol.Text;
                MyBaseNode.IPAddress = textBoxIP.Text;
                MyBaseNode.Port = textBoxPort.Text;
                MyBaseNode.DeviceID = textBoxID.Text;
                MyBaseNode.MeasurementIntervalSeconds = (int)numericMeasurementInterval.Value;
                for (int i = 1; i < keys.Length; i++)
                {
                    if (double.TryParse(incrementStepBoxes[i].Text.Replace(',', '.'), NumberStyles.Any, inv, out double step))
                        MyBaseNode.ParamIncrementSteps[keys[i]] = step;
                    if (int.TryParse(incrementIntervalBoxes[i].Text, out int interval))
                        MyBaseNode.ParamIncrementIntervals[keys[i]] = Math.Max(1, interval);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch
            {
                MessageBox.Show("Ошибка в числовых полях!");
            }
        }

        private async System.Threading.Tasks.Task RunPing()
        {
            try
            {
                using (Ping p = new Ping())
                {
                    var reply = await p.SendPingAsync(textBoxIP.Text, 1000);
                    textBoxIP.BackColor = reply.Status == IPStatus.Success ? Color.LightGreen : Color.LightPink;
                }
            }
            catch
            {
                textBoxIP.BackColor = Color.LightPink;
            }
        }
    }
}
