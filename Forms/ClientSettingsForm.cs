using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows.Forms;

namespace PowerGridEditor
{
    public class ClientSettingsForm : Form
    {
        private sealed class AdapterEntry
        {
            public string Name { get; set; }
            public string Description { get; set; }

            public override string ToString()
            {
                return $"{Name} ({Description})";
            }
        }

        private readonly ComboBox comboBoxAdapters;
        private readonly TextBox textBoxIp;
        private readonly TextBox textBoxMask;
        private readonly TextBox textBoxGateway;
        private readonly NumericUpDown numericUpdateInterval;

        public ClientSettingsForm()
        {
            Text = "Настройка клиента";
            Width = 980;
            Height = 250;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(214, 227, 242),
                Padding = new Padding(12)
            };
            Controls.Add(root);

            var labelAdapter = new Label { Left = 18, Top = 24, Width = 95, Text = "Адаптер:", Font = new Font("Segoe UI", 12F) };
            comboBoxAdapters = new ComboBox { Left = 115, Top = 20, Width = 640, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 12F) };

            var labelIp = new Label { Left = 18, Top = 72, Width = 40, Text = "IP:", Font = new Font("Segoe UI", 12F) };
            textBoxIp = new TextBox { Left = 55, Top = 70, Width = 220, Font = new Font("Segoe UI", 12F) };

            var labelMask = new Label { Left = 290, Top = 72, Width = 65, Text = "Маска:", Font = new Font("Segoe UI", 12F) };
            textBoxMask = new TextBox { Left = 355, Top = 70, Width = 160, Font = new Font("Segoe UI", 12F), Text = "255.255.255.0" };

            var labelGateway = new Label { Left = 530, Top = 72, Width = 65, Text = "Шлюз:", Font = new Font("Segoe UI", 12F) };
            textBoxGateway = new TextBox { Left = 595, Top = 70, Width = 160, Font = new Font("Segoe UI", 12F) };

            var buttonApply = new Button
            {
                Left = 770,
                Top = 18,
                Width = 170,
                Height = 92,
                Text = "Применить\r\nIP",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            buttonApply.Click += ButtonApply_Click;

            var labelInterval = new Label { Left = 18, Top = 122, Width = 260, Text = "Интервал обновления данных (сек):", Font = new Font("Segoe UI", 10F) };
            numericUpdateInterval = new NumericUpDown { Left = 280, Top = 120, Width = 100, Minimum = 1, Maximum = 3600, Value = AppRuntimeSettings.UpdateIntervalSeconds };
            numericUpdateInterval.ValueChanged += (s, e) => AppRuntimeSettings.UpdateIntervalSeconds = (int)numericUpdateInterval.Value;

            var hint = new Label
            {
                Left = 18,
                Top = 156,
                Width = 900,
                Text = "Настройка адаптера: выберите интерфейс и задайте статический IP.",
                ForeColor = Color.FromArgb(43, 71, 104),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic)
            };

            root.Controls.Add(labelAdapter);
            root.Controls.Add(comboBoxAdapters);
            root.Controls.Add(labelIp);
            root.Controls.Add(textBoxIp);
            root.Controls.Add(labelMask);
            root.Controls.Add(textBoxMask);
            root.Controls.Add(labelGateway);
            root.Controls.Add(textBoxGateway);
            root.Controls.Add(buttonApply);
            root.Controls.Add(labelInterval);
            root.Controls.Add(numericUpdateInterval);
            root.Controls.Add(hint);

            Load += (s, e) => LoadNetworkAdapters();
            Load += (s, e) => numericUpdateInterval.Value = AppRuntimeSettings.UpdateIntervalSeconds;
        }

        private void LoadNetworkAdapters()
        {
            comboBoxAdapters.Items.Clear();
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                comboBoxAdapters.Items.Add(new AdapterEntry
                {
                    Name = adapter.Name,
                    Description = adapter.Description
                });
            }

            if (comboBoxAdapters.Items.Count > 0)
            {
                comboBoxAdapters.SelectedIndex = 0;
            }
        }

        private void ButtonApply_Click(object sender, EventArgs e)
        {
            if (comboBoxAdapters.SelectedItem == null)
            {
                MessageBox.Show("Выберите сетевой адаптер", "Сеть", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IPAddress.TryParse(textBoxIp.Text, out _) ||
                !IPAddress.TryParse(textBoxMask.Text, out _) ||
                !IPAddress.TryParse(textBoxGateway.Text, out _))
            {
                MessageBox.Show("Проверьте корректность IP/маски/шлюза", "Сеть", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedAdapter = comboBoxAdapters.SelectedItem as AdapterEntry;
            if (selectedAdapter == null)
            {
                MessageBox.Show("Не удалось определить выбранный адаптер", "Сеть", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string args = $"interface ip set address name=\"{selectedAdapter.Name}\" static {textBoxIp.Text} {textBoxMask.Text} {textBoxGateway.Text}";

            try
            {
                var process = new Process();
                process.StartInfo.FileName = "netsh";
                process.StartInfo.Arguments = args;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(866);
                process.StartInfo.StandardErrorEncoding = Encoding.GetEncoding(866);
                process.Start();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    MessageBox.Show("Статический IP успешно применён", "Сеть", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    string error = process.StandardError.ReadToEnd();
                    if (string.IsNullOrWhiteSpace(error))
                    {
                        error = process.StandardOutput.ReadToEnd();
                    }

                    MessageBox.Show("Не удалось применить IP: " + error, "Сеть", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка конфигурации сети: " + ex.Message, "Сеть", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
