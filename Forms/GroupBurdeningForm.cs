using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PowerGridEditor
{
    public sealed class GroupBurdeningForm : Form
    {
        private const string DialogTitle = "Групповое утяжеление";

        private readonly TabControl tabControl;
        private readonly TabPage settingsTabPage;
        private readonly TabPage resultsTabPage;
        private readonly DataGridView nodesGrid;
        private readonly DataGridView resultsGrid;
        private readonly TextBox textIntervalSeconds;
        private readonly ComboBox comboStepType;
        private readonly TextBox textStepValue;
        private readonly Button buttonStart;
        private readonly Button buttonStop;
        private readonly Timer burdeningTimer;
        private readonly List<GroupBurdeningNodeState> selectedNodeStates = new List<GroupBurdeningNodeState>();
        private int burdeningStepNumber;

        public GroupBurdeningForm(IEnumerable<GraphicNode> nodes)
        {
            Text = DialogTitle;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 420);
            Size = new Size(860, 520);

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };
            settingsTabPage = new TabPage("Настройка");
            resultsTabPage = new TabPage("Протокол расчета");

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

            resultsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

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

            settingsTabPage.Controls.Add(nodesGrid);
            settingsTabPage.Controls.Add(controlsPanel);
            resultsTabPage.Controls.Add(resultsGrid);
            tabControl.TabPages.Add(settingsTabPage);
            tabControl.TabPages.Add(resultsTabPage);
            Controls.Add(tabControl);
            FormClosing += (s, e) => StopBurdeningTimer();

            RefreshNodes(nodes);
        }

        public void RefreshNodes(IEnumerable<GraphicNode> nodes)
        {
            nodesGrid.Rows.Clear();
            if (nodes == null)
            {
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

            if (!PrepareResultsProtocol())
            {
                return;
            }

            burdeningTimer.Interval = intervalMilliseconds;
            burdeningTimer.Start();
            buttonStart.Enabled = false;
            buttonStop.Enabled = true;
            tabControl.SelectedTab = resultsTabPage;
        }

        private void StopBurdeningTimer()
        {
            burdeningTimer.Stop();
            buttonStart.Enabled = true;
            buttonStop.Enabled = false;
        }

        private bool PrepareResultsProtocol()
        {
            selectedNodeStates.Clear();
            burdeningStepNumber = 0;

            foreach (DataGridViewRow row in nodesGrid.Rows)
            {
                if (!IsRowSelected(row) || !(row.Tag is GraphicNode node))
                {
                    continue;
                }

                selectedNodeStates.Add(new GroupBurdeningNodeState(row, node, IsTgEnabled(row)));
            }

            if (selectedNodeStates.Count == 0)
            {
                MessageBox.Show(this, "Выберите хотя бы один узел для группового утяжеления.", DialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            resultsGrid.Columns.Clear();
            resultsGrid.Rows.Clear();
            resultsGrid.Columns.Add(CreateReadOnlyTextColumn("StepNumberColumn", "Номер"));
            resultsGrid.Columns.Add(CreateReadOnlyTextColumn("BurdeningStepColumn", "Шаг"));

            foreach (var state in selectedNodeStates)
            {
                string nodeNumber = state.Node.Data.Number.ToString(CultureInfo.InvariantCulture);
                resultsGrid.Columns.Add(CreateReadOnlyTextColumn($"LoadP_{nodeNumber}", $"Pн_{nodeNumber}"));
                resultsGrid.Columns.Add(CreateReadOnlyTextColumn($"LoadQ_{nodeNumber}", $"Qн_{nodeNumber}"));
            }

            AddResultsProtocolRow(0.0);
            return true;
        }

        private static DataGridViewTextBoxColumn CreateReadOnlyTextColumn(string name, string headerText)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = headerText,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
        }

        private void ApplyBurdeningStep()
        {
            nodesGrid.EndEdit();

            if (!TryReadTimerSettings(out _, out double stepValue))
            {
                StopBurdeningTimer();
                return;
            }

            if (selectedNodeStates.Count == 0)
            {
                StopBurdeningTimer();
                return;
            }

            bool usePercent = Convert.ToString(comboStepType.SelectedItem, CultureInfo.InvariantCulture) == "%";
            foreach (var state in selectedNodeStates)
            {
                var node = state.Node;
                double currentLoad = node.Data.NominalActivePower;
                double newLoad = usePercent
                    ? currentLoad + currentLoad * stepValue / 100.0
                    : currentLoad + stepValue;

                node.Data.NominalActivePower = newLoad;
                state.Row.Cells["LoadPColumn"].Value = FormatLoadValue(newLoad);

                if (state.UseTg && Math.Abs(state.InitialP) > 1e-9)
                {
                    double newReactiveLoad = newLoad * (state.InitialQ / state.InitialP);
                    node.Data.NominalReactivePower = newReactiveLoad;
                }

                state.Row.Cells["LoadQColumn"].Value = FormatLoadValue(node.Data.NominalReactivePower);
            }

            burdeningStepNumber++;
            AddResultsProtocolRow(stepValue * burdeningStepNumber);
        }

        private void AddResultsProtocolRow(double burdeningStepValue)
        {
            var rowValues = new List<object>
            {
                burdeningStepNumber.ToString(CultureInfo.InvariantCulture),
                FormatLoadValue(burdeningStepValue)
            };

            foreach (var state in selectedNodeStates)
            {
                rowValues.Add(FormatLoadValue(state.Node.Data.NominalActivePower));
                rowValues.Add(FormatLoadValue(state.Node.Data.NominalReactivePower));
            }

            resultsGrid.Rows.Add(rowValues.ToArray());
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

        private sealed class GroupBurdeningNodeState
        {
            public GroupBurdeningNodeState(DataGridViewRow row, GraphicNode node, bool useTg)
            {
                Row = row;
                Node = node;
                UseTg = useTg;
                InitialP = node.Data.NominalActivePower;
                InitialQ = node.Data.NominalReactivePower;
            }

            public DataGridViewRow Row { get; }
            public GraphicNode Node { get; }
            public bool UseTg { get; }
            public double InitialP { get; }
            public double InitialQ { get; }
        }
    }
}
