using System;
using System.Drawing;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace PowerGridEditor
{
    public partial class ShuntForm : Form
    {
        public Shunt MyShunt { get; private set; }
        private readonly string[] keys = { "Start", "R", "X" };
        private NumericUpDown numericMeasurementInterval;
        private Timer modelSyncTimer;
        private TextBox[] incrementStepBoxes;
        private TextBox[] incrementIntervalBoxes;
        private Button[] incrementToggleButtons;

        public TextBox StartNodeTextBox => paramBoxes[0];
        public TextBox ActiveResistanceTextBox => paramBoxes[1];
        public TextBox ReactiveResistanceTextBox => paramBoxes[2];


        public void BindModel(Shunt shunt)
        {
            MyShunt = shunt;
            LoadData();
        }

        public ShuntForm()
        {
            InitializeComponent();
            BackColor = Color.FromArgb(245, 250, 255);
            tabParams.BackColor = Color.FromArgb(245, 250, 255);
            tabSettings.BackColor = Color.FromArgb(245, 250, 255);
            MyShunt = new Shunt(0);

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
            incrementStepBoxes = new TextBox[3];
            incrementIntervalBoxes = new TextBox[3];
            incrementToggleButtons = new Button[3];

            tabParams.Controls.Add(new Label { Text = "Шаг:", Location = new Point(520, 2), Size = new Size(45, 18) });
            tabParams.Controls.Add(new Label { Text = "Инт.,с:", Location = new Point(585, 2), Size = new Size(55, 18) });
            tabParams.Controls.Add(new Label { Text = "Авто изм.", Location = new Point(650, 2), Size = new Size(80, 18) });

            for (int i = 1; i < 3; i++)
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
            paramBoxes[0].Text = MyShunt.StartNodeNumber.ToString(inv);
            paramBoxes[1].Text = MyShunt.ActiveResistance.ToString(inv);
            paramBoxes[2].Text = MyShunt.ReactiveResistance.ToString(inv);

            for (int i = 1; i < 3; i++)
            {
                if (MyShunt.ParamAutoModes.ContainsKey(keys[i])) paramChecks[i].Checked = MyShunt.ParamAutoModes[keys[i]];
                if (MyShunt.ParamRegisters.ContainsKey(keys[i])) addrBoxes[i].Text = MyShunt.ParamRegisters[keys[i]];
            }

            textBoxIP.Text = MyShunt.IPAddress;
            textBoxPort.Text = MyShunt.Port;
            textBoxID.Text = MyShunt.DeviceID;
            comboBoxProtocol.SelectedItem = MyShunt.Protocol;
            if (comboBoxProtocol.SelectedIndex < 0) comboBoxProtocol.SelectedIndex = 0;
            numericMeasurementInterval.Value = MyShunt.MeasurementIntervalSeconds;
            for (int i = 1; i < 3; i++)
            {
                if (MyShunt.ParamIncrementSteps.ContainsKey(keys[i])) incrementStepBoxes[i].Text = MyShunt.ParamIncrementSteps[keys[i]].ToString(inv);
                if (MyShunt.ParamIncrementIntervals.ContainsKey(keys[i])) incrementIntervalBoxes[i].Text = MyShunt.ParamIncrementIntervals[keys[i]].ToString(inv);
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

            MyShunt.ParamIncrementSteps[keys[index]] = step;
            MyShunt.ParamIncrementIntervals[keys[index]] = interval;

            string id = ParameterAutoChangeService.BuildId(MyShunt, keys[index]);
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
            if (index == 1) return MyShunt.ActiveResistance;
            if (index == 2) return MyShunt.ReactiveResistance;
            return 0;
        }

        private void SetParamValue(int index, double value)
        {
            if (index == 1) MyShunt.ActiveResistance = value;
            else if (index == 2) MyShunt.ReactiveResistance = value;
            paramBoxes[index].Text = value.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateIncrementButtonState(int index)
        {
            if (incrementToggleButtons == null || incrementToggleButtons[index] == null) return;
            string id = ParameterAutoChangeService.BuildId(MyShunt, keys[index]);
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
                MyShunt.StartNodeNumber = int.Parse(paramBoxes[0].Text);
                MyShunt.ActiveResistance = double.Parse(paramBoxes[1].Text.Replace(',', '.'), inv);
                MyShunt.ReactiveResistance = double.Parse(paramBoxes[2].Text.Replace(',', '.'), inv);

                for (int i = 1; i < 3; i++)
                {
                    MyShunt.ParamAutoModes[keys[i]] = paramChecks[i].Checked;
                    MyShunt.ParamRegisters[keys[i]] = addrBoxes[i].Text;
                }

                MyShunt.Protocol = comboBoxProtocol.Text;
                MyShunt.IPAddress = textBoxIP.Text;
                MyShunt.Port = textBoxPort.Text;
                MyShunt.DeviceID = textBoxID.Text;
                MyShunt.MeasurementIntervalSeconds = (int)numericMeasurementInterval.Value;
                for (int i = 1; i < 3; i++)
                {
                    if (double.TryParse(incrementStepBoxes[i].Text.Replace(',', '.'), NumberStyles.Any, inv, out double step))
                        MyShunt.ParamIncrementSteps[keys[i]] = step;
                    if (int.TryParse(incrementIntervalBoxes[i].Text, out int interval))
                        MyShunt.ParamIncrementIntervals[keys[i]] = Math.Max(1, interval);
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
