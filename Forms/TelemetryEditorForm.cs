using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Windows.Forms;

namespace PowerGridEditor
{
    public class TelemetryEditorForm : Form
    {
        private sealed class GridRowTag
        {
            public string GroupKey { get; set; }
            public string Key { get; set; }
            public dynamic Data { get; set; }
            public bool IsParent { get; set; }
            public string ParentText { get; set; }
            public string ParamText { get; set; }
        }

        private readonly Func<IEnumerable<object>> elementsProvider;
        private readonly Func<IEnumerable<GraphicBranch>> branchesProvider;
        private readonly Func<IEnumerable<GraphicShunt>> shuntsProvider;
        private readonly Action invalidateCanvas;

        private readonly DataGridView grid;
        private readonly TextBox textBoxBulkIp;
        private readonly TextBox textBoxBulkPort;
        private readonly TextBox textBoxBulkDeviceId;
        private readonly ComboBox comboBoxBulkProtocol;
        private readonly CheckBox checkBoxBulkTelemetry;
        private readonly NumericUpDown numericGlobalInterval;
        private readonly Timer refreshTimer;
        private readonly TextBox textBoxSearch;
        private readonly Panel topBar;
        private readonly Label topHint;
        private readonly Button buttonRefresh;
        private readonly Button buttonApplyBulk;

        private readonly HashSet<string> expandedGroups = new HashSet<string>();
        private bool suppressGridEvents;

        private static readonly Color ThemePageBackground = Color.FromArgb(245, 250, 255);
        private static readonly Color ThemePanelBackground = Color.White;
        private static readonly Color ThemeAccentBlue = Color.FromArgb(187, 247, 208);
        private static readonly Color ThemeAccentBlueHover = Color.FromArgb(134, 239, 172);
        private static readonly Color ThemeAccentBluePressed = Color.FromArgb(74, 222, 128);
        private static readonly Color ThemeBorderBlue = Color.FromArgb(34, 197, 94);
        private static readonly Color ThemeTextBlack = Color.Black;

        public TelemetryEditorForm(
            Func<IEnumerable<object>> elementsProvider,
            Func<IEnumerable<GraphicBranch>> branchesProvider,
            Func<IEnumerable<GraphicShunt>> shuntsProvider,
            Action invalidateCanvas)
        {
            this.elementsProvider = elementsProvider;
            this.branchesProvider = branchesProvider;
            this.shuntsProvider = shuntsProvider;
            this.invalidateCanvas = invalidateCanvas;

            Text = "Телеметрия";
            Width = 1500;
            Height = 800;
            BackColor = ThemePageBackground;
            ForeColor = ThemeTextBlack;
            Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            topHint = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(10, 7, 10, 0),
                Text = "Единая таблица: узлы → базисный узел → ветви → шунты.",
                ForeColor = ThemeTextBlack
            };

            topBar = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = ThemePanelBackground, Padding = new Padding(8) };
            buttonRefresh = new Button { Left = 8, Top = 8, Width = 130, Height = 32, Text = "Обновить", BackColor = ThemeAccentBlue, ForeColor = ThemeTextBlack, FlatStyle = FlatStyle.Flat };
            buttonRefresh.Click += (s, e) => RefreshGridFromModels(true);

            textBoxSearch = new TextBox { Left = 145, Top = 10, Width = 220 };
            textBoxSearch.TextChanged += (s, e) => RefreshGridFromModels(false);

