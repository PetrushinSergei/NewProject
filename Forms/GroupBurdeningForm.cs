using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace PowerGridEditor
{
    public sealed class GroupBurdeningForm : Form
    {
        private const string SelectColumnName = "SelectColumn";
        private const string TgColumnName = "TgColumn";

        private readonly DataGridView nodesGrid;
        private readonly TextBox textIntervalSeconds;
        private readonly ComboBox comboStepType;
        private readonly TextBox textStepValue;
        private readonly Button buttonStart;
        private readonly Button buttonStop;
        private readonly Timer burdeningTimer;
        private bool selectAllChecked;
        private bool tgAllChecked;
        private bool updatingCheckBoxes;

        public GroupBurdeningForm(IEnumerable<GraphicNode> nodes)
        {
            Text = "Групповое утяжеление";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 420);
            Size = new Size(860, 520);

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
            nodesGrid.CellPainting += NodesGrid_CellPainting;
            nodesGrid.ColumnHeaderMouseClick += NodesGrid_ColumnHeaderMouseClick;
            nodesGrid.CellValueChanged += NodesGrid_CellValueChanged;

            nodesGrid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = SelectColumnName,
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
                Name = TgColumnName,
                HeaderText = "Tg",
                FillWeight = 70
            });

            var controlsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 78,
                Padding = new Padding(10, 8, 10, 8),
                ColumnCount = 10,
                RowCount = 2
            };
            controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
            controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20));
            controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            controlsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            controlsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            controlsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));

            textIntervalSeconds = new TextBox { Dock = DockStyle.Fill, Text = "1" };
            comboStepType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            comboStepType.Items.AddRange(new object[] { "МВт", "%" });
            comboStepType.SelectedIndex = 0;
            textStepValue = new TextBox { Dock = DockStyle.Fill, Text = "1" };
            buttonStart = new Button { Dock = DockStyle.Fill, Text = "Старт" };
            buttonStop = new Button { Dock = DockStyle.Fill, Text = "Стоп", Enabled = false };

            controlsPanel.Controls.Add(CreateControlLabel("Интервал (сек)"), 0, 0);
            controlsPanel.Controls.Add(textIntervalSeconds, 1, 0);
            controlsPanel.Controls.Add(CreateControlLabel("Тип шага"), 3, 0);
            controlsPanel.Controls.Add(comboStepType, 4, 0);
            controlsPanel.Controls.Add(CreateControlLabel("Величина шага"), 6, 0);
            controlsPanel.Controls.Add(textStepValue, 7, 0);
            controlsPanel.Controls.Add(buttonStart, 8, 0);
            controlsPanel.Controls.Add(buttonStop, 9, 0);

            buttonStart.Click += buttonStart_Click;
            buttonStop.Click += (s, e) => StopBurdeningTimer();

            burdeningTimer = new Timer();
            burdeningTimer.Tick += (s, e) => ApplyBurdeningStep();

            Controls.Add(nodesGrid);
            Controls.Add(controlsPanel);
            FormClosing += (s, e) => StopBurdeningTimer();

            RefreshNodes(nodes);
        }

        public void RefreshNodes(IEnumerable<GraphicNode> nodes)
        {
            nodesGrid.Rows.Clear();
            if (nodes == null)
            {
                UpdateHeaderCheckStates();
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
                nodesGrid.Rows[rowIndex].Tag = node;
            }

            UpdateHeaderCheckStates();
        }

        private void NodesGrid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1 || !IsSelectAllColumn(e.ColumnIndex))
            {
                return;
            }

            e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentForeground);

            string columnName = nodesGrid.Columns[e.ColumnIndex].Name;
            bool isChecked = GetHeaderCheckState(columnName);
            System.Windows.Forms.VisualStyles.CheckBoxState state = isChecked
                ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal
                : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
            Size checkBoxSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
            int checkBoxX = e.CellBounds.Left + 6;
            int checkBoxY = e.CellBounds.Top + (e.CellBounds.Height - checkBoxSize.Height) / 2;
            CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(checkBoxX, checkBoxY), state);

            Rectangle textBounds = new Rectangle(
                checkBoxX + checkBoxSize.Width + 4,
                e.CellBounds.Top + 1,
                e.CellBounds.Width - checkBoxSize.Width - 12,
                e.CellBounds.Height - 2);
            TextRenderer.DrawText(
                e.Graphics,
                Convert.ToString(e.FormattedValue, CultureInfo.CurrentCulture),
                e.CellStyle.Font,
                textBounds,
                e.CellStyle.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            e.Handled = true;
        }

        private void NodesGrid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (!IsSelectAllColumn(e.ColumnIndex))
            {
                return;
            }

            string columnName = nodesGrid.Columns[e.ColumnIndex].Name;
            SetColumnCheckState(columnName, !GetHeaderCheckState(columnName));
        }

        private void NodesGrid_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (updatingCheckBoxes || e.RowIndex < 0 || !IsSelectAllColumn(e.ColumnIndex))
            {
                return;
            }

            UpdateHeaderCheckState(nodesGrid.Columns[e.ColumnIndex].Name);
        }

        private bool IsSelectAllColumn(int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= nodesGrid.Columns.Count)
            {
                return false;
            }

            string columnName = nodesGrid.Columns[columnIndex].Name;
            return columnName == SelectColumnName || columnName == TgColumnName;
        }

        private bool GetHeaderCheckState(string columnName)
        {
            return columnName == SelectColumnName ? selectAllChecked : tgAllChecked;
        }

        private void SetHeaderCheckState(string columnName, bool isChecked)
        {
            if (columnName == SelectColumnName)
            {
                selectAllChecked = isChecked;
            }
            else if (columnName == TgColumnName)
            {
                tgAllChecked = isChecked;
            }

            nodesGrid.InvalidateCell(nodesGrid.Columns[columnName].HeaderCell);
        }

        private void SetColumnCheckState(string columnName, bool isChecked)
        {
            nodesGrid.EndEdit();

            updatingCheckBoxes = true;
            try
            {
                foreach (DataGridViewRow row in nodesGrid.Rows)
                {
                    if (!row.IsNewRow)
                    {
                        row.Cells[columnName].Value = isChecked;
                    }
                }
            }
            finally
            {
                updatingCheckBoxes = false;
            }

            SetHeaderCheckState(columnName, HasRows() && AreAllRowsChecked(columnName));
        }

        private void UpdateHeaderCheckStates()
        {
            UpdateHeaderCheckState(SelectColumnName);
            UpdateHeaderCheckState(TgColumnName);
        }

        private void UpdateHeaderCheckState(string columnName)
        {
            SetHeaderCheckState(columnName, HasRows() && AreAllRowsChecked(columnName));
        }

        private bool HasRows()
        {
            return nodesGrid.Rows.Cast<DataGridViewRow>().Any(row => !row.IsNewRow);
        }

        private bool AreAllRowsChecked(string columnName)
        {
            return nodesGrid.Rows.Cast<DataGridViewRow>()
                .Where(row => !row.IsNewRow)
                .All(row => row.Cells[columnName].Value is bool selected && selected);
        }

        private static Label CreateControlLabel(string text)
        {
            return new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
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
                if (!IsRowSelected(row) || !(row.Tag is GraphicNode node))
                {
                    continue;
                }

                double currentLoad = node.Data.NominalActivePower;
                double newLoad = usePercent
                    ? currentLoad + currentLoad * stepValue / 100.0
                    : currentLoad + stepValue;

                node.Data.NominalActivePower = newLoad;
                row.Cells["LoadPColumn"].Value = FormatLoadValue(newLoad);
            }
        }

        private bool TryReadTimerSettings(out int intervalMilliseconds, out double stepValue)
        {
            intervalMilliseconds = 0;
            stepValue = 0.0;

            if (!TryParsePositiveDouble(textIntervalSeconds.Text, out double intervalSeconds))
            {
                MessageBox.Show(this, "Введите положительное значение в поле \"Интервал (сек)\".", "Групповое утяжеление", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!TryParseDouble(textStepValue.Text, out stepValue))
            {
                MessageBox.Show(this, "Введите числовое значение в поле \"Величина шага\".", "Групповое утяжеление", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            intervalMilliseconds = Math.Max(1, (int)Math.Round(intervalSeconds * 1000.0, MidpointRounding.AwayFromZero));
            return true;
        }

        private static bool IsRowSelected(DataGridViewRow row)
        {
            return row != null
                && row.Cells[SelectColumnName].Value is bool selected
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
    }
}
