using System;
using System.Drawing;
using System.Windows.Forms;

namespace PowerGridEditor
{
    public class IncrementSettingsForm : Form
    {
        private readonly NumericUpDown numericStep;
        private readonly NumericUpDown numericInterval;
        private readonly CheckBox checkBoxEnabled;

        public double StepValue { get; private set; }
        public int IntervalSeconds { get; private set; }
        public bool EnabledChange { get; private set; }

        public IncrementSettingsForm(string title, double step, int interval, bool enabled)
        {
            Text = $"Инкремент: {title}";
            Width = 360;
            Height = 220;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            Controls.Add(new Label { Left = 16, Top = 20, Width = 130, Text = "Шаг изменения:" });
            numericStep = new NumericUpDown { Left = 150, Top = 18, Width = 160, DecimalPlaces = 2, Minimum = -100000, Maximum = 100000, Value = (decimal)step };
            Controls.Add(numericStep);

            Controls.Add(new Label { Left = 16, Top = 60, Width = 130, Text = "Интервал (сек):" });
            numericInterval = new NumericUpDown { Left = 150, Top = 58, Width = 160, DecimalPlaces = 0, Minimum = 1, Maximum = 3600, Value = interval };
            Controls.Add(numericInterval);

            checkBoxEnabled = new CheckBox { Left = 16, Top = 95, Width = 280, Text = "Запустить автоматическое изменение", Checked = enabled };
            Controls.Add(checkBoxEnabled);

            var btnOk = new Button { Left = 150, Top = 130, Width = 75, Height = 30, Text = "ОК", DialogResult = DialogResult.OK };
            var btnCancel = new Button { Left = 235, Top = 130, Width = 75, Height = 30, Text = "Отмена", DialogResult = DialogResult.Cancel };
            Controls.Add(btnOk);
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            btnOk.Click += (s, e) =>
            {
                StepValue = (double)numericStep.Value;
                IntervalSeconds = (int)numericInterval.Value;
                EnabledChange = checkBoxEnabled.Checked;
            };
        }
    }
}