            textBoxBulkIp = new TextBox { Left = 370, Top = 10, Width = 130, Text = "127.0.0.1" };
            textBoxBulkPort = new TextBox { Left = 506, Top = 10, Width = 60, Text = "502" };
            textBoxBulkDeviceId = new TextBox { Left = 572, Top = 10, Width = 70, Text = "1" };
            comboBoxBulkProtocol = new ComboBox { Left = 648, Top = 10, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            comboBoxBulkProtocol.Items.AddRange(new object[] { "Modbus TCP", "МЭК-104" });
            comboBoxBulkProtocol.SelectedIndex = 0;
            checkBoxBulkTelemetry = new CheckBox { Left = 774, Top = 12, Width = 145, Text = "Телеметрия для всех" };
            numericGlobalInterval = new NumericUpDown { Left = 1138, Top = 10, Width = 85, Minimum = 1, Maximum = 3600, Value = AppRuntimeSettings.UpdateIntervalSeconds };
            numericGlobalInterval.ValueChanged += (s, e) => AppRuntimeSettings.UpdateIntervalSeconds = (int)numericGlobalInterval.Value;

            buttonApplyBulk = new Button { Left = 925, Top = 8, Width = 205, Height = 32, Text = "Применить ко всем", BackColor = ThemeAccentBlue, ForeColor = ThemeTextBlack, FlatStyle = FlatStyle.Flat };
            buttonApplyBulk.Click += ButtonApplyBulk_Click;

            topBar.Controls.Add(buttonRefresh);
            topBar.Controls.Add(new Label { Left = 145, Top = 42, Width = 220, Text = "Поиск (узел/ветвь/параметр)" });
            topBar.Controls.Add(textBoxSearch);
            topBar.Controls.Add(textBoxBulkIp);
            topBar.Controls.Add(textBoxBulkPort);
            topBar.Controls.Add(textBoxBulkDeviceId);
            topBar.Controls.Add(comboBoxBulkProtocol);
            topBar.Controls.Add(checkBoxBulkTelemetry);
            topBar.Controls.Add(numericGlobalInterval);
            topBar.Controls.Add(buttonApplyBulk);
            topBar.Controls.Add(new Label { Left = 370, Top = 42, Width = 130, Text = "IP" });
            topBar.Controls.Add(new Label { Left = 506, Top = 42, Width = 60, Text = "Порт" });
            topBar.Controls.Add(new Label { Left = 572, Top = 42, Width = 70, Text = "ID" });
            topBar.Controls.Add(new Label { Left = 648, Top = 42, Width = 120, Text = "Протокол" });
            topBar.Controls.Add(new Label { Left = 1138, Top = 42, Width = 150, Text = "Общий интервал (сек)" });

            foreach (Control control in topBar.Controls)
            {
                if (control is Label label)
                {
                    label.ForeColor = ThemeTextBlack;
                }
            }

            grid = BuildGrid();

            Controls.Add(grid);
            Controls.Add(topBar);
            Controls.Add(topHint);

            refreshTimer = new Timer();
            refreshTimer.Interval = AppRuntimeSettings.UpdateIntervalSeconds * 1000;
            refreshTimer.Tick += (s, e) =>
            {
                if (grid.IsCurrentCellInEditMode) return;

                if (!TryUpdateGridRowsInPlace())
                {
                    RefreshGridFromModels(false);
                }
            };

            AppRuntimeSettings.UpdateIntervalChanged += seconds =>
            {
                if (IsDisposed) return;
                BeginInvoke(new Action(() => refreshTimer.Interval = Math.Max(1, seconds) * 1000));
            };

            Load += (s, e) =>
            {
                ApplyTheme();
                RefreshGridFromModels(true);
                refreshTimer.Start();
            };
            FormClosed += (s, e) =>
            {
                refreshTimer.Stop();
            };
        }

        public void ApplyTheme()
        {
            BackColor = ThemePageBackground;
            ForeColor = ThemeTextBlack;
            topBar.BackColor = ThemePanelBackground;
            topHint.ForeColor = ThemeTextBlack;

            foreach (Control control in topBar.Controls)
            {
                if (control is Label label)
                {
                    label.ForeColor = ThemeTextBlack;
                }
                else if (control is Button btn)
                {
                    btn.BackColor = ThemeAccentBlue;
                    btn.ForeColor = ThemeTextBlack;
                    btn.FlatAppearance.BorderColor = ThemeBorderBlue;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.FlatAppearance.MouseOverBackColor = ThemeAccentBlueHover;
                    btn.FlatAppearance.MouseDownBackColor = ThemeAccentBluePressed;
                }
                else if (control is TextBox textBox)
                {
                    textBox.BackColor = Color.White;
                    textBox.ForeColor = ThemeTextBlack;
                }
                else if (control is ComboBox combo)
                {
                    combo.BackColor = Color.White;
                    combo.ForeColor = ThemeTextBlack;
                }
                else if (control is NumericUpDown numeric)
                {
                    numeric.BackColor = Color.White;
                    numeric.ForeColor = ThemeTextBlack;
                }
                else if (control is CheckBox check)
                {
                    check.ForeColor = ThemeTextBlack;
                }
            }

            grid.BackgroundColor = ThemePageBackground;
            grid.ColumnHeadersDefaultCellStyle.BackColor = ThemePanelBackground;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeTextBlack;
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = ThemeTextBlack;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(187, 247, 208);
            grid.DefaultCellStyle.SelectionForeColor = ThemeTextBlack;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(239, 246, 255);
            grid.GridColor = Color.FromArgb(187, 247, 208);

            ApplyBoldFontsRecursive(this);
            RefreshGridFromModels(false);
        }

