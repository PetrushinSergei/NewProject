using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PowerGridEditor
{
    public sealed class BurdeningForm : Form
    {
        private const string DialogTitle = "Утяжеление";

        private readonly CheckBox checkSelectAll;
        private readonly DataGridView nodesGrid;
        private readonly TextBox textIntervalSeconds;
        private readonly ComboBox comboStepType;
        private readonly TextBox textStepValue;
        private readonly Button buttonStart;
        private readonly Button buttonStop;
        private readonly Timer burdeningTimer;
        private bool updatingSelectAll;

        public BurdeningForm(IEnumerable<GraphicNode> nodes)
        {
            Text = DialogTitle;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 420);
            Size = new Size(920, 520);

            checkSelectAll = new CheckBox
            {
                AutoSize = true,
                Text = "Выбрать все",
                Margin = new Padding(10, 8, 0, 6)
            };
            checkSelectAll.CheckedChanged += checkSelectAll_CheckedChanged;

            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            topPanel.Controls.Add(checkSelectAll);

            nodesGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            nodesGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (nodesGrid.IsCurrentCellDirty)
                {
                    nodesGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            nodesGrid.CellValueChanged += nodesGrid_CellValueChanged;

            nodesGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "SelectColumn",
                HeaderText = "Выбор",
                FillWeight = 70
            });
            nodesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NodeNumberColumn",
                HeaderText = "Номер узла",
                ReadOnly = true,
                FillWeight = 95
            });
            nodesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LoadPColumn",
                HeaderText = "Нагрузка P",
                ReadOnly = true,
                FillWeight = 110
            });
            nodesGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "LoadQColumn",
                HeaderText = "Нагрузка Q",
                ReadOnly = true,
                FillWeight = 110
            });
            nodesGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "TgColumn",
                HeaderText = "Tg",
                FillWeight = 70
            });

            var controlsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                Padding = new Padding(10, 12, 10, 10),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            textIntervalSeconds = new TextBox { Text = "1", Width = 80, Height = 24, Margin = new Padding(6, 0, 18, 0) };
            comboStepType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Height = 24, Margin = new Padding(6, 0, 18, 0) };
            comboStepType.Items.AddRange(new object[] { "МВт", "%" });
            comboStepType.SelectedIndex = 0;
            textStepValue = new TextBox { Text = "1", Width = 90, Height = 24, Margin = new Padding(6, 0, 18, 0) };
            buttonStart = new Button { Text = "Старт", Width = 90, Height = 28, Margin = new Padding(0, 0, 8, 0) };
            buttonStop = new Button { Text = "Стоп", Width = 90, Height = 28, Margin = new Padding(0), Enabled = false };

            controlsPanel.Controls.Add(CreateControlLabel("Интервал (сек)"));
            controlsPanel.Controls.Add(textIntervalSeconds);
            controlsPanel.Controls.Add(CreateControlLabel("Тип шага"));
            controlsPanel.Controls.Add(comboStepType);
            controlsPanel.Controls.Add(CreateControlLabel("Величина шага"));
            controlsPanel.Controls.Add(textStepValue);
            controlsPanel.Controls.Add(buttonStart);
            controlsPanel.Controls.Add(buttonStop);

            buttonStart.Click += buttonStart_Click;
            buttonStop.Click += (s, e) => StopBurdeningTimer();

            burdeningTimer = new Timer();
            burdeningTimer.Tick += (s, e) => ApplyBurdeningStep();

            Controls.Add(nodesGrid);
            Controls.Add(controlsPanel);
            Controls.Add(topPanel);
            FormClosing += BurdeningForm_FormClosing;

            RefreshNodes(nodes);
        }

        public void RefreshNodes(IEnumerable<GraphicNode> nodes)
        {
            nodesGrid.Rows.Clear();
            if (nodes == null)
            {
                UpdateSelectAllState();
                return;
            }

            foreach (var node in nodes.Where(IsPqNode).OrderBy(x => x.Data.Number))
            {
                int rowIndex = nodesGrid.Rows.Add(
                    false,
                    node.Data.Number.ToString(CultureInfo.InvariantCulture),
                    FormatLoadValue(node.Data.NominalActivePower),
                    FormatLoadValue(node.Data.NominalReactivePower),
                    false);
                nodesGrid.Rows[rowIndex].Tag = new BurdeningRowState(node);
            }

            UpdateSelectAllState();
        }

        private static Label CreateControlLabel(string text)
        {
            return new Label
            {
                AutoSize = true,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 4, 0, 0)
            };
        }

        private void checkSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            if (updatingSelectAll)
            {
                return;
            }

            updatingSelectAll = true;
            try
            {
                foreach (DataGridViewRow row in nodesGrid.Rows)
                {
                    row.Cells["SelectColumn"].Value = checkSelectAll.Checked;
                }
            }
            finally
            {
                updatingSelectAll = false;
            }
        }

        private void nodesGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            if (nodesGrid.Columns[e.ColumnIndex].Name == "SelectColumn")
            {
                UpdateSelectAllState();
            }
        }

        private void UpdateSelectAllState()
        {
            if (updatingSelectAll)
            {
                return;
            }

            updatingSelectAll = true;
            try
            {
                checkSelectAll.Checked = nodesGrid.Rows.Count > 0
                    && nodesGrid.Rows.Cast<DataGridViewRow>().All(IsRowSelected);
            }
            finally
            {
                updatingSelectAll = false;
            }
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            nodesGrid.EndEdit();

            if (!TryReadTimerSettings(out int intervalMilliseconds, out _))
            {
                return;
            }

            burdeningTimer.Interval = intervalMilliseconds;
            burdeningTimer.Start();
            buttonStart.Enabled = false;
            buttonStop.Enabled = true;
        }

        private void StopBurdeningTimer()
        {
            burdeningTimer.Stop();
            buttonStart.Enabled = true;
            buttonStop.Enabled = false;
        }

        private void ApplyBurdeningStep()
        {
            nodesGrid.EndEdit();

            if (!TryReadTimerSettings(out _, out double stepValue))
            {
                StopBurdeningTimer();
                return;
            }

            bool usePercent = Convert.ToString(comboStepType.SelectedItem, CultureInfo.InvariantCulture) == "%";
            foreach (DataGridViewRow row in nodesGrid.Rows)
            {
                var rowState = row.Tag as BurdeningRowState;
                if (!IsRowSelected(row) || rowState == null)
                {
                    continue;
                }

                var node = rowState.Node;
                double currentLoad = node.Data.NominalActivePower;
                double newLoad = usePercent
                    ? currentLoad + currentLoad * stepValue / 100.0
                    : currentLoad + stepValue;

                node.Data.NominalActivePower = newLoad;
                row.Cells["LoadPColumn"].Value = FormatLoadValue(newLoad);

                if (IsTgEnabled(row) && Math.Abs(rowState.InitialP) > 1e-9)
                {
                    double newReactiveLoad = newLoad * (rowState.InitialQ / rowState.InitialP);
                    node.Data.NominalReactivePower = newReactiveLoad;
                    row.Cells["LoadQColumn"].Value = FormatLoadValue(newReactiveLoad);
                }
            }
        }

        private bool TryReadTimerSettings(out int intervalMilliseconds, out double stepValue)
        {
            intervalMilliseconds = 0;
            stepValue = 0.0;

            if (!TryParsePositiveDouble(textIntervalSeconds.Text, out double intervalSeconds))
            {
                MessageBox.Show(this, "Введите положительное значение в поле \"Интервал (сек)\".", DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!TryParseDouble(textStepValue.Text, out stepValue))
            {
                MessageBox.Show(this, "Введите числовое значение в поле \"Величина шага\".", DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            intervalMilliseconds = Math.Max(1, (int)Math.Round(intervalSeconds * 1000.0, MidpointRounding.AwayFromZero));
            return true;
        }

        private void BurdeningForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing)
            {
                return;
            }

            e.Cancel = true;
            Hide();
        }

        private static bool IsRowSelected(DataGridViewRow row)
        {
            return row != null
                && row.Cells["SelectColumn"].Value is bool selected
                && selected;
        }

        private static bool IsTgEnabled(DataGridViewRow row)
        {
            return row != null
                && row.Cells["TgColumn"].Value is bool selected
                && selected;
        }

        private static bool TryParsePositiveDouble(string text, out double value)
        {
            return TryParseDouble(text, out value) && value > 0.0;
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string FormatLoadValue(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static bool IsPqNode(GraphicNode node)
        {
            return node != null
                && node.Data != null
                && Math.Abs(node.Data.FixedVoltageModule) < 1e-9;
        }

        private sealed class BurdeningRowState
        {
            public BurdeningRowState(GraphicNode node)
            {
                Node = node;
                InitialP = node.Data.NominalActivePower;
                InitialQ = node.Data.NominalReactivePower;
            }

            public GraphicNode Node { get; }
            public double InitialP { get; }
            public double InitialQ { get; }
        }
    }
}
