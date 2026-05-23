using System;
using System.Globalization;
using System.Windows.Forms;

namespace PowerGridEditor
{
    public class CalculationSettingsForm : Form
    {
        private readonly TextBox textPrecision;
        private readonly NumericUpDown numIterations;

        public CalculationSettingsForm()
        {
            Text = "Параметры расчёта";
            Width = 360;
            Height = 190;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var labelPrecision = new Label { Left = 20, Top = 20, Width = 150, Text = "Точность (epsilon):" };
            textPrecision = new TextBox { Left = 180, Top = 16, Width = 130, Text = CalculationOptions.Precision.ToString(CultureInfo.InvariantCulture) };

            var labelIter = new Label { Left = 20, Top = 58, Width = 150, Text = "Макс. итераций:" };
            numIterations = new NumericUpDown { Left = 180, Top = 54, Width = 130, Minimum = 1, Maximum = 1000, Value = CalculationOptions.MaxIterations };

            var btnSave = new Button { Left = 180, Top = 96, Width = 130, Text = "Сохранить" };
            btnSave.Click += SaveSettings;

            Controls.Add(labelPrecision);
            Controls.Add(textPrecision);
            Controls.Add(labelIter);
            Controls.Add(numIterations);
            Controls.Add(btnSave);
        }

        private void SaveSettings(object sender, EventArgs e)
        {
            if (!double.TryParse(textPrecision.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var eps) || eps <= 0)
            {
                MessageBox.Show("Некорректная точность.", "Параметры расчёта", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CalculationOptions.Precision = eps;
            CalculationOptions.MaxIterations = (int)numIterations.Value;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