        private void ApplyBoldFontsRecursive(Control root)
        {
            if (root == null) return;
            root.Font = new Font(root.Font, FontStyle.Bold);
            foreach (Control child in root.Controls) ApplyBoldFontsRecursive(child);
        }

        private DataGridView BuildGrid()
        {
            var table = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = ThemePageBackground,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
            };

            table.ColumnHeadersDefaultCellStyle.BackColor = ThemePanelBackground;
            table.ColumnHeadersDefaultCellStyle.ForeColor = ThemeTextBlack;
            table.DefaultCellStyle.BackColor = Color.White;
            table.DefaultCellStyle.ForeColor = ThemeTextBlack;
            table.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            table.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            table.DefaultCellStyle.SelectionBackColor = Color.FromArgb(187, 247, 208);
            table.DefaultCellStyle.SelectionForeColor = ThemeTextBlack;
            table.GridColor = Color.FromArgb(187, 247, 208);
            table.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(239, 246, 255);
            table.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            table.EnableHeadersVisualStyles = false;

            table.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Тип", ReadOnly = true, Width = 120 });
            table.Columns.Add(new DataGridViewTextBoxColumn { Name = "Element", HeaderText = "Элемент", ReadOnly = true, Width = 110 });
            table.Columns.Add(new DataGridViewTextBoxColumn { Name = "Param", HeaderText = "Параметр", ReadOnly = true, Width = 140 });
            table.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Значение", Width = 90 });
            table.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Telemetry", HeaderText = "Телеметрия", Width = 80 });
            table.Columns.Add(new DataGridViewTextBoxColumn { Name = "Register", HeaderText = "Адрес", Width = 80 });
            table.Columns.Add(new DataGridViewComboBoxColumn { Name = "Protocol", HeaderText = "Протокол", Width = 95, DataSource = new[] { "Modbus TCP", "МЭК-104" } });
            table.Columns.Add(new DataGridViewTextBoxColumn { Name = "IP", HeaderText = "IP адрес", Width = 110 });
            table.Columns.Add(new DataGridViewTextBoxColumn { Name = "Port", HeaderText = "Порт", Width = 65 });
            table.Columns.Add(new DataGridViewTextBoxColumn { Name = "DeviceID", HeaderText = "ID устройства", Width = 95 });
            table.Columns.Add(new DataGridViewTextBoxColumn { Name = "UpdateInterval", HeaderText = "Интервал, c", Width = 85 });
            table.Columns.Add(new DataGridViewButtonColumn { Name = "Ping", HeaderText = "Пинг", Text = "Пинг", UseColumnTextForButtonValue = true, Width = 70 });
            table.Columns.Add(new DataGridViewTextBoxColumn { Name = "IncStep", HeaderText = "Шаг", Width = 70 });
            table.Columns.Add(new DataGridViewTextBoxColumn { Name = "IncInterval", HeaderText = "Инт.,с", Width = 70 });
            table.Columns.Add(new DataGridViewButtonColumn { Name = "AutoChange", HeaderText = "Авто изм.", Text = "Старт", UseColumnTextForButtonValue = false, Width = 90 });

            foreach (DataGridViewColumn col in table.Columns)
            {
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            table.CellEndEdit += Grid_CellChanged;
            table.CellValueChanged += Grid_CellChanged;
            table.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (table.IsCurrentCellDirty)
                {
                    table.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            table.CellContentClick += Grid_CellContentClick;
            table.CellClick += Grid_CellClick;

            EnableDoubleBuffering(table);
            return table;
        }

        private void EnableDoubleBuffering(DataGridView table)
        {
            typeof(DataGridView)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?
                .SetValue(table, true, null);
        }

        private bool TryUpdateGridRowsInPlace()
        {
            if (grid.Rows.Count == 0) return false;

            grid.SuspendLayout();
            suppressGridEvents = true;
            try
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    var tag = row.Tag as GridRowTag;
                    if (tag == null || tag.Data == null)
                    {
                        continue;
                    }

                    dynamic data = tag.Data;
                    row.Cells["Protocol"].Value = data.Protocol ?? "Modbus TCP";
                    row.Cells["IP"].Value = data.IPAddress ?? "127.0.0.1";
                    row.Cells["Port"].Value = data.Port ?? "502";
                    row.Cells["DeviceID"].Value = data is Node ? (data.NodeID ?? "1") : (data.DeviceID ?? "1");
                    row.Cells["UpdateInterval"].Value = data.MeasurementIntervalSeconds;

                    if (!tag.IsParent)
                    {
                        bool isNodeVoltageStaticRow = (data is Node || data is BaseNode) && (tag.Key == "U" || tag.Key == "Ucalc");
                        row.Cells["Value"].Value = GetParamValue(data, tag.Key);
                        row.Cells["Telemetry"].Value = isNodeVoltageStaticRow ? false : (data.ParamAutoModes.ContainsKey(tag.Key) && data.ParamAutoModes[tag.Key]);
                        row.Cells["Register"].Value = isNodeVoltageStaticRow ? string.Empty : (data.ParamRegisters.ContainsKey(tag.Key) ? data.ParamRegisters[tag.Key] : string.Empty);

                        if (isNodeVoltageStaticRow)
                        {
                            row.Cells["Telemetry"].ReadOnly = true;
                            row.Cells["Register"].ReadOnly = true;
                            row.Cells["Telemetry"].Style.BackColor = Color.Gainsboro;
                            row.Cells["Register"].Style.BackColor = Color.Gainsboro;
                        }
                        row.Cells["IncStep"].Value = data.ParamIncrementSteps.ContainsKey(tag.Key) ? data.ParamIncrementSteps[tag.Key] : 1;
                        row.Cells["IncInterval"].Value = data.ParamIncrementIntervals.ContainsKey(tag.Key) ? data.ParamIncrementIntervals[tag.Key] : 2;
                        UpdateIncrementButtonState(row, data, tag.Key);
                    }
                }

                grid.Invalidate();
                return true;
            }
            finally
            {
                suppressGridEvents = false;
                grid.ResumeLayout(false);
            }
        }

        private void RefreshGridFromModels(bool resetScroll)
        {
            if (grid.IsDisposed) return;

            string topKey = null;
            string topParam = null;
            string currentKey = null;
            string currentParam = null;
            int currentColIndex = 0;
            int firstRow = 0;
            bool currentWasVisible = false;

            if (!resetScroll && grid.Rows.Count > 0)
            {
                firstRow = grid.FirstDisplayedScrollingRowIndex >= 0 ? grid.FirstDisplayedScrollingRowIndex : 0;

                if (firstRow < grid.Rows.Count && grid.Rows[firstRow].Tag is GridRowTag topTag)
                {
                    topKey = topTag.GroupKey;
                    topParam = topTag.Key;
                }

                if (grid.CurrentCell != null &&
                    grid.CurrentCell.RowIndex >= 0 &&
                    grid.CurrentCell.RowIndex < grid.Rows.Count &&
                    grid.Rows[grid.CurrentCell.RowIndex].Tag is GridRowTag currentTag)
                {
                    currentKey = currentTag.GroupKey;
                    currentParam = currentTag.Key;
                    currentColIndex = grid.CurrentCell.ColumnIndex;

                    int visibleRows = Math.Max(1, grid.DisplayedRowCount(false));
                    currentWasVisible = grid.CurrentCell.RowIndex >= firstRow && grid.CurrentCell.RowIndex < (firstRow + visibleRows);
                }
            }

            string filter = (textBoxSearch.Text ?? string.Empty).Trim().ToLowerInvariant();
            bool hasFilter = !string.IsNullOrWhiteSpace(filter);

            var elementsSnapshot = elementsProvider().ToList();
            var branchesSnapshot = branchesProvider().ToList();
            var shuntsSnapshot = shuntsProvider().ToList();

            grid.SuspendLayout();
            suppressGridEvents = true;
            try
            {
                grid.Rows.Clear();

                AddSectionRow("Узлы");
                foreach (var node in elementsSnapshot.OfType<GraphicNode>().OrderBy(n => n.Data.Number))
                    AddParentWithChildren("Узел", $"N{node.Data.Number}", node.Data, new[]
                    {
                        ("U", "Номинальное напряжение, кВ", node.Data.InitialVoltage), ("Ufact", "Фактическое напряжение, кВ", node.Data.ActualVoltage), ("Ucalc", "Расчётное напряжение, кВ", node.Data.CalculatedVoltage), ("P", "P нагрузка, МВт", node.Data.NominalActivePower),
                        ("Q", "Q нагрузка, Мвар", node.Data.NominalReactivePower), ("Pg", "P генерация, МВт", node.Data.ActivePowerGeneration),
                        ("Qg", "Q генерация, Мвар", node.Data.ReactivePowerGeneration), ("Uf", "U фикс., кВ", node.Data.FixedVoltageModule),
                        ("Qmin", "Q мин, Мвар", node.Data.MinReactivePower), ("Qmax", "Q макс, Мвар", node.Data.MaxReactivePower)
                    }, filter, hasFilter);

                AddSectionRow("Базисный узел");
                foreach (var baseNode in elementsSnapshot.OfType<GraphicBaseNode>().OrderBy(n => n.Data.Number))
                    AddParentWithChildren("Базисный узел", $"B{baseNode.Data.Number}", baseNode.Data, new[]
                    {
                        ("U", "Номинальное напряжение, кВ", baseNode.Data.InitialVoltage), ("Ufact", "Фактическое напряжение, кВ", baseNode.Data.ActualVoltage), ("Ucalc", "Расчётное напряжение, кВ", baseNode.Data.CalculatedVoltage), ("P", "P нагрузка, МВт", baseNode.Data.NominalActivePower),
                        ("Q", "Q нагрузка, Мвар", baseNode.Data.NominalReactivePower), ("Pg", "P генерация, МВт", baseNode.Data.ActivePowerGeneration),
                        ("Qg", "Q генерация, Мвар", baseNode.Data.ReactivePowerGeneration), ("Uf", "U фикс., кВ", baseNode.Data.FixedVoltageModule),
                        ("Qmin", "Q мин, Мвар", baseNode.Data.MinReactivePower), ("Qmax", "Q макс, Мвар", baseNode.Data.MaxReactivePower)
                    }, filter, hasFilter);

                AddSectionRow("Ветви");
                foreach (var branch in branchesSnapshot.OrderBy(b => b.Data.StartNodeNumber).ThenBy(b => b.Data.EndNodeNumber))
                    AddParentWithChildren("Ветвь", $"{branch.Data.StartNodeNumber}-{branch.Data.EndNodeNumber}", branch.Data, new[]
                    {
                        ("R", "R, Ом", branch.Data.ActiveResistance), ("X", "X, Ом", branch.Data.ReactiveResistance),
                        ("B", "B, См", branch.Data.ReactiveConductivity), ("Ktr", "K трансф., о.е.", branch.Data.TransformationRatio),
                        ("G", "G, См", branch.Data.ActiveConductivity), ("Imax", "Iдоп, А", branch.Data.PermissibleCurrent)
                    }, filter, hasFilter);

                AddSectionRow("Шунты");
                foreach (var shunt in shuntsSnapshot.OrderBy(s => s.Data.StartNodeNumber))
                    AddParentWithChildren("Шунт", $"Sh{shunt.Data.StartNodeNumber}", shunt.Data, new[]
                    {
                        ("R", "R, Ом", shunt.Data.ActiveResistance), ("X", "X, Ом", shunt.Data.ReactiveResistance)
                    }, filter, hasFilter);
            }
            finally
            {
                suppressGridEvents = false;
                grid.ResumeLayout(false);
            }

            if (!resetScroll && grid.Rows.Count > 0)
            {
                int topTarget = Math.Max(0, Math.Min(firstRow, grid.Rows.Count - 1));

                if (!string.IsNullOrEmpty(topKey))
                {
                    for (int i = 0; i < grid.Rows.Count; i++)
                    {
                        if (grid.Rows[i].Tag is GridRowTag tag && tag.GroupKey == topKey && (topParam == null || tag.Key == topParam))
                        {
                            topTarget = i;
                            break;
                        }
                    }
                }

                try
                {
                    grid.FirstDisplayedScrollingRowIndex = topTarget;
                }
                catch { }

                if (currentWasVisible && !string.IsNullOrEmpty(currentKey))
                {
                    for (int i = 0; i < grid.Rows.Count; i++)
                    {
                        if (grid.Rows[i].Tag is GridRowTag tag && tag.GroupKey == currentKey && (currentParam == null || tag.Key == currentParam))
                        {
                            int col = Math.Max(0, Math.Min(currentColIndex, grid.Columns.Count - 1));
                            try
                            {
                                grid.CurrentCell = grid.Rows[i].Cells[col];
                            }
                            catch { }
                            break;
                        }
                    }
                }
            }
        }

        private void UpdateIncrementButtonState(DataGridViewRow row, dynamic data, string key)
        {
            string id = ParameterAutoChangeService.BuildId(data, key);
            bool running = ParameterAutoChangeService.TryGet(id, out _, out _, out bool isRunning) && isRunning;

            var cell = row.Cells["AutoChange"];
            if (running)
            {
                cell.Style.BackColor = Color.FromArgb(252, 165, 165);
                cell.Style.ForeColor = Color.Black;
                cell.Style.SelectionBackColor = Color.FromArgb(187, 247, 208);
                cell.Style.SelectionForeColor = ThemeTextBlack;
                cell.Value = "Стоп";
            }
            else
            {
                cell.Style.BackColor = Color.FromArgb(191, 219, 254);
                cell.Style.ForeColor = Color.Black;
                cell.Style.SelectionBackColor = Color.FromArgb(147, 197, 253);
                cell.Style.SelectionForeColor = Color.Black;
                cell.Value = "Старт";
            }
        }

        private void AddParentWithChildren(string type, string elementName, dynamic data, IEnumerable<(string Key, string Label, double Value)> rows, string filter, bool hasFilter)
        {
            string groupKey = type + ":" + elementName;
            var rowList = rows.ToList();
            bool matchesParent = groupKey.ToLowerInvariant().Contains(filter);
            var filteredChildren = hasFilter
                ? rowList.Where(r => r.Label.ToLowerInvariant().Contains(filter) || matchesParent).ToList()
                : rowList;

            if (hasFilter && !matchesParent && filteredChildren.Count == 0) return;

            int parentIndex = grid.Rows.Add(type, (expandedGroups.Contains(groupKey) ? "▼ " : "▶ ") + elementName, "", "", false, "", data.Protocol, data.IPAddress, data.Port, data is Node ? data.NodeID : data.DeviceID, data.MeasurementIntervalSeconds, "", "", "", "");
            var parentRow = grid.Rows[parentIndex];
            parentRow.ReadOnly = true;
            parentRow.DefaultCellStyle.BackColor = Color.FromArgb(187, 247, 208);
            parentRow.DefaultCellStyle.ForeColor = ThemeTextBlack;
            parentRow.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            parentRow.Tag = new GridRowTag { GroupKey = groupKey, Data = data, IsParent = true, ParentText = elementName };

            bool showChildren = hasFilter || expandedGroups.Contains(groupKey);
            if (!showChildren) return;

            foreach (var row in filteredChildren)
            {
                bool telemetryEnabled = data.ParamAutoModes.ContainsKey(row.Key) && data.ParamAutoModes[row.Key];
                string registerValue = data.ParamRegisters.ContainsKey(row.Key) ? data.ParamRegisters[row.Key] : string.Empty;
                double incStep = data.ParamIncrementSteps.ContainsKey(row.Key) ? data.ParamIncrementSteps[row.Key] : 1;
                int incInterval = data.ParamIncrementIntervals.ContainsKey(row.Key) ? data.ParamIncrementIntervals[row.Key] : 2;
                bool isNodeVoltageStaticRow = (type == "Узел" || type == "Базисный узел") && (row.Key == "U" || row.Key == "Ucalc");
                if (isNodeVoltageStaticRow)
                {
                    telemetryEnabled = false;
                    registerValue = string.Empty;
                }

                int index = grid.Rows.Add(type, "   " + elementName, row.Label, row.Value, telemetryEnabled, registerValue, data.Protocol, data.IPAddress, data.Port, data is Node ? data.NodeID : data.DeviceID, data.MeasurementIntervalSeconds, "Пинг", incStep, incInterval, "Старт");
                var childRow = grid.Rows[index];
                childRow.Tag = new GridRowTag { GroupKey = groupKey, Key = row.Key, Data = data, IsParent = false, ParentText = elementName, ParamText = row.Label };

                if (isNodeVoltageStaticRow)
                {
                    childRow.Cells["Telemetry"].ReadOnly = true;
                    childRow.Cells["Register"].ReadOnly = true;
                    childRow.Cells["Telemetry"].Style.BackColor = Color.Gainsboro;
                    childRow.Cells["Register"].Style.BackColor = Color.Gainsboro;
                }

                if ((type == "Узел" || type == "Базисный узел") && row.Key == "Ucalc")
                {
                    childRow.Cells["Value"].ReadOnly = true;
                    childRow.Cells["Value"].Style.BackColor = Color.Gainsboro;
                }

                UpdateIncrementButtonState(childRow, data, row.Key);
            }
        }

        private void AddSectionRow(string title)
        {
            int index = grid.Rows.Add(title, "", "", "", false, "", "", "", "", "", "", "", "", "", "");
            var row = grid.Rows[index];
            row.ReadOnly = true;
            row.DefaultCellStyle.BackColor = Color.FromArgb(187, 247, 208);
            row.DefaultCellStyle.ForeColor = ThemeTextBlack;
            row.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            row.Tag = null;
        }

        private void Grid_CellChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (suppressGridEvents) return;
            if (e.RowIndex < 0) return;
            var row = grid.Rows[e.RowIndex];
            if (!(row.Tag is GridRowTag tag) || tag.IsParent) return;

            dynamic data = tag.Data;
            string key = tag.Key;

            if (double.TryParse(Convert.ToString(row.Cells["IncStep"].Value), out double step))
            {
                data.ParamIncrementSteps[key] = step;
            }
            if (int.TryParse(Convert.ToString(row.Cells["IncInterval"].Value), out int incInterval))
            {
                data.ParamIncrementIntervals[key] = Math.Max(1, incInterval);
            }

            bool isNodeVoltageStaticRow = (data is Node || data is BaseNode) && (key == "U" || key == "Ucalc");
            if (key != "Ucalc" && double.TryParse(Convert.ToString(row.Cells["Value"].Value), out double value2))
            {
                ApplyParamValue(data, key, value2);
            }

            if (!isNodeVoltageStaticRow)
            {
                data.ParamAutoModes[key] = Convert.ToBoolean(row.Cells["Telemetry"].Value);
                data.ParamRegisters[key] = Convert.ToString(row.Cells["Register"].Value) ?? "0";
            }
            if (int.TryParse(Convert.ToString(row.Cells["UpdateInterval"].Value), out int updateInterval))
            {
                data.MeasurementIntervalSeconds = Math.Max(1, updateInterval);
            }
            data.Protocol = Convert.ToString(row.Cells["Protocol"].Value) ?? "Modbus TCP";
            data.IPAddress = Convert.ToString(row.Cells["IP"].Value) ?? "127.0.0.1";
            data.Port = Convert.ToString(row.Cells["Port"].Value) ?? "502";
            if (data is Node) data.NodeID = Convert.ToString(row.Cells["DeviceID"].Value) ?? "1";
            else data.DeviceID = Convert.ToString(row.Cells["DeviceID"].Value) ?? "1";

            invalidateCanvas();
        }

        private async void Grid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var row = grid.Rows[e.RowIndex];
            if (!(row.Tag is GridRowTag tag)) return;
            if (tag.IsParent) return;

            if (grid.Columns[e.ColumnIndex].Name == "AutoChange")
            {
                ToggleIncrement(tag.Data, tag.Key, row);
                return;
            }

            if (grid.Columns[e.ColumnIndex].Name != "Ping") return;
            string ip = Convert.ToString(row.Cells["IP"].Value);
            if (string.IsNullOrWhiteSpace(ip))
            {
                MessageBox.Show("Введите IP адрес для проверки.");
                return;
            }

            bool ok = await PingHostAsync(ip);
            row.Cells["IP"].Style.BackColor = ok ? Color.LightGreen : Color.LightPink;
            row.Cells["Ping"].Value = ok ? "ОК" : "Нет";
        }

        private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (!(grid.Rows[e.RowIndex].Tag is GridRowTag tag)) return;
            if (!tag.IsParent) return;
            if (grid.Columns[e.ColumnIndex].Name == "Ping" || grid.Columns[e.ColumnIndex].Name == "AutoChange") return;

            if (expandedGroups.Contains(tag.GroupKey)) expandedGroups.Remove(tag.GroupKey);
            else expandedGroups.Add(tag.GroupKey);
            RefreshGridFromModels(false);
        }

        private void ToggleIncrement(dynamic data, string key, DataGridViewRow row)
        {
            double step = 1;
            int interval = 2;
            double.TryParse(Convert.ToString(row.Cells["IncStep"].Value), out step);
            int.TryParse(Convert.ToString(row.Cells["IncInterval"].Value), out interval);
            interval = Math.Max(1, interval);

            data.ParamIncrementSteps[key] = step;
            data.ParamIncrementIntervals[key] = interval;

            string id = ParameterAutoChangeService.BuildId(data, key);
            bool running = ParameterAutoChangeService.TryGet(id, out _, out _, out bool isRunning) && isRunning;
            bool enable = !running;

            ParameterAutoChangeService.Configure(
                id,
                step,
                interval,
                enable,
                () => GetParamValue(data, key),
                value => ApplyParamValue(data, key, value),
                () => BeginInvoke(new Action(() =>
                {
                    row.Cells["Value"].Value = GetParamValue(data, key);
                    UpdateIncrementButtonState(row, data, key);
                    invalidateCanvas();
                })));

            UpdateIncrementButtonState(row, data, key);
        }

        private void ButtonApplyBulk_Click(object sender, EventArgs e)
        {
            if (!IPAddress.TryParse(textBoxBulkIp.Text, out _))
            {
                MessageBox.Show("Введите корректный IP для массового применения.", "Телеметрия", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var elementsSnapshot = elementsProvider().ToList();
            var branchesSnapshot = branchesProvider().ToList();
            var shuntsSnapshot = shuntsProvider().ToList();

            var allData = elementsSnapshot.OfType<GraphicNode>().Select(x => (dynamic)x.Data)
                .Concat(elementsSnapshot.OfType<GraphicBaseNode>().Select(x => (dynamic)x.Data))
                .Concat(branchesSnapshot.Select(x => (dynamic)x.Data))
                .Concat(shuntsSnapshot.Select(x => (dynamic)x.Data)).ToList();

            foreach (var data in allData)
            {
                data.IPAddress = textBoxBulkIp.Text;
                data.Port = textBoxBulkPort.Text;
                data.Protocol = Convert.ToString(comboBoxBulkProtocol.SelectedItem) ?? "Modbus TCP";
                data.MeasurementIntervalSeconds = AppRuntimeSettings.UpdateIntervalSeconds;
                if (data is Node) data.NodeID = textBoxBulkDeviceId.Text;
                else data.DeviceID = textBoxBulkDeviceId.Text;

                foreach (var key in new List<string>(data.ParamAutoModes.Keys))
                {
                    data.ParamAutoModes[key] = checkBoxBulkTelemetry.Checked;
                }
            }

            RefreshGridFromModels(false);
            invalidateCanvas();
            MessageBox.Show("Параметры телеметрии применены ко всем элементам.", "Телеметрия", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private double GetParamValue(dynamic data, string key)
        {
            if (key == "U") return data.InitialVoltage;
            if (key == "Ufact") return data.ActualVoltage;
            if (key == "Ucalc") return data.CalculatedVoltage;
            if (key == "P") return data.NominalActivePower;
            if (key == "Q") return data.NominalReactivePower;
            if (key == "Pg") return data.ActivePowerGeneration;
            if (key == "Qg") return data.ReactivePowerGeneration;
            if (key == "Uf") return data.FixedVoltageModule;
            if (key == "Qmin") return data.MinReactivePower;
            if (key == "Qmax") return data.MaxReactivePower;
            if (key == "R") return data.ActiveResistance;
            if (key == "X") return data.ReactiveResistance;
            if (key == "B") return data.ReactiveConductivity;
            if (key == "Ktr") return data.TransformationRatio;
            if (key == "G") return data.ActiveConductivity;
            if (key == "Imax") return data.PermissibleCurrent;
            return 0;
        }

        private void ApplyParamValue(dynamic data, string key, double value)
        {
            if (key == "U") data.InitialVoltage = value;
            else if (key == "Ufact") data.ActualVoltage = value;
            else if (key == "Ucalc") data.CalculatedVoltage = value;
            else if (key == "P") data.NominalActivePower = value;
            else if (key == "Q") data.NominalReactivePower = value;
            else if (key == "Pg") data.ActivePowerGeneration = value;
            else if (key == "Qg") data.ReactivePowerGeneration = value;
            else if (key == "Uf") data.FixedVoltageModule = value;
            else if (key == "Qmin") data.MinReactivePower = value;
            else if (key == "Qmax") data.MaxReactivePower = value;
            else if (key == "R") data.ActiveResistance = value;
            else if (key == "X") data.ReactiveResistance = value;
            else if (key == "B") data.ReactiveConductivity = value;
            else if (key == "Ktr") data.TransformationRatio = value;
            else if (key == "G") data.ActiveConductivity = value;
            else if (key == "Imax") data.PermissibleCurrent = value;
        }

        private async System.Threading.Tasks.Task<bool> PingHostAsync(string ip)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ip, 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
