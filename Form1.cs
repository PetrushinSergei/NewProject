using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using NModbus;


namespace PowerGridEditor
{
    public partial class Form1 : Form
    {
        private List<object> graphicElements = new List<object>();
        private List<GraphicBranch> graphicBranches = new List<GraphicBranch>();
        private List<GraphicShunt> graphicShunts = new List<GraphicShunt>();
        private object selectedElement = null;
        private Point lastMousePosition;
        private bool isDragging = false;
        private ContextMenuStrip contextMenuStrip;
        private readonly Dictionary<Type, List<Form>> openedEditorWindows = new Dictionary<Type, List<Form>>();
        private System.Windows.Forms.Timer uiClockTimer;
        private Button buttonCalcSettings;
        private System.Windows.Forms.Timer telemetryTimer;
        private bool telemetryPollingInProgress = false;
        private readonly Dictionary<object, DateTime> lastTelemetryReadAt = new Dictionary<object, DateTime>();
        private readonly HashSet<object> selectedElements = new HashSet<object>();
        private bool isMarqueeSelecting = false;
        private Point marqueeStartModel;
        private Point marqueeCurrentModel;
        private readonly Dictionary<object, int> elementGroups = new Dictionary<object, int>();
        private int nextGroupId = 1;
        private DataGridView elementsGrid;
        private Button buttonOpenTelemetryForm;
        private Button buttonOpenClientSettingsForm;
        private TelemetryEditorForm telemetryEditorForm;
        private ClientSettingsForm clientSettingsForm;
        private Point rightMouseDownPoint;
        private bool rightMouseMoved;
        private bool hasConvergenceStatus;
        private bool isLastModeConverged;
        private readonly ToolTip hoverToolTip = new ToolTip();
        private object hoveredElement;
        private readonly Dictionary<int, double> lastCalculatedNodeVoltages = new Dictionary<int, double>();
        private CheckBox checkBoxShowBranchInfo;
        private bool showBranchCurrentOverlay = true;
        private string lastTooltipText;
        private System.Windows.Forms.Timer calculationTimer;
        private bool isCalculationRunning;
        private bool calculationInProgress;
        private DateTime lastCalcDoubleClickAt = DateTime.MinValue;
        private CheckBox checkBoxLockLegends;
        private CheckBox checkBoxComprehensiveControl;
        private Button buttonPrevMode;
        private Button buttonNextMode;
        private bool comprehensiveControlEnabled = true;
        private string lastStopReason = "нет";
        private readonly List<string> burdeningIterationLog = new List<string>();
        private dynamic burdeningTrackedData;
        private string burdeningTrackedKey;
        private bool legendsLocked = true;
        private Rectangle branchLegendBounds = Rectangle.Empty;
        private Rectangle voltageLegendBounds = Rectangle.Empty;
        private Point branchLegendOffset = Point.Empty;
        private Point voltageLegendOffset = Point.Empty;
        private bool isDraggingLegend;
        private string draggingLegendKey;
        private Point lastLegendMousePoint;
        private bool hasLastConvergedSnapshot;
        private readonly Dictionary<(int Start, int End), (double Active, double Reactive, double Current, double Loading, Color Color)> lastConvergedBranchState = new Dictionary<(int Start, int End), (double Active, double Reactive, double Current, double Loading, Color Color)>();
        private readonly Dictionary<int, (double CalculatedVoltage, Color Color)> lastConvergedNodeState = new Dictionary<int, (double CalculatedVoltage, Color Color)>();
        private readonly Dictionary<int, (double CalculatedVoltage, Color Color)> lastConvergedBaseNodeState = new Dictionary<int, (double CalculatedVoltage, Color Color)>();
        private readonly Dictionary<int, double> lastConvergedCalculatedNodeVoltages = new Dictionary<int, double>();
        private readonly Dictionary<(int Start, int End), (double Active, double Reactive, double Current, double Loading, Color Color)> lastDivergedBranchState = new Dictionary<(int Start, int End), (double Active, double Reactive, double Current, double Loading, Color Color)>();
        private readonly Dictionary<int, (double CalculatedVoltage, Color Color)> lastDivergedNodeState = new Dictionary<int, (double CalculatedVoltage, Color Color)>();
        private readonly Dictionary<int, (double CalculatedVoltage, Color Color)> lastDivergedBaseNodeState = new Dictionary<int, (double CalculatedVoltage, Color Color)>();
        private readonly Dictionary<int, double> lastDivergedCalculatedNodeVoltages = new Dictionary<int, double>();
        private bool hasLastDivergedSnapshot;
        private bool showingDivergedSnapshot;
        // Временные переменные для обратной совместимости
        private int convergedModeCounter;
        private int? divergedModeNumber;
        private string burdeningParameterDescription = "нет данных";
        private double burdeningStartValue;
        private bool burdeningStartValueKnown;
        private bool divergenceNotificationShown;
        private string lastStopDetails = string.Empty;
        private readonly List<BurdeningTrackedParameter> burdeningTrackedParameters = new List<BurdeningTrackedParameter>();
        private readonly Dictionary<string, double> burdeningLastAutoValues = new Dictionary<string, double>();
        private readonly Dictionary<string, List<double>> burdeningValueHistory = new Dictionary<string, List<double>>();
        private readonly Dictionary<string, string> burdeningHistoryDescriptions = new Dictionary<string, string>();
        private readonly List<string> burdeningHistoryOrder = new List<string>();
        private readonly Dictionary<string, double> lastConvergedTrackedValues = new Dictionary<string, double>();
        private readonly Dictionary<string, double> lastDivergedTrackedValues = new Dictionary<string, double>();

        private sealed class BurdeningTrackedParameter
        {
            public string Description { get; set; }
            public dynamic Data { get; set; }
            public string Key { get; set; }
            public double StartValue { get; set; }
            public double Step { get; set; }
            public int IntervalSeconds { get; set; }
        }

        // Временные переменные для обратной совместимости
        private List<GraphicNode> graphicNodes => GetGraphicNodes();
        private GraphicNode selectedNode => selectedElement as GraphicNode;

        private static readonly Color ThemePageBackground = Color.FromArgb(245, 250, 255);
        private static readonly Color ThemePanelBackground = Color.White;
        private static readonly Color ThemeAccentBlue = Color.FromArgb(219, 234, 254);
        private static readonly Color ThemeAccentBlueHover = Color.FromArgb(191, 219, 254);
        private static readonly Color ThemeAccentBluePressed = Color.FromArgb(147, 197, 253);
        private static readonly Color ThemeBorderBlue = Color.FromArgb(96, 165, 250);
        private static readonly Color ThemeTextBlack = Color.Black;

        private sealed class AdapterEntry
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public override string ToString()
            {
                return $"{Name} ({Description})";
            }
        }

        public Form1()
        {
            InitializeComponent();
            SetupWorkspaceTabs();
            SetupCanvas();
            SetupContextMenu();
            this.MouseWheel += Form1_MouseWheel; // зум колесом
            BackColor = ThemePageBackground;
            ForeColor = ThemeTextBlack;
            Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            ConfigureToolbarStyle();
            AddDynamicControls();
            buttonOpenReport.MouseDoubleClick += buttonOpenReport_MouseDoubleClick;
            buttonOpenReport.MouseUp += buttonOpenReport_MouseUp;
            ApplyTheme();
        }


        private void SetupWorkspaceTabs()
        {
            elementsGrid = BuildElementsGrid();

            panel1.BringToFront();
            statusStrip1.BringToFront();
        }

        private DataGridView BuildElementsGrid()
        {
            var grid = new DataGridView
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

            grid.ColumnHeadersDefaultCellStyle.BackColor = ThemePanelBackground;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeTextBlack;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.EnableHeadersVisualStyles = false;
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = ThemeTextBlack;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            grid.DefaultCellStyle.SelectionForeColor = ThemeTextBlack;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(220, 252, 231);
            grid.GridColor = Color.FromArgb(219, 234, 254);

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Тип", ReadOnly = true, Width = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Element", HeaderText = "Элемент", ReadOnly = true, Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Param", HeaderText = "Параметр", ReadOnly = true, Width = 140 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Значение", Width = 90 });
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Telemetry", HeaderText = "Телеметрия", Width = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Register", HeaderText = "Адрес", Width = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MeasureInterval", HeaderText = "Интервал изм., c", Width = 90 });
            grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "Protocol", HeaderText = "Протокол", Width = 95, DataSource = new[] { "Modbus TCP", "МЭК-104" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "IP", HeaderText = "IP адрес", Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Port", HeaderText = "Порт", Width = 65 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DeviceID", HeaderText = "ID устройства", Width = 85 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "Ping", HeaderText = "Пинг", Text = "Пинг", UseColumnTextForButtonValue = true, Width = 70 });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "Increment", HeaderText = "Инкремент", Text = "Настроить", UseColumnTextForButtonValue = true, Width = 90 });

            grid.CellEndEdit += ElementsGrid_CellEndEdit;
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += ElementsGrid_CellEndEdit;
            grid.CellContentClick += ElementsGrid_CellContentClick;
            return grid;
        }

        private void MoveClientControlsToTab(Panel targetClientPanel)
        {
            var contentPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 140,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(187, 247, 208)
            };

            labelTopClock.Parent = contentPanel;
            labelTopClock.AutoSize = true;
            labelTopClock.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
            labelTopClock.Location = new Point(22, 8);

            labelAdapter.Parent = contentPanel;
            labelAdapter.Location = new Point(22, 52);
            labelAdapter.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            comboBoxAdapters.Parent = contentPanel;
            comboBoxAdapters.Location = new Point(130, 50);
            comboBoxAdapters.Size = new Size(670, 28);
            comboBoxAdapters.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            labelIp.Parent = contentPanel;
            labelIp.Location = new Point(22, 95);
            labelIp.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            textBoxStaticIp.Parent = contentPanel;
            textBoxStaticIp.Location = new Point(60, 92);
            textBoxStaticIp.Size = new Size(230, 29);
            textBoxStaticIp.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            labelMask.Parent = contentPanel;
            labelMask.Location = new Point(305, 95);
            labelMask.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            textBoxMask.Parent = contentPanel;
            textBoxMask.Location = new Point(370, 92);
            textBoxMask.Size = new Size(150, 29);
            textBoxMask.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            labelGateway.Parent = contentPanel;
            labelGateway.Location = new Point(530, 95);
            labelGateway.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            textBoxGateway.Parent = contentPanel;
            textBoxGateway.Location = new Point(595, 92);
            textBoxGateway.Size = new Size(150, 29);
            textBoxGateway.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            buttonApplyStaticIp.Parent = contentPanel;
            buttonApplyStaticIp.Text = "Применить\r\nIP";
            buttonApplyStaticIp.Location = new Point(760, 45);
            buttonApplyStaticIp.Size = new Size(140, 75);
            buttonApplyStaticIp.Font = new Font("Segoe UI", 12F, FontStyle.Bold);

            var hint = new Label
            {
                Parent = contentPanel,
                AutoSize = true,
                Text = "Настройка адаптера: выберите интерфейс и задайте статический IP.",
                Location = new Point(920, 100),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = ThemeTextBlack
            };

            targetClientPanel.Controls.Add(contentPanel);
        }

        private void AddDynamicControls()
        {
            buttonOpenTelemetryForm = new Button
            {
                Name = "buttonOpenTelemetryForm",
                Text = "Телеметрия",
                Width = 150,
                Height = 30,
                Left = 170,
                Top = 48,
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeAccentBlue,
                ForeColor = ThemeTextBlack
            };
            buttonOpenTelemetryForm.FlatAppearance.BorderSize = 2;
            buttonOpenTelemetryForm.Click += buttonOpenTelemetryForm_Click;

            buttonOpenClientSettingsForm = new Button
            {
                Name = "buttonOpenClientSettingsForm",
                Text = "Настройка клиента",
                Width = 150,
                Height = 30,
                Left = 326,
                Top = 48,
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeAccentBlue,
                ForeColor = ThemeTextBlack
            };
            buttonOpenClientSettingsForm.FlatAppearance.BorderSize = 2;
            buttonOpenClientSettingsForm.Click += buttonOpenClientSettingsForm_Click;

            buttonCalcSettings = new Button
            {
                Name = "buttonCalcSettings",
                Text = "Параметры расчёта",
                Width = 150,
                Height = 30,
                Left = 12,
                Top = 48,
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeAccentBlue,
                ForeColor = ThemeTextBlack
            };
            buttonCalcSettings.FlatAppearance.BorderSize = 2;
            buttonCalcSettings.Click += (s, e) =>
            {
                var settingsForm = new CalculationSettingsForm();
                RegisterOpenedWindow(settingsForm);
                settingsForm.StartPosition = FormStartPosition.Manual;
                settingsForm.Location = GetNextChildWindowLocation();
                settingsForm.Show(this);
            };

            panel1.Controls.Add(buttonOpenTelemetryForm);
            panel1.Controls.Add(buttonOpenClientSettingsForm);
            panel1.Controls.Add(buttonCalcSettings);

            checkBoxShowBranchInfo = new CheckBox
            {
                Name = "checkBoxShowBranchInfo",
                Text = "Показывать токи/загрузку на ветвях",
                AutoSize = true,
                Left = 490,
                Top = 80,
                Checked = true,
                BackColor = ThemePanelBackground,
                ForeColor = ThemeTextBlack
            };
            checkBoxShowBranchInfo.CheckedChanged += (s, e) =>
            {
                showBranchCurrentOverlay = checkBoxShowBranchInfo.Checked;
                panel2.Invalidate();
            };
            panel1.Controls.Add(checkBoxShowBranchInfo);

            checkBoxLockLegends = new CheckBox
            {
                Name = "checkBoxLockLegends",
                Text = "Закрепить легенды",
                AutoSize = true,
                Left = 730,
                Top = 80,
                Checked = true,
                BackColor = ThemePanelBackground,
                ForeColor = ThemeTextBlack
            };
            checkBoxLockLegends.CheckedChanged += (s, e) => legendsLocked = checkBoxLockLegends.Checked;
            panel1.Controls.Add(checkBoxLockLegends);

            checkBoxComprehensiveControl = new CheckBox
            {
                Name = "checkBoxComprehensiveControl",
                Text = "Комплексный контроль параметров",
                AutoSize = true,
                Left = 900,
                Top = 80,
                Checked = true,
                BackColor = ThemePanelBackground,
                ForeColor = ThemeTextBlack
            };
            checkBoxComprehensiveControl.CheckedChanged += (s, e) => comprehensiveControlEnabled = checkBoxComprehensiveControl.Checked;
            panel1.Controls.Add(checkBoxComprehensiveControl);

            buttonPrevMode = new Button
            {
                Name = "buttonPrevMode",
                Text = "Предыдущий режим",
                Width = 145,
                Height = 30,
                Left = 980,
                Top = 48,
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeAccentBlue,
                ForeColor = ThemeTextBlack,
                Enabled = false
            };
            buttonPrevMode.FlatAppearance.BorderSize = 2;
            buttonPrevMode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonPrevMode.Click += (s, e) => ShowConvergedSnapshot();
            panel1.Controls.Add(buttonPrevMode);

            buttonNextMode = new Button
            {
                Name = "buttonNextMode",
                Text = "Последний режим",
                Width = 145,
                Height = 30,
                Left = 1130,
                Top = 48,
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeAccentBlue,
                ForeColor = ThemeTextBlack,
                Enabled = false
            };
            buttonNextMode.FlatAppearance.BorderSize = 2;
            buttonNextMode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonNextMode.Click += (s, e) => ShowDivergedSnapshot();
            panel1.Controls.Add(buttonNextMode);
        }

        private void UpdateSnapshotNavigationButtons()
        {
            bool canNavigate = !isCalculationRunning && hasLastConvergedSnapshot && hasLastDivergedSnapshot;
            if (buttonPrevMode != null)
            {
                buttonPrevMode.Enabled = canNavigate && showingDivergedSnapshot;
            }

            if (buttonNextMode != null)
            {
                buttonNextMode.Enabled = canNavigate && !showingDivergedSnapshot;
            }
        }

        private void ShowConvergedSnapshot()
        {
            if (!hasLastConvergedSnapshot)
            {
                return;
            }

            RestoreTrackedParameterSnapshot(lastConvergedTrackedValues);
            RecalculateSnapshotState();
            showingDivergedSnapshot = false;
            panel2.Invalidate();
            RefreshElementsGrid();
            RefreshOpenedReportForms();
            UpdateSnapshotNavigationButtons();
        }

        private void ShowDivergedSnapshot()
        {
            if (!hasLastDivergedSnapshot)
            {
                return;
            }

            RestoreTrackedParameterSnapshot(lastDivergedTrackedValues);
            RecalculateSnapshotState();
            showingDivergedSnapshot = true;
            panel2.Invalidate();
            RefreshElementsGrid();
            RefreshOpenedReportForms();
            UpdateSnapshotNavigationButtons();
        }

        private void buttonOpenTelemetryForm_Click(object sender, EventArgs e)
        {
            if (telemetryEditorForm != null && !telemetryEditorForm.IsDisposed)
            {
                if (!telemetryEditorForm.Visible)
                {
                    telemetryEditorForm.Show(this);
                }
                if (telemetryEditorForm.WindowState == FormWindowState.Minimized)
                {
                    telemetryEditorForm.WindowState = FormWindowState.Normal;
                }
                telemetryEditorForm.BringToFront();
                telemetryEditorForm.Focus();
                return;
            }

            telemetryEditorForm = new TelemetryEditorForm(
                () => graphicElements,
                () => graphicBranches,
                () => graphicShunts,
                () =>
                {
                    panel2.Invalidate();
                    RefreshElementsGrid();
                    RefreshOpenedEditorForms();
                });
            RegisterOpenedWindow(telemetryEditorForm);
            telemetryEditorForm.StartPosition = FormStartPosition.Manual;
            telemetryEditorForm.Location = GetNextChildWindowLocation();
            telemetryEditorForm.FormClosed += (s, args) => telemetryEditorForm = null;
            telemetryEditorForm.Show(this);
        }

        private void buttonOpenClientSettingsForm_Click(object sender, EventArgs e)
        {
            if (clientSettingsForm != null && !clientSettingsForm.IsDisposed)
            {
                if (!clientSettingsForm.Visible)
                {
                    clientSettingsForm.Show(this);
                }
                if (clientSettingsForm.WindowState == FormWindowState.Minimized)
                {
                    clientSettingsForm.WindowState = FormWindowState.Normal;
                }
                clientSettingsForm.BringToFront();
                clientSettingsForm.Focus();
                return;
            }

            clientSettingsForm = new ClientSettingsForm();
            RegisterOpenedWindow(clientSettingsForm);
            clientSettingsForm.StartPosition = FormStartPosition.Manual;
            clientSettingsForm.Location = GetNextChildWindowLocation();
            clientSettingsForm.FormClosed += (s, args) => clientSettingsForm = null;
            clientSettingsForm.Show(this);
        }


        private void GroupSelectedElements()
        {
            var movable = selectedElements.Where(IsMovableElement).ToList();
            if (movable.Count < 2)
            {
                MessageBox.Show("Выделите минимум два элемента для группировки.");
                return;
            }

            int groupId = nextGroupId++;
            foreach (var element in movable)
            {
                elementGroups[element] = groupId;
            }

            MessageBox.Show($"Создана группа из {movable.Count} элементов.");
        }


        private void UngroupSelectedElements()
        {
            var toUngroup = selectedElements.Where(e => elementGroups.ContainsKey(e)).ToList();
            foreach (var element in toUngroup)
            {
                elementGroups.Remove(element);
            }

            if (toUngroup.Count == 0)
            {
                MessageBox.Show("Среди выделенных элементов нет групп.");
            }
        }

        private bool IsMovableElement(object element)
        {
            return element is GraphicNode || element is GraphicBaseNode || element is GraphicShunt;
        }

        private IEnumerable<object> GetDragTargets(object primary)
        {
            var dragTargets = selectedElements.Where(IsMovableElement).ToList();
            if (dragTargets.Count > 1)
            {
                return dragTargets;
            }

            if (primary != null && elementGroups.TryGetValue(primary, out int groupId))
            {
                return elementGroups.Where(kv => kv.Value == groupId).Select(kv => kv.Key).Where(IsMovableElement).ToList();
            }

            return primary != null ? new[] { primary } : Enumerable.Empty<object>();
        }

        private void RefreshElementsGrid()
        {
            if (elementsGrid == null) return;
            elementsGrid.Rows.Clear();

            AddSectionRow("Узлы");
            foreach (var node in graphicElements.OfType<GraphicNode>().OrderBy(n => n.Data.Number))
                AddRowsForNode("Узел", $"N{node.Data.Number}", node.Data, node, new[] { ("U", "Номинальное напряжение, кВ", node.Data.InitialVoltage), ("Ufact", "Фактическое напряжение, кВ", node.Data.ActualVoltage), ("Ucalc", "Расчётное напряжение, кВ", node.Data.CalculatedVoltage), ("P", "P нагрузка, МВт", node.Data.NominalActivePower), ("Q", "Q нагрузка, Мвар", node.Data.NominalReactivePower), ("Pg", "P генерация, МВт", node.Data.ActivePowerGeneration), ("Qg", "Q генерация, Мвар", node.Data.ReactivePowerGeneration), ("Uf", "U фикс., кВ", node.Data.FixedVoltageModule), ("Qmin", "Q мин, Мвар", node.Data.MinReactivePower), ("Qmax", "Q макс, Мвар", node.Data.MaxReactivePower) });

            AddSectionRow("Базисный узел");
            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>().OrderBy(n => n.Data.Number))
                AddRowsForNode("Базисный узел", $"B{baseNode.Data.Number}", baseNode.Data, baseNode, new[] { ("U", "Номинальное напряжение, кВ", baseNode.Data.InitialVoltage), ("Ufact", "Фактическое напряжение, кВ", baseNode.Data.ActualVoltage), ("Ucalc", "Расчётное напряжение, кВ", baseNode.Data.CalculatedVoltage), ("P", "P нагрузка, МВт", baseNode.Data.NominalActivePower), ("Q", "Q нагрузка, Мвар", baseNode.Data.NominalReactivePower), ("Pg", "P генерация, МВт", baseNode.Data.ActivePowerGeneration), ("Qg", "Q генерация, Мвар", baseNode.Data.ReactivePowerGeneration), ("Uf", "U фикс., кВ", baseNode.Data.FixedVoltageModule), ("Qmin", "Q мин, Мвар", baseNode.Data.MinReactivePower), ("Qmax", "Q макс, Мвар", baseNode.Data.MaxReactivePower) });

            AddSectionRow("Ветви");
            foreach (var branch in graphicBranches.OrderBy(b => b.Data.StartNodeNumber).ThenBy(b => b.Data.EndNodeNumber))
                AddRowsForNode("Ветвь", $"{branch.Data.StartNodeNumber}-{branch.Data.EndNodeNumber}", branch.Data, branch, new[] { ("R", "R, Ом", branch.Data.ActiveResistance), ("X", "X, Ом", branch.Data.ReactiveResistance), ("B", "B, См", branch.Data.ReactiveConductivity), ("Ktr", "K трансф., о.е.", branch.Data.TransformationRatio), ("G", "G, См", branch.Data.ActiveConductivity), ("Imax", "Iдоп, А", branch.Data.PermissibleCurrent) });

            AddSectionRow("Шунты");
            foreach (var shunt in graphicShunts.OrderBy(s => s.Data.StartNodeNumber))
                AddRowsForNode("Шунт", $"Sh{shunt.Data.StartNodeNumber}", shunt.Data, shunt, new[] { ("R", "R, Ом", shunt.Data.ActiveResistance), ("X", "X, Ом", shunt.Data.ReactiveResistance) });
        }

        private void AddSectionRow(string title)
        {
            int index = elementsGrid.Rows.Add(title, "", "", "", false, "", "", "", "", "", "", "", "");
            var row = elementsGrid.Rows[index];
            row.ReadOnly = true;
            row.DefaultCellStyle.BackColor = Color.FromArgb(187, 247, 208);
            row.DefaultCellStyle.ForeColor = ThemeTextBlack;
            row.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            row.Tag = null;
        }

        private void AddRowsForNode(string type, string elementName, dynamic data, object owner, IEnumerable<(string Key, string Label, double Value)> rows)
        {
            foreach (var row in rows)
            {
                bool telemetryEnabled = data.ParamAutoModes.ContainsKey(row.Key) && data.ParamAutoModes[row.Key];
                string registerValue = data.ParamRegisters.ContainsKey(row.Key) ? data.ParamRegisters[row.Key] : string.Empty;
                bool isNominalVoltageRow = row.Key == "U";
                bool isCalculatedVoltageRow = row.Key == "Ucalc";
                if ((type == "Узел" || type == "Базисный узел") && (isNominalVoltageRow || isCalculatedVoltageRow))
                {
                    telemetryEnabled = false;
                    registerValue = string.Empty;
                }

                int index = elementsGrid.Rows.Add(type, elementName, row.Label, row.Value, telemetryEnabled, registerValue, data.MeasurementIntervalSeconds, data.Protocol, data.IPAddress, data.Port, data is Node ? data.NodeID : data.DeviceID, "Пинг", "Настроить");
                elementsGrid.Rows[index].Tag = Tuple.Create(owner, row.Key, data);

                if ((type == "Узел" || type == "Базисный узел") && (isNominalVoltageRow || isCalculatedVoltageRow))
                {
                    var createdRow = elementsGrid.Rows[index];
                    createdRow.Cells["Telemetry"].ReadOnly = true;
                    createdRow.Cells["Register"].ReadOnly = true;
                    createdRow.Cells["Telemetry"].Style.BackColor = Color.Gainsboro;
                    createdRow.Cells["Register"].Style.BackColor = Color.Gainsboro;
                }

                if ((type == "Узел" || type == "Базисный узел") && isCalculatedVoltageRow)
                {
                    elementsGrid.Rows[index].Cells["Value"].ReadOnly = true;
                    elementsGrid.Rows[index].Cells["Value"].Style.BackColor = Color.Gainsboro;
                }
            }
        }

        private void ElementsGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = elementsGrid.Rows[e.RowIndex];
            if (row.Tag == null) return;
            if (row.Tag is Tuple<object, string, object> tag)
            {
                dynamic data = tag.Item3;
                string key = tag.Item2;
                bool isNodeVoltageStaticRow = (data is Node || data is BaseNode) && (key == "U" || key == "Ucalc");
                if (key != "Ucalc")
                {
                    if (double.TryParse(Convert.ToString(row.Cells["Value"].Value), out double val))
                    {
                        ApplyParamValue(data, key, val);
                    }
                }

                if (!isNodeVoltageStaticRow)
                {
                    data.ParamAutoModes[key] = Convert.ToBoolean(row.Cells["Telemetry"].Value);
                    data.ParamRegisters[key] = Convert.ToString(row.Cells["Register"].Value) ?? "0";
                }
                if (int.TryParse(Convert.ToString(row.Cells["MeasureInterval"].Value), out int measureInterval))
                {
                    data.MeasurementIntervalSeconds = Math.Max(1, measureInterval);
                }
                data.Protocol = Convert.ToString(row.Cells["Protocol"].Value) ?? "Modbus TCP";
                data.IPAddress = Convert.ToString(row.Cells["IP"].Value) ?? "127.0.0.1";
                data.Port = Convert.ToString(row.Cells["Port"].Value) ?? "502";
                if (data is Node) data.NodeID = Convert.ToString(row.Cells["DeviceID"].Value) ?? "1";
                else data.DeviceID = Convert.ToString(row.Cells["DeviceID"].Value) ?? "1";
                panel2.Invalidate();
            }
        }


        private async void ElementsGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var row = elementsGrid.Rows[e.RowIndex];
            if (row.Tag == null) return;

            if (elementsGrid.Columns[e.ColumnIndex].Name == "Increment")
            {
                if (row.Tag is Tuple<object, string, object> incTag)
                {
                    dynamic data = incTag.Item3;
                    string key = incTag.Item2;
                    ConfigureIncrement(data, key, Convert.ToString(row.Cells["Element"].Value) + "." + Convert.ToString(row.Cells["Param"].Value));
                }
                return;
            }

            if (elementsGrid.Columns[e.ColumnIndex].Name != "Ping") return;

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

        private void ConfigureIncrement(dynamic data, string key, string title)
        {
            string id = ParameterAutoChangeService.BuildId(data, key);
            bool hasConfig = ParameterAutoChangeService.TryGet(id, out double oldStep, out int oldInterval, out bool oldEnabled);
            if (!hasConfig)
            {
                if (data.ParamIncrementSteps.ContainsKey(key)) oldStep = Convert.ToDouble(data.ParamIncrementSteps[key]);
                if (data.ParamIncrementIntervals.ContainsKey(key)) oldInterval = Convert.ToInt32(data.ParamIncrementIntervals[key]);
            }
            using (var form = new IncrementSettingsForm(title, oldStep, oldInterval, oldEnabled))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                data.ParamIncrementSteps[key] = form.StepValue;
                data.ParamIncrementIntervals[key] = form.IntervalSeconds;
                Action onTick = () =>
                {
                    BeginInvoke(new Action(() =>
                    {
                        LogBurdeningAutoChangeTick(data, key);
                        RefreshElementsGrid();
                        panel2.Invalidate();
                    }));
                };

                ParameterAutoChangeService.Configure(
                    id,
                    form.StepValue,
                    form.IntervalSeconds,
                    form.EnabledChange,
                    () => GetParamValue(data, key),
                    newValue =>
                    {
                        if (!isCalculationRunning)
                        {
                            return;
                        }

                        ApplyParamValue(data, key, newValue);
                    },
                    onTick);

                if (form.EnabledChange)
                {
                    RegisterBurdeningTracking(data, key, form.StepValue, form.IntervalSeconds);
                    RefreshOpenedReportForms();
                }
            }

        }

        private void AppendBurdeningAutoChangeEvent(dynamic data, string key)
        {
            if (!isCalculationRunning)
            {
                return;
            }

            string element = "Элемент";
            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                if (ReferenceEquals(node.Data, data))
                {
                    element = $"Узел {node.Data.Number}";
                    break;
                }
            }

            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                if (ReferenceEquals(baseNode.Data, data))
                {
                    element = $"Базисный узел {baseNode.Data.Number}";
                    break;
                }
            }

            foreach (var branch in graphicBranches)
            {
                if (ReferenceEquals(branch.Data, data))
                {
                    element = $"Ветвь {branch.Data.StartNodeNumber}-{branch.Data.EndNodeNumber}";
                    break;
                }
            }

            foreach (var shunt in graphicShunts)
            {
                if (ReferenceEquals(shunt.Data, data))
                {
                    element = $"Шунт {shunt.Data.StartNodeNumber}";
                    break;
                }
            }

            double currentValue = GetParamValue(data, key);
            string autoKey = $"{element}|{key}";
            if (burdeningLastAutoValues.TryGetValue(autoKey, out var prev))
            {
                burdeningIterationLog.Add($"Автоизм.: {element} | {ConvertParamKeyToLabel(key)}: Было {prev.ToString("F4", CultureInfo.InvariantCulture)} -> Стало {currentValue.ToString("F4", CultureInfo.InvariantCulture)}");
            }
            else
            {
                burdeningIterationLog.Add($"Автоизм.: {element} | {ConvertParamKeyToLabel(key)}: Старт {currentValue.ToString("F4", CultureInfo.InvariantCulture)}");
            }

            burdeningLastAutoValues[autoKey] = currentValue;
        }

        private void AppendBurdeningAutoChangeLogEvent(dynamic data, string key)
        {
            if (!isCalculationRunning)
            {
                return;
            }

            string element = ResolveBurdeningElementLabel(data);
            double currentValue = GetParamValue(data, key);
            string autoKey = $"{element}|{key}";
            if (burdeningLastAutoValues.TryGetValue(autoKey, out var prev))
            {
                burdeningIterationLog.Add($"Автоизм.: {element} | {ConvertParamKeyToLabel(key)}: Было {prev.ToString("F4", CultureInfo.InvariantCulture)} -> Стало {currentValue.ToString("F4", CultureInfo.InvariantCulture)}");
            }
            else
            {
                burdeningIterationLog.Add($"Автоизм.: {element} | {ConvertParamKeyToLabel(key)}: Старт {currentValue.ToString("F4", CultureInfo.InvariantCulture)}");
            }

            burdeningLastAutoValues[autoKey] = currentValue;
        }

        private void LogBurdeningAutoChangeTick(dynamic data, string key)
        {
            if (!isCalculationRunning)
            {
                return;
            }

            string element = ResolveBurdeningElementLabel(data);
            double latestValue = GetParamValue(data, key);
            string historyKey = $"{element}|{key}";
            if (!burdeningValueHistory.TryGetValue(historyKey, out var values))
            {
                values = new List<double>();
                burdeningValueHistory[historyKey] = values;
                burdeningHistoryOrder.Add(historyKey);
                burdeningHistoryDescriptions[historyKey] = $"{element} | {ConvertParamKeyToLabel(key)}";
            }

            if (values.Count == 0)
            {
                values.Add(latestValue);
            }
            else if (Math.Abs(values[values.Count - 1] - latestValue) > 1e-9)
            {
                values.Add(latestValue);
            }

            burdeningLastAutoValues[historyKey] = latestValue;
            RefreshOpenedReportForms();
        }

        private string ResolveBurdeningElementLabel(dynamic data)
        {
            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                if (ReferenceEquals(node.Data, data))
                {
                    return $"Узел {node.Data.Number}";
                }
            }

            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                if (ReferenceEquals(baseNode.Data, data))
                {
                    return $"Базисный узел {baseNode.Data.Number}";
                }
            }

            foreach (var branch in graphicBranches)
            {
                if (ReferenceEquals(branch.Data, data))
                {
                    return $"Ветвь {branch.Data.StartNodeNumber}-{branch.Data.EndNodeNumber}";
                }
            }

            foreach (var shunt in graphicShunts)
            {
                if (ReferenceEquals(shunt.Data, data))
                {
                    return $"Шунт {shunt.Data.StartNodeNumber}";
                }
            }

            return "Элемент";
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

        private async Task<bool> PingHostAsync(string ip)
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

        private void SetupCanvas()
        {
            panel2.BackColor = Color.White;
            panel2.BorderStyle = BorderStyle.FixedSingle;
            panel2.GetType().GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(panel2, true, null);
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            panel2.Paint += Panel2_Paint;
            panel2.MouseDown += Panel2_MouseDown;
            panel2.MouseMove += Panel2_MouseMove;
            panel2.MouseUp += Panel2_MouseUp;
            panel2.DoubleClick += Panel2_DoubleClick;
            panel2.MouseLeave += (s, e) => UpdateHoverTooltip(null, Point.Empty);
        }

        private void SetupContextMenu()
        {
            contextMenuStrip = new ContextMenuStrip();
        }

        private void Panel2_Paint(object sender, PaintEventArgs e)
        {


            e.Graphics.ScaleTransform(scale, scale);
            e.Graphics.TranslateTransform(pan.X / scale, pan.Y / scale);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.Clear(panel2.BackColor);

            Console.WriteLine($"=== ОТРИСОВКА ===");
            Console.WriteLine($"Ветвей: {graphicBranches.Count}, Шунтов: {graphicShunts.Count}, Узлов: {graphicElements.Count}");

            // 1. Сначала рисуем линии соединения шунтов (самый задний план)
            foreach (var shunt in graphicShunts)
            {
                DrawShuntConnectionLine(e.Graphics, shunt);
            }

            // 2. Затем рисуем ветви
            foreach (var branch in graphicBranches)
            {
                branch.Draw(e.Graphics);
                if (showBranchCurrentOverlay)
                {
                    DrawBranchCurrentInfo(e.Graphics, branch);
                }
            }

            // 3. Затем рисуем прямоугольники шунтов
            foreach (var shunt in graphicShunts)
            {
                Console.WriteLine($"Рисуем шунт на узле {shunt.GetConnectedNodeNumber()}");
                shunt.Draw(e.Graphics);
            }

            // 4. Затем рисуем узлы поверх всего
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode graphicNode)
                {
                    graphicNode.Draw(e.Graphics);
                }
                else if (element is GraphicBaseNode graphicBaseNode)
                {
                    graphicBaseNode.Draw(e.Graphics);
                }
            }

            if (isMarqueeSelecting)
            {
                var rect = GetMarqueeRectangle();
                using (var pen = new Pen(Color.DodgerBlue, 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                {
                    e.Graphics.DrawRectangle(pen, rect);
                }
            }

            DrawBranchLegend(e.Graphics);
            DrawConvergenceStatus(e.Graphics);
            DrawVoltageGostLegend(e.Graphics);
        }

        private void DrawBranchCurrentInfo(Graphics g, GraphicBranch branch)
        {
            if (branch == null || branch.StartNode == null || branch.EndNode == null)
            {
                return;
            }

            Point start = NodeGraphicsHelper.GetNodeCenter(branch.StartNode);
            Point end = NodeGraphicsHelper.GetNodeCenter(branch.EndNode);
            Point middle = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);

            double id = branch.Data.PermissibleCurrent <= 0 ? 600 : branch.Data.PermissibleCurrent;
            double ir = branch.Data.CalculatedCurrent;
            double loading = branch.Data.LoadingPercent;

            string info = $"Iд={id:F2}A  Iр={ir:F2}A  Загрузка={loading:F2}%";
            var size = g.MeasureString(info, Font);
            var rect = new RectangleF(middle.X - size.Width / 2f - 4f, middle.Y - 25f, size.Width + 8f, size.Height + 2f);

            using (var bg = new SolidBrush(Color.FromArgb(225, Color.White)))
            using (var textBrush = new SolidBrush(Color.Black))
            using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                g.FillRectangle(bg, rect);
                g.DrawString(info, font, textBrush, rect.X + 4f, rect.Y + 1f);
            }
        }

        private void DrawBranchLegend(Graphics g)
        {
            var state = g.Save();
            g.ResetTransform();

            int defaultX = Math.Max(6, panel2.ClientSize.Width - 365);
            int defaultY = Math.Max(6, panel2.ClientSize.Height - 170);
            int x = Math.Max(6, defaultX + branchLegendOffset.X);
            int y = Math.Max(6, defaultY + branchLegendOffset.Y);
            int rowH = 24;
            int legendW = 350;
            int legendH = 155;
            branchLegendBounds = new Rectangle(x, y, legendW, legendH);

            using (var bg = new SolidBrush(Color.FromArgb(235, Color.White)))
            using (var border = new Pen(Color.FromArgb(96, 165, 250), 1))
            using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                g.FillRectangle(bg, x, y, legendW, legendH);
                g.DrawRectangle(border, x, y, legendW, legendH);
                g.DrawString("Легенда загрузки ветвей", font, Brushes.Black, x + 8, y + 6);

                var rows = new[]
                {
                    (Color.Green, "0% - 80%", "Оптимальный режим"),
                    (Color.Yellow, "80% - 100%", "Внимание! Близко к пределу"),
                    (Color.Red, "> 100%", "ПЕРЕГРУЗКА! Риск повреждения")
                };

                for (int i = 0; i < rows.Length; i++)
                {
                    int ry = y + 30 + i * rowH;
                    using (var colorBrush = new SolidBrush(rows[i].Item1))
                    {
                        g.FillRectangle(colorBrush, x + 8, ry + 4, 18, 12);
                    }

                    g.DrawRectangle(Pens.Black, x + 8, ry + 4, 18, 12);
                    g.DrawString($"{rows[i].Item2}: {rows[i].Item3}", font, Brushes.Black, x + 34, ry + 1);
                }
            }

            g.Restore(state);
        }

        private void DrawConvergenceStatus(Graphics g)
        {
            var state = g.Save();
            g.ResetTransform();

            string title = "Режим:";
            string value = hasConvergenceStatus
                ? (isLastModeConverged ? "СХОДИТСЯ" : "НЕ СХОДИТСЯ")
                : "не рассчитан";

            Color statusColor = hasConvergenceStatus
                ? (isLastModeConverged ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38))
                : Color.FromArgb(107, 114, 128);

            using (var font = new Font("Segoe UI", 9F, FontStyle.Bold))
            {
                var titleSize = g.MeasureString(title, font);
                var valueSize = g.MeasureString(value, font);
                int width = (int)Math.Ceiling(titleSize.Width + valueSize.Width + 30);
                int height = 30;
                int x = Math.Max(6, panel2.ClientSize.Width - width - 10);
                int y = 8;

                using (var bg = new SolidBrush(Color.FromArgb(235, Color.White)))
                using (var border = new Pen(Color.FromArgb(96, 165, 250), 1))
                using (var valueBrush = new SolidBrush(statusColor))
                {
                    g.FillRectangle(bg, x, y, width, height);
                    g.DrawRectangle(border, x, y, width, height);
                    g.DrawString(title, font, Brushes.Black, x + 10, y + 7);
                    g.DrawString(value, font, valueBrush, x + 14 + titleSize.Width, y + 7);
                }
            }

            g.Restore(state);
        }

        private void DrawVoltageGostLegend(Graphics g)
        {
            var state = g.Save();
            g.ResetTransform();

            int legendW = 360;
            int legendH = 120;
            int defaultX = Math.Max(6, panel2.ClientSize.Width - legendW - 10);
            int defaultY = 130;
            int x = Math.Max(6, defaultX + voltageLegendOffset.X);
            int y = Math.Max(6, defaultY + voltageLegendOffset.Y);
            voltageLegendBounds = new Rectangle(x, y, legendW, legendH);

            using (var bg = new SolidBrush(Color.FromArgb(235, Color.White)))
            using (var border = new Pen(Color.FromArgb(96, 165, 250), 1))
            using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                g.FillRectangle(bg, x, y, legendW, legendH);
                g.DrawRectangle(border, x, y, legendW, legendH);
                g.DrawString("ГОСТ 32144-2013", font, Brushes.Black, x + 8, y + 6);

                var rows = new[]
                {
                    (Color.LightGreen, "|ΔU| ≤ 5%", "Норма"),
                    (Color.Yellow, "5% < |ΔU| ≤ 10%", "Предупреждение"),
                    (Color.IndianRed, "|ΔU| > 10%", "Критическое")
                };

                for (int i = 0; i < rows.Length; i++)
                {
                    int ry = y + 30 + i * 26;
                    using (var colorBrush = new SolidBrush(rows[i].Item1))
                    {
                        g.FillRectangle(colorBrush, x + 8, ry + 4, 18, 12);
                    }

                    g.DrawRectangle(Pens.Black, x + 8, ry + 4, 18, 12);
                    g.DrawString($"{rows[i].Item2}: {rows[i].Item3}", font, Brushes.Black, x + 34, ry + 1);
                }
            }

            g.Restore(state);
        }

        private Rectangle GetMarqueeRectangle()
        {
            int x = Math.Min(marqueeStartModel.X, marqueeCurrentModel.X);
            int y = Math.Min(marqueeStartModel.Y, marqueeCurrentModel.Y);
            int w = Math.Abs(marqueeCurrentModel.X - marqueeStartModel.X);
            int h = Math.Abs(marqueeCurrentModel.Y - marqueeStartModel.Y);
            return new Rectangle(x, y, w, h);
        }

        // Метод для отрисовки линии соединения шунта отдельно
        private void DrawShuntConnectionLine(Graphics g, GraphicShunt shunt)
        {
            if (shunt.ConnectedNode == null) return;

            Point nodeCenter = GetNodeCenter(shunt.ConnectedNode);
            Point shuntCenter = new Point(
                shunt.Location.X + GraphicShunt.ShuntSize.Width / 2,
                shunt.Location.Y + GraphicShunt.ShuntSize.Height / 2
            );

            // Рисуем линию от центра узла к центру шунта
            g.DrawLine(new Pen(Color.Gray, 1), nodeCenter, shuntCenter);
        }

        // Вспомогательный метод для получения центра узла
        private Point GetNodeCenter(object node)
        {
            if (node is GraphicNode graphicNode)
            {
                return new Point(
                    graphicNode.Location.X + GraphicNode.NodeSize.Width / 2,
                    graphicNode.Location.Y + GraphicNode.NodeSize.Height / 2
                );
            }
            else if (node is GraphicBaseNode graphicBaseNode)
            {
                return new Point(
                    graphicBaseNode.Location.X + GraphicBaseNode.NodeSize.Width / 2,
                    graphicBaseNode.Location.Y + GraphicBaseNode.NodeSize.Height / 2
                );
            }
            return Point.Empty;
        }

        private void Panel2_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                panning = true;
                rightMouseMoved = false;
                rightMouseDownPoint = e.Location;
                lastPanPos = e.Location;

                Point modelPointRight = Point.Round(ScreenToModel(e.Location));
                object hitRight = FindElementAt(modelPointRight);
                if (hitRight != null && !selectedElements.Contains(hitRight))
                {
                    ClearAllSelection();
                    SelectElement(hitRight);
                    panel2.Invalidate();
                }
                return;
            }

            if (e.Button != MouseButtons.Left) return;

            if (!legendsLocked)
            {
                if (branchLegendBounds.Contains(e.Location))
                {
                    isDraggingLegend = true;
                    draggingLegendKey = "branch";
                    lastLegendMousePoint = e.Location;
                    return;
                }

                if (voltageLegendBounds.Contains(e.Location))
                {
                    isDraggingLegend = true;
                    draggingLegendKey = "voltage";
                    lastLegendMousePoint = e.Location;
                    return;
                }
            }

            Point modelPoint = Point.Round(ScreenToModel(e.Location));
            bool ctrlPressed = (ModifierKeys & Keys.Control) == Keys.Control;
            object hitElement = FindElementAt(modelPoint);

            if (hitElement != null)
            {
                if (ctrlPressed)
                {
                    if (!selectedElements.Contains(hitElement))
                    {
                        SelectElement(hitElement);
                    }
                }
                else
                {
                    if (!selectedElements.Contains(hitElement) || selectedElements.Count > 1)
                    {
                        ClearAllSelection();
                        SelectElement(hitElement);
                    }
                }

                selectedElement = hitElement;
                isDragging = true;
                lastMousePosition = e.Location;
                panel2.Invalidate();
                return;
            }

            if (!ctrlPressed)
            {
                ClearAllSelection();
            }

            isMarqueeSelecting = true;
            marqueeStartModel = modelPoint;
            marqueeCurrentModel = modelPoint;
            panel2.Invalidate();
        }

        private object FindElementAt(Point modelPoint)
        {
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node && node.Contains(modelPoint)) return node;
                if (element is GraphicBaseNode baseNode && baseNode.Contains(modelPoint)) return baseNode;
            }

            foreach (var shunt in graphicShunts)
            {
                if (shunt.Contains(modelPoint)) return shunt;
            }

            foreach (var branch in graphicBranches)
            {
                if (branch.Contains(modelPoint)) return branch;
            }

            return null;
        }

        private void ToggleSelection(object element)
        {
            if (selectedElements.Contains(element))
            {
                selectedElements.Remove(element);
                SetElementSelectedState(element, false);
            }
            else
            {
                SelectElement(element);
            }
        }

        private void SelectElement(object element)
        {
            selectedElements.Add(element);
            selectedElement = element;
            SetElementSelectedState(element, true);
        }

        private void ClearAllSelection()
        {
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node) node.IsSelected = false;
                else if (element is GraphicBaseNode baseNode) baseNode.IsSelected = false;
            }

            foreach (var branch in graphicBranches) branch.IsSelected = false;
            foreach (var shunt in graphicShunts) shunt.IsSelected = false;

            selectedElements.Clear();
            selectedElement = null;
        }

        private void SetElementSelectedState(object element, bool selected)
        {
            if (element is GraphicNode node) node.IsSelected = selected;
            else if (element is GraphicBaseNode baseNode) baseNode.IsSelected = selected;
            else if (element is GraphicShunt shunt) shunt.IsSelected = selected;
            else if (element is GraphicBranch branch) branch.IsSelected = selected;
        }


        private void Panel2_MouseMove(object sender, MouseEventArgs e)
        {
            var modelPoint = Point.Round(ScreenToModel(e.Location));

            if (isDraggingLegend)
            {
                int dxLegend = e.X - lastLegendMousePoint.X;
                int dyLegend = e.Y - lastLegendMousePoint.Y;
                lastLegendMousePoint = e.Location;

                if (draggingLegendKey == "branch")
                {
                    branchLegendOffset = new Point(branchLegendOffset.X + dxLegend, branchLegendOffset.Y + dyLegend);
                }
                else if (draggingLegendKey == "voltage")
                {
                    voltageLegendOffset = new Point(voltageLegendOffset.X + dxLegend, voltageLegendOffset.Y + dyLegend);
                }

                panel2.Invalidate();
                return;
            }

            if (panning)
            {
                UpdateHoverTooltip(null, e.Location);
                int dx = e.X - lastPanPos.X;
                int dy = e.Y - lastPanPos.Y;
                if (Math.Abs(e.X - rightMouseDownPoint.X) > 3 || Math.Abs(e.Y - rightMouseDownPoint.Y) > 3)
                {
                    rightMouseMoved = true;
                }

                pan = new PointF(pan.X + dx, pan.Y + dy);
                lastPanPos = e.Location;
                panel2.Invalidate();
                return;
            }

            if (isMarqueeSelecting)
            {
                UpdateHoverTooltip(null, e.Location);
                marqueeCurrentModel = Point.Round(ScreenToModel(e.Location));
                ApplyMarqueeSelection();
                panel2.Invalidate();
                return;
            }

            if (isDragging && selectedElement != null)
            {
                UpdateHoverTooltip(null, e.Location);
                int dx = (int)((e.X - lastMousePosition.X) / scale);
                int dy = (int)((e.Y - lastMousePosition.Y) / scale);

                foreach (var target in GetDragTargets(selectedElement))
                {
                    if (target is GraphicNode gn)
                    {
                        gn.Location = new Point(gn.Location.X + dx, gn.Location.Y + dy);
                        UpdateShuntPositions(gn);
                    }
                    else if (target is GraphicBaseNode bn)
                    {
                        bn.Location = new Point(bn.Location.X + dx, bn.Location.Y + dy);
                        UpdateShuntPositions(bn);
                    }
                    else if (target is GraphicShunt gs)
                    {
                        gs.Location = new Point(gs.Location.X + dx, gs.Location.Y + dy);
                    }
                }

                lastMousePosition = e.Location;
                panel2.Invalidate();
            }

            if (!isDragging && !isMarqueeSelecting && !panning)
            {
                var hitForTooltip = FindElementAt(modelPoint);
                if (!ReferenceEquals(hitForTooltip, hoveredElement))
                {
                    UpdateHoverTooltip(hitForTooltip, e.Location);
                }
            }
        }

        private void ApplyMarqueeSelection()
        {
            var rect = GetMarqueeRectangle();
            ClearAllSelection();

            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                if (rect.IntersectsWith(new Rectangle(node.Location, GraphicNode.NodeSize))) SelectElement(node);
            }

            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                if (rect.IntersectsWith(new Rectangle(baseNode.Location, GraphicBaseNode.NodeSize))) SelectElement(baseNode);
            }

            foreach (var shunt in graphicShunts)
            {
                if (rect.IntersectsWith(new Rectangle(shunt.Location, GraphicShunt.ShuntSize))) SelectElement(shunt);
            }
        }

        private void UpdateShuntPositions(object movedNode)
        {
            foreach (var shunt in graphicShunts)
            {
                // Если шунт прикреплен к перемещаемому узлу - обновляем его позицию
                if (shunt.ConnectedNode == movedNode)
                {
                    shunt.UpdatePosition();
                }
            }
        }
        private void Panel2_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                panning = false;
                if (!rightMouseMoved)
                {
                    ShowContextMenu(e.Location);
                }
            }

            if (e.Button == MouseButtons.Left)
            {
                isMarqueeSelecting = false;
                if (isDraggingLegend)
                {
                    isDraggingLegend = false;
                    draggingLegendKey = null;
                    return;
                }
            }

            isDragging = false;
        }

        private void Panel2_DoubleClick(object sender, EventArgs e)
        {
            isDragging = false;
            if (selectedElement != null)
            {
                EditSelectedElement();
            }
        }
        private void EditBranch(GraphicBranch graphicBranch)
        {
            BranchForm form = new BranchForm();
            form.BindBranchModel(graphicBranch.Data);
            form.SetAvailableNodeNumbers(GetAllNodeNumbers());

            RegisterOpenedWindow(form);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = GetNextChildWindowLocation();
            form.FormClosed += (s, e) =>
            {
                if (form.DialogResult != DialogResult.OK) return;

                int newStartNode = form.MyBranch.StartNodeNumber;
                int newEndNode = form.MyBranch.EndNodeNumber;
                bool nodesChanged = (newStartNode != graphicBranch.GetStartNodeNumber()) || (newEndNode != graphicBranch.GetEndNodeNumber());

                if (nodesChanged)
                {
                    object newStartGraphicNode = FindNodeByNumber(newStartNode);
                    object newEndGraphicNode = FindNodeByNumber(newEndNode);
                    if (newStartGraphicNode == null || newEndGraphicNode == null || newStartGraphicNode == newEndGraphicNode)
                    {
                        MessageBox.Show("Ошибка изменения ветви: проверьте узлы.", "Ошибка");
                        return;
                    }

                    if (IsBranchAlreadyExists(newStartNode, newEndNode, graphicBranch))
                    {
                        MessageBox.Show("Ошибка: Ветвь между этими узлами уже существует!", "Ошибка");
                        return;
                    }

                    graphicBranch.StartNode = newStartGraphicNode;
                    graphicBranch.EndNode = newEndGraphicNode;
                }

                graphicBranch.Data.StartNodeNumber = form.MyBranch.StartNodeNumber;
                graphicBranch.Data.EndNodeNumber = form.MyBranch.EndNodeNumber;
                graphicBranch.Data.ActiveResistance = form.MyBranch.ActiveResistance;
                graphicBranch.Data.ReactiveResistance = form.MyBranch.ReactiveResistance;
                graphicBranch.Data.ReactiveConductivity = form.MyBranch.ReactiveConductivity;
                graphicBranch.Data.TransformationRatio = form.MyBranch.TransformationRatio;
                graphicBranch.Data.ActiveConductivity = form.MyBranch.ActiveConductivity;
                graphicBranch.Data.PermissibleCurrent = form.MyBranch.PermissibleCurrent;
                panel2.Invalidate();
            };
            form.Show(this);
        }

        private bool IsBranchAlreadyExists(int startNode, int endNode, GraphicBranch currentBranch)
        {
            foreach (var branch in graphicBranches)
            {
                // Пропускаем текущую редактируемую ветвь
                if (branch == currentBranch) continue;

                // Проверяем в обе стороны (ветвь может быть направленной или нет)
                bool sameDirection = (branch.Data.StartNodeNumber == startNode && branch.Data.EndNodeNumber == endNode);
                bool reverseDirection = (branch.Data.StartNodeNumber == endNode && branch.Data.EndNodeNumber == startNode);

                if (sameDirection || reverseDirection)
                {
                    return true;
                }
            }
            return false;
        }
        private void ShowContextMenu(Point location)
        {
            contextMenuStrip.Items.Clear();

            bool hasSelection = selectedElements.Count > 0;
            bool singleSelection = selectedElements.Count == 1;

            var editItem = new ToolStripMenuItem("Изменить параметры");
            editItem.Enabled = singleSelection;
            editItem.Click += (s, e) => EditSelectedElement();
            contextMenuStrip.Items.Add(editItem);

            var groupItem = new ToolStripMenuItem("Группировать");
            groupItem.Enabled = selectedElements.Count >= 2;
            groupItem.Click += (s, e) => GroupSelectedElements();
            contextMenuStrip.Items.Add(groupItem);

            var ungroupItem = new ToolStripMenuItem("Разгруппировать");
            ungroupItem.Enabled = hasSelection && selectedElements.Any(x => elementGroups.ContainsKey(x));
            ungroupItem.Click += (s, e) => UngroupSelectedElements();
            contextMenuStrip.Items.Add(ungroupItem);

            var deleteItem = new ToolStripMenuItem("Удалить");
            deleteItem.Enabled = hasSelection;
            deleteItem.Click += (s, e) => DeleteSelectionFromContext();
            contextMenuStrip.Items.Add(deleteItem);

            contextMenuStrip.Show(panel2, location);
        }

        private void DeleteSelectionFromContext()
        {
            var toDelete = selectedElements.ToList();
            if (toDelete.Count == 0)
            {
                return;
            }

            if (toDelete.Count == 1)
            {
                selectedElement = toDelete[0];
                DeleteSelectedElement();
                RefreshElementsGrid();
                return;
            }

            var dr = MessageBox.Show($"Удалить выбранные элементы ({toDelete.Count})?", "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            foreach (var element in toDelete)
            {
                selectedElement = element;
                if (element is GraphicNode || element is GraphicBaseNode)
                {
                    // Полное удаление вместе со связями
                    DeleteSelectedElement();
                }
                else
                {
                    DeleteSelectedElement();
                }
                elementGroups.Remove(element);
            }

            ClearAllSelection();
            RefreshElementsGrid();
            panel2.Invalidate();
        }

        private void EditSelectedElement()
        {
            if (selectedElement is GraphicNode graphicNode)
            {
                EditNode(graphicNode);
            }
            else if (selectedElement is GraphicBaseNode graphicBaseNode)
            {
                EditBaseNode(graphicBaseNode);
            }
            else if (selectedElement is GraphicBranch graphicBranch)
            {
                EditBranch(graphicBranch);
            }
            else if (selectedElement is GraphicShunt graphicShunt)
            {
                EditShunt(graphicShunt); // ЗАМЕНИЛ заглушку на реальный метод
            }
        }

        private void EditNode(GraphicNode graphicNode)
        {
            NodeForm form = new NodeForm(graphicNode.Data);
            RegisterOpenedWindow(form);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = GetNextChildWindowLocation();
            form.FormClosed += (s, e) =>
            {
                if (form.DialogResult != DialogResult.OK) return;

                if (form.MyNode.Number != graphicNode.Data.Number && IsNodeNumberExists(form.MyNode.Number))
                {
                    MessageBox.Show($"Ошибка: Узел с номером {form.MyNode.Number} уже существует!", "Ошибка");
                    return;
                }

                graphicNode.Data.Number = form.MyNode.Number;
                graphicNode.Data.InitialVoltage = form.MyNode.InitialVoltage;
                graphicNode.Data.NominalActivePower = form.MyNode.NominalActivePower;
                graphicNode.Data.NominalReactivePower = form.MyNode.NominalReactivePower;
                graphicNode.Data.ActivePowerGeneration = form.MyNode.ActivePowerGeneration;
                graphicNode.Data.ReactivePowerGeneration = form.MyNode.ReactivePowerGeneration;
                graphicNode.Data.FixedVoltageModule = form.MyNode.FixedVoltageModule;
                graphicNode.Data.MinReactivePower = form.MyNode.MinReactivePower;
                graphicNode.Data.MaxReactivePower = form.MyNode.MaxReactivePower;
                panel2.Invalidate();
            };
            form.Show(this);
        }

        private void EditBaseNode(GraphicBaseNode graphicBaseNode)
        {
            BaseNodeForm form = new BaseNodeForm();
            form.BindModel(graphicBaseNode.Data);

            RegisterOpenedWindow(form);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = GetNextChildWindowLocation();
            form.FormClosed += (s, e) =>
            {
                if (form.DialogResult != DialogResult.OK) return;
                if (form.MyBaseNode.Number != graphicBaseNode.Data.Number && IsNodeNumberExists(form.MyBaseNode.Number))
                {
                    MessageBox.Show($"Ошибка: Узел с номером {form.MyBaseNode.Number} уже существует!", "Ошибка");
                    return;
                }
                graphicBaseNode.Data.Number = form.MyBaseNode.Number;
                graphicBaseNode.Data.InitialVoltage = form.MyBaseNode.InitialVoltage;
                graphicBaseNode.Data.NominalActivePower = form.MyBaseNode.NominalActivePower;
                graphicBaseNode.Data.NominalReactivePower = form.MyBaseNode.NominalReactivePower;
                graphicBaseNode.Data.ActivePowerGeneration = form.MyBaseNode.ActivePowerGeneration;
                graphicBaseNode.Data.ReactivePowerGeneration = form.MyBaseNode.ReactivePowerGeneration;
                graphicBaseNode.Data.FixedVoltageModule = form.MyBaseNode.FixedVoltageModule;
                graphicBaseNode.Data.MinReactivePower = form.MyBaseNode.MinReactivePower;
                graphicBaseNode.Data.MaxReactivePower = form.MyBaseNode.MaxReactivePower;
                panel2.Invalidate();
            };
            form.Show(this);
        }

        private void DeleteSelectedElement()
        {
            if (selectedElement is GraphicBranch graphicBranch)
            {
                // Удаление ветви
                graphicBranches.Remove(graphicBranch);
                selectedElement = null;
                panel2.Invalidate();
            }
            else if (selectedElement is GraphicShunt graphicShunt)
            {
                // Удаление шунта
                graphicShunts.Remove(graphicShunt);
                selectedElement = null;
                panel2.Invalidate();
            }
            else if (selectedElement is GraphicNode graphicNode)
            {
                // Удаление узла и всех связанных элементов
                DeleteNodeWithConnections(graphicNode);
            }
            else if (selectedElement is GraphicBaseNode graphicBaseNode)
            {
                // Удаление базисного узла и всех связанных элементов
                DeleteNodeWithConnections(graphicBaseNode);
            }
        }

        private void DeleteNodeWithConnections(object node)
        {
            int nodeNumber = 0;
            string nodeType = "";

            // Определяем тип узла и его номер
            if (node is GraphicNode graphicNode)
            {
                nodeNumber = graphicNode.Data.Number;
                nodeType = "узла";
            }
            else if (node is GraphicBaseNode graphicBaseNode)
            {
                nodeNumber = graphicBaseNode.Data.Number;
                nodeType = "базисного узла";
            }

            // Спрашиваем подтверждение
            var result = MessageBox.Show(
                $"Удалить {nodeType} №{nodeNumber} и все связанные ветви и шунты?",
                "Подтверждение удаления",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                // Удаляем связанные ветви
                DeleteConnectedBranches(nodeNumber);

                // Удаляем связанные шунты
                DeleteConnectedShunts(nodeNumber);

                // Удаляем сам узел
                graphicElements.Remove(node as dynamic);

                selectedElement = null;
                panel2.Invalidate();

                MessageBox.Show($"{nodeType} №{nodeNumber} и все связанные элементы удалены!");
            }
        }

        private void DeleteConnectedBranches(int nodeNumber)
        {
            List<GraphicBranch> branchesToRemove = new List<GraphicBranch>();
            int removedCount = 0;

            foreach (var branch in graphicBranches)
            {
                if (branch.GetStartNodeNumber() == nodeNumber || branch.GetEndNodeNumber() == nodeNumber)
                {
                    branchesToRemove.Add(branch);
                    removedCount++;
                }
            }

            foreach (var branch in branchesToRemove)
            {
                graphicBranches.Remove(branch);
            }

            Console.WriteLine($"Удалено ветвей: {removedCount}");
        }

        private void DeleteConnectedShunts(int nodeNumber)
        {
            List<GraphicShunt> shuntsToRemove = new List<GraphicShunt>();
            int removedCount = 0;

            foreach (var shunt in graphicShunts)
            {
                if (shunt.GetConnectedNodeNumber() == nodeNumber) // ИСПРАВЛЕНО
                {
                    shuntsToRemove.Add(shunt);
                    removedCount++;
                    Console.WriteLine($"Удаляем шунт на узле {shunt.GetConnectedNodeNumber()}");
                }
            }

            // Удаляем найденные шунты
            foreach (var shunt in shuntsToRemove)
            {
                graphicShunts.Remove(shunt);
            }

            Console.WriteLine($"Удалено шунтов: {removedCount}");
        }

        private void buttonAddNode_Click(object sender, EventArgs e)
        {
            Node newNodeData = new Node(0);
            NodeForm nodeForm = new NodeForm(newNodeData);

            if (ShowEditorForm(nodeForm) == DialogResult.OK && nodeForm.MyNode.Number != 0)
            {
                // ПРОВЕРКА УНИКАЛЬНОСТИ НОМЕРА
                if (IsNodeNumberExists(nodeForm.MyNode.Number))
                {
                    MessageBox.Show($"Ошибка: Узел с номером {nodeForm.MyNode.Number} уже существует!\nПожалуйста, выберите другой номер.", "Ошибка");
                    return;
                }

                Point center = new Point(
                    panel2.Width / 2 - GraphicNode.NodeSize.Width / 2,
                    panel2.Height / 2 - GraphicNode.NodeSize.Height / 2
                );

                GraphicNode newGraphicNode = new GraphicNode(nodeForm.MyNode, center);
                graphicElements.Add(newGraphicNode);
                panel2.Invalidate();

                MessageBox.Show($"Узел №{nodeForm.MyNode.Number} добавлен!");
                RefreshElementsGrid();
            }
        }

        private void buttonAddBaseNode_Click(object sender, EventArgs e)
        {
            Console.WriteLine("=== НАЧАЛО ДОБАВЛЕНИЯ БАЗИСНОГО УЗЛА ===");

            BaseNodeForm baseNodeForm = new BaseNodeForm();
            Console.WriteLine("Форма базисного узла создана");

            DialogResult result = ShowEditorForm(baseNodeForm);
            Console.WriteLine($"Результат диалога: {result}");

            if (result == DialogResult.OK)
            {
                Console.WriteLine("DialogResult.OK получен");
                Console.WriteLine($"Номер узла: {baseNodeForm.MyBaseNode?.Number}");

                if (baseNodeForm.MyBaseNode != null && baseNodeForm.MyBaseNode.Number != 0)
                {
                    // ПРОВЕРКА УНИКАЛЬНОСТИ НОМЕРА
                    if (IsNodeNumberExists(baseNodeForm.MyBaseNode.Number))
                    {
                        MessageBox.Show($"Ошибка: Узел с номером {baseNodeForm.MyBaseNode.Number} уже существует!\nПожалуйста, выберите другой номер.", "Ошибка");
                        return;
                    }

                    Console.WriteLine("Условие пройдено - добавляем узел");

                    Point center = new Point(
                        panel2.Width / 2 - GraphicBaseNode.NodeSize.Width / 2,
                        panel2.Height / 2 - GraphicBaseNode.NodeSize.Height / 2
                    );

                    GraphicBaseNode newGraphicBaseNode = new GraphicBaseNode(baseNodeForm.MyBaseNode, center);
                    graphicElements.Add(newGraphicBaseNode);

                    panel2.Invalidate();
                    MessageBox.Show($"Базисный узел №{baseNodeForm.MyBaseNode.Number} добавлен!");
                RefreshElementsGrid();
                }
            }
        }
        private void buttonDelete_Click(object sender, EventArgs e)
        {
            if (selectedElement != null)
            {
                DeleteSelectedElement();
                RefreshElementsGrid();
            }
            else
            {
                MessageBox.Show("Выберите элемент для удаления");
            }
        }

        private void buttonClearAll_Click(object sender, EventArgs e)
        {
            bool anything = graphicElements.Count > 0 ||
                            graphicBranches.Count > 0 ||
                            graphicShunts.Count > 0;
            if (!anything) return;

            var dr = MessageBox.Show("Очистить всю схему?",
                                    "Подтверждение",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question);
            if (dr == DialogResult.Yes)
            {
                graphicElements.Clear();
                graphicBranches.Clear();   // теперь тоже чистим
                graphicShunts.Clear();
                selectedElement = null;
                panel2.Invalidate();
            }
        }

        private void buttonViewAll_Click(object sender, EventArgs e)
        {
            if (graphicElements.Count == 0 && graphicBranches.Count == 0 && graphicShunts.Count == 0)
            {
                MessageBox.Show("Нет элементов на схеме");
                return;
            }

            string result = "Элементы на схеме:\n\n";

            // Узлы
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node)
                {
                    result += $"Узел №{node.Data.Number} (позиция: {node.Location})\n";
                }
                else if (element is GraphicBaseNode baseNode)
                {
                    result += $"Базисный узел №{baseNode.Data.Number} (позиция: {baseNode.Location})\n";
                }
            }

            // Ветви
            foreach (var branch in graphicBranches)
            {
                result += $"Ветвь: {branch.Data.StartNodeNumber} -> {branch.Data.EndNodeNumber} (R={branch.Data.ActiveResistance:F1})\n";
            }

            // Шунты
            foreach (var shunt in graphicShunts)
            {
                result += $"Шунт на узле {shunt.Data.StartNodeNumber} (X={shunt.Data.ReactiveResistance})\n";
            }

            MessageBox.Show(result);
        }

        // Вспомогательный метод для обратной совместимости
        private List<GraphicNode> GetGraphicNodes()
        {
            var nodes = new List<GraphicNode>();
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node)
                    nodes.Add(node);
            }
            return nodes;
        }



        private object FindNodeByNumber(int nodeNumber)
        {
            // Ищем в обычных узлах
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node && node.Data.Number == nodeNumber)
                    return node;
                else if (element is GraphicBaseNode baseNode && baseNode.Data.Number == nodeNumber)
                    return baseNode;
            }
            return null;
        }


        private void buttonAddShunt_Click(object sender, EventArgs e)
        {
            // Проверяем, что есть хотя бы 1 узел (любого типа)
            if (graphicElements.Count == 0)
            {
                MessageBox.Show("Для создания шунта нужен хотя бы 1 узел на схеме", "Ошибка");
                return;
            }

            ShuntForm shuntForm = new ShuntForm();

            if (ShowEditorForm(shuntForm) == DialogResult.OK && shuntForm.MyShunt.StartNodeNumber != 0)
            {
                // Ищем узел по номеру (любого типа)
                object connectedNode = FindNodeByNumber(shuntForm.MyShunt.StartNodeNumber);

                if (connectedNode == null)
                {
                    MessageBox.Show($"Узел с номером {shuntForm.MyShunt.StartNodeNumber} не найден на схеме!", "Ошибка");
                    return;
                }

                // Проверяем, нет ли уже шунта на этом узле
                if (IsShuntAlreadyExists(shuntForm.MyShunt.StartNodeNumber))
                {
                    MessageBox.Show("На этом узле уже есть шунт!", "Ошибка");
                    return;
                }

                // Создаем и добавляем шунт
                GraphicShunt newGraphicShunt = new GraphicShunt(shuntForm.MyShunt, connectedNode);
                graphicShunts.Add(newGraphicShunt);

                panel2.Invalidate();

                string nodeType = (connectedNode is GraphicBaseNode) ? "базисный узел" : "узел";
                MessageBox.Show($"Шунт добавлен к {nodeType} №{shuntForm.MyShunt.StartNodeNumber}!");
                RefreshElementsGrid();
            }
        }

        private bool IsShuntAlreadyExists(int nodeNumber)
        {
            foreach (var shunt in graphicShunts)
            {
                if (shunt.GetConnectedNodeNumber() == nodeNumber)
                    return true;
            }
            return false;
        }

        private void buttonAddBranch_Click(object sender, EventArgs e)
        {
            // Сначала проверяем, что есть хотя бы 2 узла (любого типа)
            if (graphicElements.Count < 2)
            {
                MessageBox.Show("Для создания ветви нужно как минимум 2 узла на схеме", "Ошибка");
                return;
            }

            BranchForm branchForm = new BranchForm();
            branchForm.SetAvailableNodeNumbers(GetAllNodeNumbers());

            if (ShowEditorForm(branchForm) == DialogResult.OK &&
                branchForm.MyBranch.StartNodeNumber != 0 &&
                branchForm.MyBranch.EndNodeNumber != 0)
            {
                // Ищем узлы по номерам (любого типа)
                object startNode = FindNodeByNumber(branchForm.MyBranch.StartNodeNumber);
                object endNode = FindNodeByNumber(branchForm.MyBranch.EndNodeNumber);

                if (startNode == null)
                {
                    MessageBox.Show($"Ошибка: Начальный узел №{branchForm.MyBranch.StartNodeNumber} не найден на схеме!", "Ошибка");
                    return;
                }

                if (endNode == null)
                {
                    MessageBox.Show($"Ошибка: Конечный узел №{branchForm.MyBranch.EndNodeNumber} не найден на схеме!", "Ошибка");
                    return;
                }

                if (startNode == endNode)
                {
                    MessageBox.Show("Ошибка: Начальный и конечный узлы не могут быть одинаковыми!", "Ошибка");
                    return;
                }

                // Проверяем, не существует ли уже такая ветвь
                if (IsBranchAlreadyExists(branchForm.MyBranch.StartNodeNumber, branchForm.MyBranch.EndNodeNumber, null))
                {
                    MessageBox.Show("Ошибка: Ветвь между этими узлами уже существует!", "Ошибка");
                    return;
                }

                // Создаем и добавляем ветвь
                GraphicBranch newGraphicBranch = new GraphicBranch(branchForm.MyBranch, startNode, endNode);
                graphicBranches.Add(newGraphicBranch);

                panel2.Invalidate();

                string startType = (startNode is GraphicBaseNode) ? "базисный узел" : "узел";
                string endType = (endNode is GraphicBaseNode) ? "базисный узел" : "узел";

                MessageBox.Show($"Ветвь между {startType} №{branchForm.MyBranch.StartNodeNumber} и {endType} №{branchForm.MyBranch.EndNodeNumber} добавлена!");
                RefreshElementsGrid();
            }
        }

        private void EditShunt(GraphicShunt graphicShunt)
        {
            ShuntForm form = new ShuntForm();
            form.BindModel(graphicShunt.Data);

            RegisterOpenedWindow(form);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = GetNextChildWindowLocation();
            form.FormClosed += (s, e) =>
            {
                if (form.DialogResult != DialogResult.OK) return;

                int newNodeNumber = form.MyShunt.StartNodeNumber;
                if (newNodeNumber != graphicShunt.Data.StartNodeNumber)
                {
                    object newConnectedNode = FindNodeByNumber(newNodeNumber);
                    if (newConnectedNode == null || IsShuntAlreadyExists(newNodeNumber))
                    {
                        MessageBox.Show("Ошибка изменения шунта: проверьте узел.", "Ошибка");
                        return;
                    }
                    graphicShunt.ConnectedNode = newConnectedNode;
                }

                graphicShunt.Data.StartNodeNumber = form.MyShunt.StartNodeNumber;
                graphicShunt.Data.ActiveResistance = form.MyShunt.ActiveResistance;
                graphicShunt.Data.ReactiveResistance = form.MyShunt.ReactiveResistance;
                graphicShunt.UpdatePosition();
                panel2.Invalidate();
            };
            form.Show(this);
        }


        private void RefreshOpenedEditorForms()
        {
            foreach (var list in openedEditorWindows.Values)
            {
                foreach (var form in list.ToList())
                {
                    if (form == null || form.IsDisposed) continue;

                    if (form is NodeForm nodeForm) nodeForm.RefreshFromModel();
                    else if (form is BaseNodeForm baseNodeForm) baseNodeForm.RefreshFromModel();
                    else if (form is BranchForm branchForm) branchForm.RefreshFromModel();
                    else if (form is ShuntForm shuntForm) shuntForm.RefreshFromModel();
                }
            }
        }

        // Метод проверки существования узла с таким номером
        private bool IsNodeNumberExists(int nodeNumber)
        {
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node && node.Data.Number == nodeNumber)
                    return true;
                else if (element is GraphicBaseNode baseNode && baseNode.Data.Number == nodeNumber)
                    return true;
            }
            return false;
        }

        // Метод получения списка всех существующих номеров узлов
        private List<int> GetAllNodeNumbers()
        {
            List<int> numbers = new List<int>();
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node)
                    numbers.Add(node.Data.Number);
                else if (element is GraphicBaseNode baseNode)
                    numbers.Add(baseNode.Data.Number);
            }
            return numbers;
        }




        private void buttonExportData_Click(object sender, EventArgs e)
        {
            try
            {
                using (var saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
                    saveFileDialog.Title = "Сохранить экспорт данных";
                    saveFileDialog.FileName = "Начальные данные.txt";
                    if (saveFileDialog.ShowDialog() != DialogResult.OK) return;
                    string filePath = saveFileDialog.FileName;

                    using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                    {
                        // 1. Все узлы (0201 0)
                        WriteAllNodes(writer);

                        // 2. Базисные узлы (0102 0)
                        WriteBaseNodesOnly(writer);

                        // 3. Все ветви и шунты (0301 0)
                        WriteAllBranchesAndShunts(writer);

                        // 4. Координаты элементов (0901 0)
                        WriteLayout(writer);

                        // 5. Расширенное состояние (легенды, телеметрия и инкременты)
                        WriteExtendedState(writer);
                    }

                    MessageBox.Show($"Файл успешно создан!\nРасположение: {filePath}", "Экспорт завершен");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании файла: {ex.Message}", "Ошибка");
            }
        }

        private void WriteLayout(StreamWriter writer)
        {
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node)
                {
                    writer.WriteLine($"0901 0 {node.Data.Number} {node.Location.X} {node.Location.Y}");
                }
                else if (element is GraphicBaseNode baseNode)
                {
                    writer.WriteLine($"0901 0 {baseNode.Data.Number} {baseNode.Location.X} {baseNode.Location.Y}");
                }
            }
        }

        private void WriteExtendedState(StreamWriter writer)
        {
            writer.WriteLine($"0991 0 {Convert.ToInt32(legendsLocked)} {branchLegendOffset.X} {branchLegendOffset.Y} {voltageLegendOffset.X} {voltageLegendOffset.Y}");

            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                writer.WriteLine($"0992 0 {node.Data.Number} {BuildExtendedDataPayload(node.Data, isNode: true)}");
            }

            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                writer.WriteLine($"0995 0 {baseNode.Data.Number} {BuildExtendedDataPayload(baseNode.Data, isNode: false)}");
            }

            foreach (var branch in graphicBranches)
            {
                writer.WriteLine($"0993 0 {branch.Data.StartNodeNumber} {branch.Data.EndNodeNumber} {BuildExtendedDataPayload(branch.Data, isNode: false)}");
            }

            foreach (var shunt in graphicShunts)
            {
                writer.WriteLine($"0994 0 {shunt.Data.StartNodeNumber} {BuildExtendedDataPayload(shunt.Data, isNode: false)}");
            }
        }

        private void WriteAllNodes(StreamWriter writer)
        {
            var baseNodeNumbers = new HashSet<int>(graphicElements
                .OfType<GraphicBaseNode>()
                .Select(x => x.Data.Number));

            var allNodes = new List<(int number, double voltage, double pLoad, double qLoad, double pGen, double qGen, double uFixed, double qMin, double qMax)>();

            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                if (baseNodeNumbers.Contains(node.Data.Number))
                {
                    continue;
                }

                allNodes.Add((
                    node.Data.Number,
                    node.Data.InitialVoltage,
                    node.Data.NominalActivePower,
                    node.Data.NominalReactivePower,
                    node.Data.ActivePowerGeneration,
                    node.Data.ReactivePowerGeneration,
                    node.Data.FixedVoltageModule,
                    node.Data.MinReactivePower,
                    node.Data.MaxReactivePower
                ));
            }

            foreach (var node in allNodes)
            {
                string line = $"0201 0   {node.number,3}  {node.voltage,3}     " +
                             $"{FormatInt(node.pLoad),4}  {FormatInt(node.qLoad),3}  " +
                             $"{FormatInt(node.pGen),1} {FormatInt(node.qGen),1}  " +
                             $"{FormatInt(node.uFixed),3} {FormatInt(node.qMin),1} {FormatInt(node.qMax),1}";

                writer.WriteLine(line);
            }
        }

        private void WriteBaseNodesOnly(StreamWriter writer)
        {
            foreach (var element in graphicElements)
            {
                if (element is GraphicBaseNode baseNode)
                {
                    string line = $"0102 0   {baseNode.Data.Number,3}  {baseNode.Data.InitialVoltage,3}       " +
                                 $"{FormatInt(baseNode.Data.NominalActivePower),1}    " +
                                 $"{FormatInt(baseNode.Data.NominalReactivePower),1}  " +
                                 $"{FormatInt(baseNode.Data.ActivePowerGeneration),1} " +
                                 $"{FormatInt(baseNode.Data.ReactivePowerGeneration),1}   " +
                                 $"{FormatInt(baseNode.Data.FixedVoltageModule),1} " +
                                 $"{FormatInt(baseNode.Data.MinReactivePower),1} " +
                                 $"{FormatInt(baseNode.Data.MaxReactivePower),1}";

                    writer.WriteLine(line);
                }
            }
        }

        private void WriteAllBranchesAndShunts(StreamWriter writer)
        {
            // Сначала шунты (как в исходных CDU), затем ветви.
            // Это важно для совместимости с ConsoleApplicationCS,
            // где при чтении шунтов используется временный индекс ветви.
            foreach (var shunt in graphicShunts)
            {
                string shuntLine = $"0301 0   {shunt.Data.StartNodeNumber,3}      {shunt.Data.EndNodeNumber,2}    " +
                                   $"{FormatDouble(shunt.Data.ActiveResistance),4}   " +
                                   $"{FormatDouble(shunt.Data.ReactiveResistance),5}";
                writer.WriteLine(shuntLine);
            }

            foreach (var branch in graphicBranches)
            {
                string branchLine = $"0301 0   {branch.Data.StartNodeNumber,3}      {branch.Data.EndNodeNumber,2}    " +
                                    $"{FormatDouble(branch.Data.ActiveResistance),4}   " +
                                    $"{FormatDouble(branch.Data.ReactiveResistance),5}   " +
                                    $"{FormatDouble(branch.Data.ReactiveConductivity, true),6}     " +
                                    $"{FormatDouble(branch.Data.TransformationRatio),5} " +
                                    $"{FormatInt(branch.Data.ActiveConductivity),1} 0 0 {FormatDouble(branch.Data.PermissibleCurrent)}";
                writer.WriteLine(branchLine);
            }
        }

        // Методы форматирования с ТОЧКАМИ вместо запятых
        private string FormatInt(double number)
        {
            if (number == 0) return "0";
            return ((int)number).ToString();
        }

        private string FormatDouble(double number, bool isConductivity = false)
        {
            if (number == 0) return "0";

            if (isConductivity && number < 0)
            {
                // Для отрицательных проводимостей
                return $"-{Math.Abs(number).ToString("F1", CultureInfo.InvariantCulture)}";
            }

            // Используем инвариантную культуру для точек вместо запятых
            string formatted = number.ToString("F2", CultureInfo.InvariantCulture);

            // Убираем ведущий ноль для формата ".10"
            if (formatted.StartsWith("0."))
            {
                formatted = "." + formatted.Substring(2);
            }

            return formatted;
        }

        // Вспомогательные методы для форматирования чисел
        private string FormatNumber(double number)
        {
            if (number == 0) return "0";
            return number.ToString("F0");
        }

        private string FormatBranchNumber(double number, bool isConductivity = false)
        {
            if (number == 0) return "0";

            if (isConductivity && number < 0)
            {
                return $"-{Math.Abs(number):F1}";
            }

            // Для формата как в примере: ".10" вместо "0.10"
            string formatted = number.ToString("F2").TrimStart('0');
            if (formatted.StartsWith(".")) formatted = " " + formatted;

            return formatted;
        }

        // Метод для записи базисных узлов
        private void WriteBaseNodes(StreamWriter writer)
        {
            writer.WriteLine("БАЗИСНЫЕ УЗЛЫ:");
            writer.WriteLine("0102 0   Номер  Напряж  Pнагр  Qнагр  Pген  Qген  Uфикс  Qmin  Qmax");

            foreach (var element in graphicElements)
            {
                if (element is GraphicBaseNode baseNode)
                {
                    string line = $"0102 0   {baseNode.Data.Number,4}  " +
                                 $"{baseNode.Data.InitialVoltage,6:F2}    " +
                                 $"{baseNode.Data.NominalActivePower,4}   " +
                                 $"{baseNode.Data.NominalReactivePower,4}  " +
                                 $"{baseNode.Data.ActivePowerGeneration,4}  " +
                                 $"{baseNode.Data.ReactivePowerGeneration,4}   " +
                                 $"{baseNode.Data.FixedVoltageModule,4}  " +
                                 $"{baseNode.Data.MinReactivePower,4}  " +
                                 $"{baseNode.Data.MaxReactivePower,4}";

                    writer.WriteLine(line);
                }
            }
            writer.WriteLine();
        }

        // Метод для записи обычных узлов
        private void WriteNodes(StreamWriter writer)
        {
            writer.WriteLine("ОБЫЧНЫЕ УЗЛЫ:");
            writer.WriteLine("0102 0   Номер  Напряж  Pнагр  Qнагр  Pген  Qген  Uфикс  Qmin  Qmax");

            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node)
                {
                    string line = $"0102 0   {node.Data.Number,4}  " +
                                 $"{node.Data.InitialVoltage,6:F2}    " +
                                 $"{node.Data.NominalActivePower,4}   " +
                                 $"{node.Data.NominalReactivePower,4}  " +
                                 $"{node.Data.ActivePowerGeneration,4}  " +
                                 $"{node.Data.ReactivePowerGeneration,4}   " +
                                 $"{node.Data.FixedVoltageModule,4}  " +
                                 $"{node.Data.MinReactivePower,4}  " +
                                 $"{node.Data.MaxReactivePower,4}";

                    writer.WriteLine(line);
                }
            }
            writer.WriteLine();
        }

        // Метод для записи ветвей
        private void WriteBranches(StreamWriter writer)
        {
            writer.WriteLine("ВЕТВИ:");
            writer.WriteLine("0301 0  НачУз  КонУз   Rакт    Xреакт     Bреакт    Kтрансф  Gакт   Iдоп");

            foreach (var branch in graphicBranches)
            {
                string line = $"0301 0  {branch.Data.StartNodeNumber,4}   " +
                             $"{branch.Data.EndNodeNumber,4}   " +
                             $"{branch.Data.ActiveResistance,5:F1}   " +
                             $"{branch.Data.ReactiveResistance,7:F2}   " +
                             $"{branch.Data.ReactiveConductivity,8:F1}   " +
                             $"{branch.Data.TransformationRatio,4}   " +
                             $"{branch.Data.ActiveConductivity,4}   " +
                             $"{branch.Data.PermissibleCurrent,6:F1}";

                writer.WriteLine(line);
            }
            writer.WriteLine();
        }

        // Метод для записи шунтов
        private void WriteShunts(StreamWriter writer)
        {
            writer.WriteLine("ШУНТЫ:");
            writer.WriteLine("0301 0  НачУз  КонУз   Rакт   Xреакт");

            foreach (var shunt in graphicShunts)
            {
                string line = $"0301 0  {shunt.Data.StartNodeNumber,4}   " +
                             $"{shunt.Data.EndNodeNumber,4}   " +
                             $"{shunt.Data.ActiveResistance,5:F1}   " +
                             $"{shunt.Data.ReactiveResistance,4}";

                writer.WriteLine(line);
            }
            writer.WriteLine();
        }

        // Вспомогательные методы для статистики
        private int CountNodes()
        {
            int count = 0;
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode) count++;
            }
            return count;
        }

        private int CountBaseNodes()
        {
            int count = 0;
            foreach (var element in graphicElements)
            {
                if (element is GraphicBaseNode) count++;
            }
            return count;
        }

        // Добавь using в начало файла


        private void buttonImportData_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
                openFileDialog.Title = "Выберите файл с начальными данными";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    ImportDataFromFile(openFileDialog.FileName);
                    MessageBox.Show("Данные успешно импортированы!", "Импорт завершен");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при импорте файла: {ex.Message}", "Ошибка");
            }
        }

        private void ImportDataFromFile(string filePath)
        {
            graphicElements.Clear();
            graphicBranches.Clear();
            graphicShunts.Clear();

            string[] lines = File.ReadAllLines(filePath);

            int nodeIndex = 0;   // счётчик для узлов (спираль)
            var savedLayout = new Dictionary<int, Point>();

            foreach (string rawLine in lines)
            {
                string ln = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(ln)) continue;

                string[] parts = ln.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                string code = parts[0] + " " + parts[1];

                switch (code)
                {
                    case "0201 0":                // обычный узел
                        ParseNodeLine(parts, false, nodeIndex++);
                        break;

                    case "0102 0":                // базисный узел
                        ParseNodeLine(parts, true, nodeIndex++);
                        break;

                    case "0301 0":                // ветвь или шунт
                        int endNode = int.Parse(parts[3]);
                        if (endNode == 0)         // шунт
                            ParseShuntLine(parts); // нет второго аргумента
                        else                      // ветвь
                            ParseBranchLine(parts); // нет второго аргумента
                        break;

                    case "0901 0":                // координаты узла
                        ParseLayoutLine(parts, savedLayout);
                        break;

                    case "0991 0":
                        ParseLegendStateLine(parts);
                        break;

                    case "0992 0":
                        ParseNodeExtendedStateLine(parts, false);
                        break;

                    case "0995 0":
                        ParseNodeExtendedStateLine(parts, true);
                        break;

                    case "0993 0":
                        ParseBranchExtendedStateLine(parts);
                        break;

                    case "0994 0":
                        ParseShuntExtendedStateLine(parts);
                        break;
                }
            }

            ApplySavedLayout(savedLayout);
            panel2.Invalidate();
            RefreshElementsGrid();
            RefreshOpenedReportForms();
        }
        private void ParseNodeLine(string[] parts, bool isBaseNode, int index)
        {
            try
            {
                int number = int.Parse(parts[2]);
                double voltage = ParseDouble(parts[3]);
                double pLoad = ParseDouble(parts[4]);
                double qLoad = ParseDouble(parts[5]);
                double pGen = ParseDouble(parts[6]);
                double qGen = ParseDouble(parts[7]);
                double uFixed = ParseDouble(parts[8]);
                double qMin = ParseDouble(parts[9]);
                double qMax = ParseDouble(parts[10]);

                Point pos = SpiralPosition(index, 90);   // 90 px шаг

                for (int i = graphicElements.Count - 1; i >= 0; i--)
                {
                    var existingNode = graphicElements[i] as GraphicNode;
                    if (existingNode != null && existingNode.Data.Number == number)
                    {
                        if (!isBaseNode)
                        {
                            return;
                        }

                        graphicElements.RemoveAt(i);
                        continue;
                    }

                    var existingBaseNode = graphicElements[i] as GraphicBaseNode;
                    if (existingBaseNode != null && existingBaseNode.Data.Number == number)
                    {
                        if (!isBaseNode)
                        {
                            return;
                        }

                        graphicElements.RemoveAt(i);
                    }
                }

                if (isBaseNode)
                {
                    var bn = new BaseNode(number)
                    {
                        InitialVoltage = voltage,
                        NominalActivePower = pLoad,
                        NominalReactivePower = qLoad,
                        ActivePowerGeneration = pGen,
                        ReactivePowerGeneration = qGen,
                        FixedVoltageModule = uFixed,
                        MinReactivePower = qMin,
                        MaxReactivePower = qMax
                    };
                    graphicElements.Add(new GraphicBaseNode(bn, pos));
                }
                else
                {
                    var nd = new Node(number)
                    {
                        InitialVoltage = voltage,
                        NominalActivePower = pLoad,
                        NominalReactivePower = qLoad,
                        ActivePowerGeneration = pGen,
                        ReactivePowerGeneration = qGen,
                        FixedVoltageModule = uFixed,
                        MinReactivePower = qMin,
                        MaxReactivePower = qMax
                    };
                    graphicElements.Add(new GraphicNode(nd, pos));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка парсинга узла: " + ex.Message);
            }
        }

        private void ParseShuntLine(string[] parts)
        {
            try
            {
                int nodeNum = int.Parse(parts[2]);
                double r = ParseDouble(parts[4]);
                double x = ParseDouble(parts[5]);

                object hostNode = FindNodeByNumber(nodeNum);
                if (hostNode == null) return;

                var shunt = new Shunt(nodeNum)
                {
                    ActiveResistance = r,
                    ReactiveResistance = x
                };
                graphicShunts.Add(new GraphicShunt(shunt, hostNode));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка парсинга шунта: " + ex.Message);
            }
        }
        private void ParseLayoutLine(string[] parts, Dictionary<int, Point> savedLayout)
        {
            if (parts.Length < 5) return;

            try
            {
                int number = int.Parse(parts[2]);
                int x = int.Parse(parts[3]);
                int y = int.Parse(parts[4]);
                savedLayout[number] = new Point(x, y);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка парсинга координат: " + ex.Message);
            }
        }

        private void ApplySavedLayout(Dictionary<int, Point> savedLayout)
        {
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node && savedLayout.TryGetValue(node.Data.Number, out Point nodePoint))
                {
                    node.Location = nodePoint;
                }
                else if (element is GraphicBaseNode baseNode && savedLayout.TryGetValue(baseNode.Data.Number, out Point basePoint))
                {
                    baseNode.Location = basePoint;
                }
            }

            foreach (var shunt in graphicShunts)
            {
                shunt.UpdatePosition();
            }
        }

        private string BuildExtendedDataPayload(dynamic data, bool isNode)
        {
            string deviceId = isNode ? Convert.ToString(data.NodeID) : Convert.ToString(data.DeviceID);
            string body = string.Join("|", new[]
            {
                Convert.ToString(data.IPAddress),
                Convert.ToString(data.Port),
                deviceId,
                Convert.ToString(data.Protocol),
                Convert.ToString(data.MeasurementIntervalSeconds, CultureInfo.InvariantCulture),
                SerializeDictionary((Dictionary<string, string>)data.ParamRegisters, v => v),
                SerializeDictionary((Dictionary<string, bool>)data.ParamAutoModes, v => v ? "1" : "0"),
                SerializeDictionary((Dictionary<string, double>)data.ParamIncrementSteps, v => v.ToString("R", CultureInfo.InvariantCulture)),
                SerializeDictionary((Dictionary<string, int>)data.ParamIncrementIntervals, v => v.ToString(CultureInfo.InvariantCulture))
            });

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(body));
        }

        private string SerializeDictionary<T>(Dictionary<string, T> dict, Func<T, string> valueSelector)
        {
            if (dict == null || dict.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",", dict.Select(kv =>
                $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Key))}:{Convert.ToBase64String(Encoding.UTF8.GetBytes(valueSelector(kv.Value) ?? string.Empty))}"));
        }

        private Dictionary<string, string> DeserializeDictionaryString(string payload)
        {
            var result = new Dictionary<string, string>();
            foreach (var item in SplitSerializedDictionary(payload))
            {
                result[item.Key] = item.Value;
            }
            return result;
        }

        private Dictionary<string, bool> DeserializeDictionaryBool(string payload)
        {
            var result = new Dictionary<string, bool>();
            foreach (var item in SplitSerializedDictionary(payload))
            {
                result[item.Key] = item.Value == "1" || item.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            return result;
        }

        private Dictionary<string, double> DeserializeDictionaryDouble(string payload)
        {
            var result = new Dictionary<string, double>();
            foreach (var item in SplitSerializedDictionary(payload))
            {
                if (double.TryParse(item.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    result[item.Key] = parsed;
                }
            }
            return result;
        }

        private Dictionary<string, int> DeserializeDictionaryInt(string payload)
        {
            var result = new Dictionary<string, int>();
            foreach (var item in SplitSerializedDictionary(payload))
            {
                if (int.TryParse(item.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    result[item.Key] = parsed;
                }
            }
            return result;
        }

        private IEnumerable<KeyValuePair<string, string>> SplitSerializedDictionary(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                yield break;
            }

            foreach (var token in payload.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = token.Split(':');
                if (parts.Length != 2)
                {
                    continue;
                }

                string key = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                string value = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
                yield return new KeyValuePair<string, string>(key, value);
            }
        }

        private void ApplyExtendedStatePayload(dynamic data, string payload, bool isNode)
        {
            if (data == null || string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            try
            {
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                string[] parts = decoded.Split('|');
                if (parts.Length < 9)
                {
                    return;
                }

                data.IPAddress = parts[0];
                data.Port = parts[1];
                if (isNode)
                {
                    data.NodeID = parts[2];
                }
                else
                {
                    data.DeviceID = parts[2];
                }

                data.Protocol = parts[3];
                if (int.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval))
                {
                    data.MeasurementIntervalSeconds = interval;
                }

                MergeDictionary((Dictionary<string, string>)data.ParamRegisters, DeserializeDictionaryString(parts[5]));
                MergeDictionary((Dictionary<string, bool>)data.ParamAutoModes, DeserializeDictionaryBool(parts[6]));
                MergeDictionary((Dictionary<string, double>)data.ParamIncrementSteps, DeserializeDictionaryDouble(parts[7]));
                MergeDictionary((Dictionary<string, int>)data.ParamIncrementIntervals, DeserializeDictionaryInt(parts[8]));
            }
            catch
            {
            }
        }

        private void MergeDictionary<T>(Dictionary<string, T> target, Dictionary<string, T> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            foreach (var kv in source)
            {
                target[kv.Key] = kv.Value;
            }
        }

        private void ParseLegendStateLine(string[] parts)
        {
            if (parts.Length < 7)
            {
                return;
            }

            if (int.TryParse(parts[2], out var locked))
            {
                legendsLocked = locked != 0;
                if (checkBoxLockLegends != null)
                {
                    checkBoxLockLegends.Checked = legendsLocked;
                }
            }

            if (int.TryParse(parts[3], out var branchX) && int.TryParse(parts[4], out var branchY))
            {
                branchLegendOffset = new Point(branchX, branchY);
            }

            if (int.TryParse(parts[5], out var voltageX) && int.TryParse(parts[6], out var voltageY))
            {
                voltageLegendOffset = new Point(voltageX, voltageY);
            }
        }

        private void ParseNodeExtendedStateLine(string[] parts, bool isBaseNode)
        {
            if (parts.Length < 4 || !int.TryParse(parts[2], out var number))
            {
                return;
            }

            string payload = parts[3];
            if (isBaseNode)
            {
                var baseNode = graphicElements.OfType<GraphicBaseNode>().FirstOrDefault(x => x.Data.Number == number);
                if (baseNode != null)
                {
                    ApplyExtendedStatePayload(baseNode.Data, payload, false);
                }

                return;
            }

            var node = graphicElements.OfType<GraphicNode>().FirstOrDefault(x => x.Data.Number == number);
            if (node != null)
            {
                ApplyExtendedStatePayload(node.Data, payload, true);
            }
        }

        private void ParseBranchExtendedStateLine(string[] parts)
        {
            if (parts.Length < 5 || !int.TryParse(parts[2], out var startNode) || !int.TryParse(parts[3], out var endNode))
            {
                return;
            }

            var branch = graphicBranches.FirstOrDefault(x => x.Data.StartNodeNumber == startNode && x.Data.EndNodeNumber == endNode);
            if (branch != null)
            {
                ApplyExtendedStatePayload(branch.Data, parts[4], false);
            }
        }

        private void ParseShuntExtendedStateLine(string[] parts)
        {
            if (parts.Length < 4 || !int.TryParse(parts[2], out var startNode))
            {
                return;
            }

            var shunt = graphicShunts.FirstOrDefault(x => x.Data.StartNodeNumber == startNode);
            if (shunt != null)
            {
                ApplyExtendedStatePayload(shunt.Data, parts[3], false);
            }
        }

        private void ParseBranchLine(string[] parts)
        {
            try
            {
                int startNode = int.Parse(parts[2]);
                int endNode = int.Parse(parts[3]);
                double r = ParseDouble(parts[4]);
                double x = ParseDouble(parts[5]);
                double b = ParseDouble(parts[6]);
                double k = ParseDouble(parts[7]);
                double g = ParseDouble(parts[8]);
                double iMax = (parts.Length == 10 || parts.Length > 11) ? ParseDouble(parts[parts.Length - 1]) : 600;

                object startObj = FindNodeByNumber(startNode);
                object endObj = FindNodeByNumber(endNode);

                if (startObj == null || endObj == null) return;

                var branch = new Branch(startNode, endNode)
                {
                    ActiveResistance = r,
                    ReactiveResistance = x,
                    ReactiveConductivity = b,
                    TransformationRatio = k,
                    ActiveConductivity = g,
                    PermissibleCurrent = iMax
                };
                graphicBranches.Add(new GraphicBranch(branch, startObj, endObj));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка парсинга ветви: " + ex.Message);
            }
        }

        // Метод для парсинга чисел с точками (учитывает как "0.1" так и ".10")
        private double ParseDouble(string value)
        {
            // Заменяем запятые на точки если есть
            value = value.Replace(",", ".");

            // Если число начинается с точки, добавляем ноль
            if (value.StartsWith("."))
            {
                value = "0" + value;
            }

            return double.Parse(value, CultureInfo.InvariantCulture);
        }


        private Point SpiralPosition(int index, int stepPx)
        {
            double angle = index * 0.9;               // угол чуть меньше 90°
            double radius = stepPx * Math.Sqrt(index); // радиус растёт
            int cx = panel2.Width / 2;
            int cy = panel2.Height / 2;
            int x = cx + (int)(radius * Math.Cos(angle));
            int y = cy + (int)(radius * Math.Sin(angle));
            return new Point(x, y);
        }

        // -----------  масштаб и панорама  -----------
        private float scale = 1.0f;          // текущий масштаб
        private PointF pan = PointF.Empty;   // сдвиг холста в пикселях
        private bool panning = false;        // тянем ли холст ПКМ
        private Point lastPanPos;            // предыдущая точка при панораме

        private void Form1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (ModifierKeys != Keys.Control) return;
            float k = e.Delta > 0 ? 1.1f : 0.9f;
            scale *= k;
            scale = Math.Max(0.2f, Math.Min(5.0f, scale));   // 20 % – 500 %
            panel2.Invalidate();
        }

        // перевод экранных координат в «логические» (с учётом масштаба и панорамы)
        private PointF ScreenToModel(Point screen)
        {
            return new PointF((screen.X - pan.X) / scale,
                              (screen.Y - pan.Y) / scale);
        }


        private DialogResult ShowEditorForm(Form form)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = GetNextChildWindowLocation();
            return form.ShowDialog(this);
        }

        private void RegisterOpenedWindow(Form form)
        {
            var type = form.GetType();
            if (!openedEditorWindows.ContainsKey(type))
            {
                openedEditorWindows[type] = new List<Form>();
            }

            openedEditorWindows[type].Add(form);
            form.FormClosed += (s, e) => openedEditorWindows[type].Remove(form);
        }

        private Point GetNextChildWindowLocation()
        {
            int total = openedEditorWindows.Values.Sum(list => list.Count);
            int offset = 30 * (total % 8);
            Point origin = this.PointToScreen(Point.Empty);
            int x = Math.Max(origin.X + 80, origin.X + offset + 40);
            int y = Math.Max(origin.Y + 80, origin.Y + offset + 40);
            return new Point(x, y);
        }

        private void buttonOpenReport_Click(object sender, EventArgs e)
        {
            if ((DateTime.UtcNow - lastCalcDoubleClickAt).TotalMilliseconds < 350)
            {
                return;
            }

            if (isCalculationRunning)
            {
                StopCalculationLoopInternal();
            }
            else
            {
                StartCalculationLoopInternal();
            }
        }

        private void buttonOpenReport_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            lastCalcDoubleClickAt = DateTime.UtcNow;
            OpenCalculationReportWindow();
        }

        private void buttonOpenReport_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                OpenCalculationReportWindow();
            }
        }

        private void OpenCalculationReportWindow()
        {
            ApplyBranchLoadingColorsFromCurrentResult();

            var reportForm = new ReportForm();
            reportForm.SetNetworkSummary(graphicElements, graphicBranches, graphicShunts);
            reportForm.SetModeBurdeningInfo(BuildModeBurdeningReport());
            reportForm.SetResearchModeInfo(BuildResearchModeReport());
            RegisterOpenedWindow(reportForm);
            reportForm.StartPosition = FormStartPosition.Manual;
            reportForm.Location = GetNextChildWindowLocation();
            reportForm.Show(this);
        }

        private void RefreshOpenedReportForms()
        {
            if (!openedEditorWindows.TryGetValue(typeof(ReportForm), out var forms))
            {
                return;
            }

            foreach (var form in forms.OfType<ReportForm>().Where(f => f != null && !f.IsDisposed))
            {
                form.SetModeBurdeningInfo(BuildModeBurdeningReport());
                form.SetResearchModeInfo(BuildResearchModeReport());
            }
        }

        private void UpdateBurdeningStartInfo()
        {
            burdeningParameterDescription = "нет данных";
            burdeningStartValueKnown = false;
            burdeningTrackedData = null;
            burdeningTrackedKey = null;
            burdeningTrackedParameters.Clear();

            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node)
                {
                    CaptureBurdeningInfos("Узел", node.Data.Number, node.Data);
                }
                else if (element is GraphicBaseNode baseNode)
                {
                    CaptureBurdeningInfos("Базисный узел", baseNode.Data.Number, baseNode.Data);
                }
            }

            foreach (var branch in graphicBranches)
            {
                CaptureBurdeningInfos($"Ветвь {branch.Data.StartNodeNumber}-{branch.Data.EndNodeNumber}", 0, branch.Data);
            }

            foreach (var shunt in graphicShunts)
            {
                CaptureBurdeningInfos($"Шунт {shunt.Data.StartNodeNumber}", 0, shunt.Data);
            }

            if (burdeningTrackedParameters.Count > 0)
            {
                var first = burdeningTrackedParameters[0];
                burdeningStartValueKnown = true;
                burdeningTrackedData = first.Data;
                burdeningTrackedKey = first.Key;
                burdeningStartValue = first.StartValue;

                string suffix = burdeningTrackedParameters.Count > 1
                    ? $" (+ ещё {burdeningTrackedParameters.Count - 1})"
                    : string.Empty;
                burdeningParameterDescription = $"{first.Description}{suffix}";
            }
        }

        private void CaptureBurdeningInfos(string prefix, int number, dynamic data)
        {
            if (data == null || data.ParamIncrementSteps == null)
            {
                return;
            }

            foreach (var kv in ((Dictionary<string, double>)data.ParamIncrementSteps))
            {
                string key = kv.Key;
                string id = ParameterAutoChangeService.BuildId((object)data, key);
                if (!ParameterAutoChangeService.TryGet(id, out double step, out int interval, out bool running) || !running)
                {
                    continue;
                }

                string label = ConvertParamKeyToLabel(key);
                string description = number > 0 ? $"{prefix} {number}: {label}" : $"{prefix}: {label}";
                double startValue = GetParamValue(data, key);

                if (burdeningTrackedParameters.Any(x => ReferenceEquals((object)x.Data, (object)data) && x.Key == key))
                {
                    continue;
                }

                burdeningTrackedParameters.Add(new BurdeningTrackedParameter
                {
                    Description = description,
                    Data = data,
                    Key = key,
                    StartValue = startValue,
                    Step = step,
                    IntervalSeconds = interval
                });
            }
        }

        private string ConvertParamKeyToLabel(string key)
        {
            if (key == "U") return "Номинальное напряжение";
            if (key == "Ufact") return "Фактическое напряжение";
            if (key == "Ucalc") return "Расчётное напряжение";
            if (key == "P") return "P нагрузка";
            if (key == "Q") return "Q нагрузка";
            if (key == "Pg") return "P генерация";
            if (key == "Qg") return "Q генерация";
            if (key == "Uf") return "U фикс.";
            if (key == "Qmin") return "Q мин";
            if (key == "Qmax") return "Q макс";
            if (key == "R") return "R";
            if (key == "X") return "X";
            if (key == "B") return "B";
            if (key == "Ktr") return "K трансф.";
            if (key == "G") return "G";
            if (key == "Imax") return "Iдоп";
            return key;
        }
        private string BuildModeBurdeningReport()
        {
            SyncRunningAutoChangeTracking();

            var sb = new StringBuilder();
            sb.AppendLine("Утяжеление режима");
            sb.AppendLine();

            if (burdeningHistoryOrder.Count == 0)
            {
                sb.AppendLine("Автоизменение параметров не запущено.");
                return sb.ToString();
            }

            foreach (var historyKey in burdeningHistoryOrder)
            {
                if (!burdeningValueHistory.TryGetValue(historyKey, out var values) || values.Count == 0)
                {
                    continue;
                }

                string description = burdeningHistoryDescriptions.TryGetValue(historyKey, out var text) ? text : historyKey;
                sb.AppendLine(description);
                sb.AppendLine($"  Было: {values[0].ToString("F4", CultureInfo.InvariantCulture)}");
                for (int i = 1; i < values.Count; i++)
                {
                    sb.AppendLine($"  Стало: {values[i].ToString("F4", CultureInfo.InvariantCulture)}");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string BuildResearchModeReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Контроль параметров и устойчивость");
            sb.AppendLine();
            sb.AppendLine($"Комплексный контроль параметров: {(comprehensiveControlEnabled ? "включён" : "выключен")}");
            sb.AppendLine($"Процесс остановлен на шаге: {(divergedModeNumber.HasValue ? divergedModeNumber.Value.ToString(CultureInfo.InvariantCulture) : "нет")}");
            sb.AppendLine($"Причина остановки: {lastStopReason}");
            if (!string.IsNullOrWhiteSpace(lastStopDetails))
            {
                sb.AppendLine($"Дополнительно: {lastStopDetails}");
            }
            sb.AppendLine($"Статус: {(hasLastConvergedSnapshot ? $"Отображен режим итерации №{convergedModeCounter}." : "Стабильный режим ещё не зафиксирован.")}");
            sb.AppendLine();
            sb.AppendLine("Пороги комплексного контроля:");
            sb.AppendLine("- Несходимость итераций");
            sb.AppendLine("- Перегрузка ветви > 100%");
            sb.AppendLine("- Отклонение напряжения узла |ΔU| > 10% (ΔU% = (Uном - Uфакт)/Uном)");
            return sb.ToString();
        }

        private bool TryBuildControlStopReason(int stepNumber, out string reason, out string details)
        {
            var overloaded = graphicBranches
                .Where(b => b.Data.LoadingPercent > 100.0)
                .OrderByDescending(b => b.Data.LoadingPercent)
                .ToList();

            var criticalNodes = new List<string>();
            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                double uNom = node.Data.InitialVoltage;
                double uFact = node.Data.ActualVoltage > 0 ? node.Data.ActualVoltage : node.Data.CalculatedVoltage;
                if (uNom <= 0 || uFact <= 0)
                {
                    continue;
                }

                double delta = Math.Abs(CalculateVoltageDeviationPercent(uNom, uFact));
                if (delta > 10.0)
                {
                    criticalNodes.Add($"{node.Data.Number} ({delta:F1}%)");
                }
            }

            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                double uNom = baseNode.Data.InitialVoltage;
                double uFact = baseNode.Data.ActualVoltage > 0 ? baseNode.Data.ActualVoltage : baseNode.Data.CalculatedVoltage;
                if (uNom <= 0 || uFact <= 0)
                {
                    continue;
                }

                double delta = Math.Abs(CalculateVoltageDeviationPercent(uNom, uFact));
                if (delta > 10.0)
                {
                    criticalNodes.Add($"{baseNode.Data.Number} ({delta:F1}%)");
                }
            }

            if (overloaded.Count == 0 && criticalNodes.Count == 0)
            {
                reason = null;
                details = null;
                return false;
            }

            reason = overloaded.Count > 0
                ? $"Остановлено: перегрузка ветви {overloaded[0].Data.StartNodeNumber}-{overloaded[0].Data.EndNodeNumber} ({overloaded[0].Data.LoadingPercent:F1}%)."
                : "Остановлено: напряжение в узлах вышло за пределы ±10%.";

            var detailLines = new List<string>
            {
                $"Процесс остановлен на шаге №{stepNumber}."
            };
            if (criticalNodes.Count > 0)
            {
                detailLines.Add($"Узлы с |ΔU| > 10%: {string.Join(", ", criticalNodes)}.");
            }

            details = string.Join(" ", detailLines);
            return true;
        }

        private void StartCalculationLoopInternal()
        {
            if (isCalculationRunning)
            {
                return;
            }

            isCalculationRunning = true;
            convergedModeCounter = 0;
            divergedModeNumber = null;
            divergenceNotificationShown = false;
            hasLastDivergedSnapshot = false;
            showingDivergedSnapshot = false;
            lastDivergedBranchState.Clear();
            lastDivergedNodeState.Clear();
            lastDivergedBaseNodeState.Clear();
            lastDivergedCalculatedNodeVoltages.Clear();
            ParameterAutoChangeService.StopAll();
            lastStopReason = "нет";
            lastStopDetails = string.Empty;
            burdeningIterationLog.Clear();
            burdeningLastAutoValues.Clear();
            burdeningValueHistory.Clear();
            burdeningHistoryDescriptions.Clear();
            burdeningHistoryOrder.Clear();
            lastConvergedTrackedValues.Clear();
            lastDivergedTrackedValues.Clear();
            UpdateBurdeningStartInfo();
            RefreshOpenedReportForms();
            UpdateCalculationButtonUi();

            if (calculationTimer == null)
            {
                calculationTimer = new System.Windows.Forms.Timer();
                calculationTimer.Interval = Math.Max(1000, AppRuntimeSettings.UpdateIntervalSeconds * 1000);
                calculationTimer.Tick += async (s, e) => await RunCalculationCycleAsync();

                AppRuntimeSettings.UpdateIntervalChanged += seconds =>
                {
                    if (calculationTimer != null)
                    {
                        calculationTimer.Interval = Math.Max(1, seconds) * 1000;
                    }
                };
            }

            calculationTimer.Start();
            _ = RunCalculationCycleAsync();
            UpdateSnapshotNavigationButtons();
        }

        private void StopCalculationLoopInternal()
        {
            if (!isCalculationRunning)
            {
                return;
            }

            isCalculationRunning = false;
            calculationTimer?.Stop();
            ParameterAutoChangeService.StopAll();
            UpdateCalculationButtonUi();
            UpdateSnapshotNavigationButtons();
        }

        private void UpdateCalculationButtonUi()
        {
            buttonOpenReport.Text = isCalculationRunning ? "Стоп расчёт" : "Расчёт";
            buttonOpenReport.BackColor = isCalculationRunning ? Color.FromArgb(220, 38, 38) : ThemeAccentBlue;
            buttonOpenReport.ForeColor = isCalculationRunning ? Color.White : ThemeTextBlack;
            buttonOpenReport.FlatAppearance.BorderColor = isCalculationRunning ? Color.FromArgb(127, 29, 29) : ThemeBorderBlue;
            buttonOpenReport.FlatAppearance.BorderSize = 2;
            buttonOpenReport.Invalidate();
        }

        private async Task RunCalculationCycleAsync()
        {
            if (!isCalculationRunning || calculationInProgress)
            {
                return;
            }

            calculationInProgress = true;
            try
            {
                ApplyBranchLoadingColorsFromCurrentResult();
                await Task.CompletedTask;
            }
            finally
            {
                calculationInProgress = false;
            }
        }

        private void ApplyBranchLoadingColorsFromCurrentResult()
        {
            var result = RunCurrentLossesCalculation();
            if (result == null)
            {
                hasConvergenceStatus = false;
                panel2.Invalidate();
                return;
            }

            isLastModeConverged = !(result.NetworkRez ?? string.Empty).Contains("НЕ сошелся");
            hasConvergenceStatus = true;

            if (!isLastModeConverged)
            {
                divergedModeNumber = convergedModeCounter + 1;
                lastStopReason = "Остановлено: режим не существует (расчёт не сошёлся).";
                lastStopDetails = $"Процесс остановлен на шаге №{divergedModeNumber}.";

                SaveLastDivergedState();
                showingDivergedSnapshot = true;

                StopCalculationLoopInternal();

                if (!divergenceNotificationShown)
                {
                    divergenceNotificationShown = true;
                    MessageBox.Show(this, $"Остановлено: режим не существует (режим № {divergedModeNumber}).\nФоновое автоизменение параметров остановлено.", "Расчёт режима", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                panel2.Invalidate();
                RefreshElementsGrid();
                RefreshOpenedReportForms();
                return;
            }

            var lossesText = result.LossesRez;
            if (string.IsNullOrWhiteSpace(lossesText))
            {
                panel2.Invalidate();
                return;
            }

            var currents = ParseBranchCurrentsFromLosses(lossesText);
            foreach (var branch in graphicBranches)
            {
                var key = (branch.Data.StartNodeNumber, branch.Data.EndNodeNumber);
                if (!currents.TryGetValue(key, out var c) && !currents.TryGetValue((key.Item2, key.Item1), out c))
                {
                    branch.Data.CalculatedActiveCurrent = 0;
                    branch.Data.CalculatedReactiveCurrent = 0;
                    branch.Data.CalculatedCurrent = 0;
                    branch.Data.LoadingPercent = 0;
                    branch.LoadColor = Color.Black;
                    continue;
                }

                branch.Data.CalculatedActiveCurrent = c.Active;
                branch.Data.CalculatedReactiveCurrent = c.Reactive;
                double uNomKv = GetNodeNominalVoltageKv(branch.Data.StartNodeNumber);
                double limit = branch.Data.PermissibleCurrent <= 0 ? 600 : branch.Data.PermissibleCurrent;
                bool overloaded;
                branch.Data.CalculatedCurrent = ConvertTokToAmperes(c.Active, c.Reactive, uNomKv, limit, out overloaded);
                branch.Data.LoadingPercent = limit > 0 ? (branch.Data.CalculatedCurrent / limit) * 100.0 : 0;
                branch.LoadColor = GetBranchLoadColor(branch.Data.LoadingPercent);
            }

            ApplyNodeVoltageColorsFromNetworkRez(result.NetworkRez);
            convergedModeCounter++;
            SaveLastConvergedState();

            if (comprehensiveControlEnabled && TryBuildControlStopReason(convergedModeCounter + 1, out var stopReason, out var stopDetails))
            {
                divergedModeNumber = convergedModeCounter + 1;
                lastStopReason = stopReason;
                lastStopDetails = stopDetails;

                SaveLastDivergedState();
                showingDivergedSnapshot = true;

                StopCalculationLoopInternal();
                MessageBox.Show(this, $"{stopReason}\n{stopDetails}", "Комплексный контроль параметров", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                panel2.Invalidate();
                RefreshElementsGrid();
                RefreshOpenedReportForms();
                return;
            }

            panel2.Invalidate();
            RefreshElementsGrid();
            RefreshOpenedReportForms();
        }


        private void SaveLastConvergedState()
        {
            lastConvergedBranchState.Clear();
            foreach (var branch in graphicBranches)
            {
                lastConvergedBranchState[(branch.Data.StartNodeNumber, branch.Data.EndNodeNumber)] =
                    (branch.Data.CalculatedActiveCurrent, branch.Data.CalculatedReactiveCurrent, branch.Data.CalculatedCurrent, branch.Data.LoadingPercent, branch.LoadColor);
            }

            lastConvergedNodeState.Clear();
            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                lastConvergedNodeState[node.Data.Number] = (node.Data.CalculatedVoltage, node.VoltageColor);
            }

            lastConvergedBaseNodeState.Clear();
            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                lastConvergedBaseNodeState[baseNode.Data.Number] = (baseNode.Data.CalculatedVoltage, baseNode.VoltageColor);
            }

            lastConvergedCalculatedNodeVoltages.Clear();
            foreach (var kv in lastCalculatedNodeVoltages)
            {
                lastConvergedCalculatedNodeVoltages[kv.Key] = kv.Value;
            }

            CaptureTrackedParameterSnapshot(lastConvergedTrackedValues);
            hasLastConvergedSnapshot = true;
        }

        private void SaveLastDivergedState()
        {
            lastDivergedBranchState.Clear();
            foreach (var branch in graphicBranches)
            {
                lastDivergedBranchState[(branch.Data.StartNodeNumber, branch.Data.EndNodeNumber)] =
                    (branch.Data.CalculatedActiveCurrent, branch.Data.CalculatedReactiveCurrent, branch.Data.CalculatedCurrent, branch.Data.LoadingPercent, branch.LoadColor);
            }

            lastDivergedNodeState.Clear();
            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                lastDivergedNodeState[node.Data.Number] = (node.Data.CalculatedVoltage, node.VoltageColor);
            }

            lastDivergedBaseNodeState.Clear();
            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                lastDivergedBaseNodeState[baseNode.Data.Number] = (baseNode.Data.CalculatedVoltage, baseNode.VoltageColor);
            }

            lastDivergedCalculatedNodeVoltages.Clear();
            foreach (var kv in lastCalculatedNodeVoltages)
            {
                lastDivergedCalculatedNodeVoltages[kv.Key] = kv.Value;
            }

            CaptureTrackedParameterSnapshot(lastDivergedTrackedValues);
            EnsureDivergedValueIsTrackedInHistory();
            hasLastDivergedSnapshot = true;
            UpdateSnapshotNavigationButtons();
        }


        private void EnsureDivergedValueIsTrackedInHistory()
        {
            foreach (var tracked in burdeningTrackedParameters)
            {
                string id = ParameterAutoChangeService.BuildId((object)tracked.Data, tracked.Key);
                if (!lastDivergedTrackedValues.TryGetValue(id, out var divergedValue))
                {
                    continue;
                }

                string element = ResolveBurdeningElementLabel(tracked.Data);
                string historyKey = $"{element}|{tracked.Key}";
                if (!burdeningValueHistory.TryGetValue(historyKey, out var values))
                {
                    values = new List<double>();
                    burdeningValueHistory[historyKey] = values;
                    burdeningHistoryOrder.Add(historyKey);
                    burdeningHistoryDescriptions[historyKey] = $"{element} | {ConvertParamKeyToLabel(tracked.Key)}";
                }

                if (values.Count == 0 || Math.Abs(values[values.Count - 1] - divergedValue) > 1e-9)
                {
                    values.Add(divergedValue);
                }
            }
        }
        private void RestoreLastDivergedState()
        {
            if (!hasLastDivergedSnapshot)
            {
                return;
            }

            foreach (var branch in graphicBranches)
            {
                if (lastDivergedBranchState.TryGetValue((branch.Data.StartNodeNumber, branch.Data.EndNodeNumber), out var state)
                    || lastDivergedBranchState.TryGetValue((branch.Data.EndNodeNumber, branch.Data.StartNodeNumber), out state))
                {
                    branch.Data.CalculatedActiveCurrent = state.Active;
                    branch.Data.CalculatedReactiveCurrent = state.Reactive;
                    branch.Data.CalculatedCurrent = state.Current;
                    branch.Data.LoadingPercent = state.Loading;
                    branch.LoadColor = state.Color;
                }
            }

            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                if (lastDivergedNodeState.TryGetValue(node.Data.Number, out var state))
                {
                    node.Data.CalculatedVoltage = state.CalculatedVoltage;
                    node.VoltageColor = state.Color;
                }
            }

            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                if (lastDivergedBaseNodeState.TryGetValue(baseNode.Data.Number, out var state))
                {
                    baseNode.Data.CalculatedVoltage = state.CalculatedVoltage;
                    baseNode.VoltageColor = state.Color;
                }
            }

            lastCalculatedNodeVoltages.Clear();
            foreach (var kv in lastDivergedCalculatedNodeVoltages)
            {
                lastCalculatedNodeVoltages[kv.Key] = kv.Value;
            }
        }

        private void RestoreLastConvergedState()
        {
            foreach (var branch in graphicBranches)
            {
                if (lastConvergedBranchState.TryGetValue((branch.Data.StartNodeNumber, branch.Data.EndNodeNumber), out var state)
                    || lastConvergedBranchState.TryGetValue((branch.Data.EndNodeNumber, branch.Data.StartNodeNumber), out state))
                {
                    branch.Data.CalculatedActiveCurrent = state.Active;
                    branch.Data.CalculatedReactiveCurrent = state.Reactive;
                    branch.Data.CalculatedCurrent = state.Current;
                    branch.Data.LoadingPercent = state.Loading;
                    branch.LoadColor = state.Color;
                }
            }

            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                if (lastConvergedNodeState.TryGetValue(node.Data.Number, out var state))
                {
                    node.Data.CalculatedVoltage = state.CalculatedVoltage;
                    node.VoltageColor = state.Color;
                }
            }

            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                if (lastConvergedBaseNodeState.TryGetValue(baseNode.Data.Number, out var state))
                {
                    baseNode.Data.CalculatedVoltage = state.CalculatedVoltage;
                    baseNode.VoltageColor = state.Color;
                }
            }

            lastCalculatedNodeVoltages.Clear();
            foreach (var kv in lastConvergedCalculatedNodeVoltages)
            {
                lastCalculatedNodeVoltages[kv.Key] = kv.Value;
            }
        }

        private void SyncRunningAutoChangeTracking()
        {
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node)
                {
                    SyncAutoChangeForData(node.Data);
                }
                else if (element is GraphicBaseNode baseNode)
                {
                    SyncAutoChangeForData(baseNode.Data);
                }
            }

            foreach (var branch in graphicBranches)
            {
                SyncAutoChangeForData(branch.Data);
            }

            foreach (var shunt in graphicShunts)
            {
                SyncAutoChangeForData(shunt.Data);
            }
        }

        private void SyncAutoChangeForData(dynamic data)
        {
            if (data == null || data.ParamIncrementSteps == null)
            {
                return;
            }

            foreach (var kv in ((Dictionary<string, double>)data.ParamIncrementSteps))
            {
                string key = kv.Key;
                string id = ParameterAutoChangeService.BuildId((object)data, key);
                if (!ParameterAutoChangeService.TryGet(id, out double step, out int interval, out bool running) || !running)
                {
                    continue;
                }

                RegisterBurdeningTracking(data, key, step, interval);

                string element = ResolveBurdeningElementLabel(data);
                string historyKey = $"{element}|{key}";
                double currentValue = GetParamValue(data, key);
                if (burdeningValueHistory.TryGetValue(historyKey, out var values)
                    && (values.Count == 0 || Math.Abs(values[values.Count - 1] - currentValue) > 1e-9))
                {
                    values.Add(currentValue);
                }
            }
        }

        private void RegisterBurdeningTracking(dynamic data, string key, double step, int intervalSeconds)
        {
            if (!isCalculationRunning)
            {
                return;
            }

            if (!burdeningTrackedParameters.Any(x => ReferenceEquals((object)x.Data, (object)data) && x.Key == key))
            {
                string description = $"{ResolveBurdeningElementLabel(data)} | {ConvertParamKeyToLabel(key)}";
                burdeningTrackedParameters.Add(new BurdeningTrackedParameter
                {
                    Description = description,
                    Data = data,
                    Key = key,
                    StartValue = GetParamValue(data, key),
                    Step = step,
                    IntervalSeconds = intervalSeconds
                });
            }

            string element = ResolveBurdeningElementLabel(data);
            string historyKey = $"{element}|{key}";
            double currentValue = GetParamValue(data, key);
            if (!burdeningValueHistory.TryGetValue(historyKey, out var values))
            {
                values = new List<double>();
                burdeningValueHistory[historyKey] = values;
                burdeningHistoryOrder.Add(historyKey);
                burdeningHistoryDescriptions[historyKey] = $"{element} | {ConvertParamKeyToLabel(key)}";
            }

            if (values.Count == 0)
            {
                values.Add(currentValue);
            }

            burdeningLastAutoValues[historyKey] = currentValue;
        }

        private void CaptureTrackedParameterSnapshot(Dictionary<string, double> target)
        {
            target.Clear();
            foreach (var tracked in burdeningTrackedParameters)
            {
                string id = ParameterAutoChangeService.BuildId((object)tracked.Data, tracked.Key);
                target[id] = GetParamValue(tracked.Data, tracked.Key);
            }
        }

        private void RestoreTrackedParameterSnapshot(Dictionary<string, double> source)
        {
            if (source == null || source.Count == 0)
            {
                return;
            }

            foreach (var tracked in burdeningTrackedParameters)
            {
                string id = ParameterAutoChangeService.BuildId((object)tracked.Data, tracked.Key);
                if (source.TryGetValue(id, out var value))
                {
                    ApplyParamValue(tracked.Data, tracked.Key, value);
                }
            }
        }

        private void RecalculateSnapshotState()
        {
            var result = RunCurrentLossesCalculation();
            if (result == null || string.IsNullOrWhiteSpace(result.LossesRez))
            {
                return;
            }

            var currents = ParseBranchCurrentsFromLosses(result.LossesRez);
            foreach (var branch in graphicBranches)
            {
                var key = (branch.Data.StartNodeNumber, branch.Data.EndNodeNumber);
                if (!currents.TryGetValue(key, out var c) && !currents.TryGetValue((key.Item2, key.Item1), out c))
                {
                    continue;
                }

                branch.Data.CalculatedActiveCurrent = c.Active;
                branch.Data.CalculatedReactiveCurrent = c.Reactive;
                double uNomKv = GetNodeNominalVoltageKv(branch.Data.StartNodeNumber);
                double limit = branch.Data.PermissibleCurrent <= 0 ? 600 : branch.Data.PermissibleCurrent;
                bool overloaded;
                branch.Data.CalculatedCurrent = ConvertTokToAmperes(c.Active, c.Reactive, uNomKv, limit, out overloaded);
                branch.Data.LoadingPercent = limit > 0 ? (branch.Data.CalculatedCurrent / limit) * 100.0 : 0;
                branch.LoadColor = GetBranchLoadColor(branch.Data.LoadingPercent);
            }

            ApplyNodeVoltageColorsFromNetworkRez(result.NetworkRez);
        }

        private void ApplyNodeVoltageColorsFromNetworkRez(string networkRez)
        {
            var voltages = ParseNodeVoltagesFromNetworkRez(networkRez);
            lastCalculatedNodeVoltages.Clear();
            foreach (var kv in voltages)
            {
                lastCalculatedNodeVoltages[kv.Key] = kv.Value;
            }

            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                if (!voltages.TryGetValue(node.Data.Number, out var uFact))
                {
                    node.Data.CalculatedVoltage = 0;
                    double uFactTelemetry = node.Data.ActualVoltage > 0 ? node.Data.ActualVoltage : node.Data.InitialVoltage;
                    node.VoltageColor = GetNodeVoltageColor(uFactTelemetry, node.Data.CalculatedVoltage > 0 ? node.Data.CalculatedVoltage : uFactTelemetry);
                    continue;
                }

                node.Data.CalculatedVoltage = uFact;
                if (!IsFactTelemetryEnabled(node.Data))
                {
                    node.Data.ActualVoltage = node.Data.CalculatedVoltage;
                }

                double uFactForColor = node.Data.ActualVoltage > 0 ? node.Data.ActualVoltage : uFact;
                node.VoltageColor = GetNodeVoltageColor(node.Data.InitialVoltage, uFactForColor);
            }

            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                if (!voltages.TryGetValue(baseNode.Data.Number, out var uFact))
                {
                    baseNode.Data.CalculatedVoltage = 0;
                    double uFactTelemetry = baseNode.Data.ActualVoltage > 0 ? baseNode.Data.ActualVoltage : baseNode.Data.InitialVoltage;
                    baseNode.VoltageColor = GetNodeVoltageColor(uFactTelemetry, baseNode.Data.CalculatedVoltage > 0 ? baseNode.Data.CalculatedVoltage : uFactTelemetry);
                    continue;
                }

                baseNode.Data.CalculatedVoltage = uFact;
                if (!IsFactTelemetryEnabled(baseNode.Data))
                {
                    baseNode.Data.ActualVoltage = baseNode.Data.CalculatedVoltage;
                }

                double uFactForColor = baseNode.Data.ActualVoltage > 0 ? baseNode.Data.ActualVoltage : uFact;
                baseNode.VoltageColor = GetNodeVoltageColor(baseNode.Data.InitialVoltage, uFactForColor);
            }
        }

        private bool IsFactTelemetryEnabled(dynamic data)
        {
            if (data == null || data.ParamAutoModes == null)
            {
                return false;
            }

            var modes = data.ParamAutoModes as Dictionary<string, bool>;
            if (modes == null)
            {
                return false;
            }

            return modes.TryGetValue("Ufact", out bool enabled) && enabled;
        }

        private Dictionary<int, double> ParseNodeVoltagesFromNetworkRez(string networkRez)
        {
            var result = new Dictionary<int, double>();
            if (string.IsNullOrWhiteSpace(networkRez))
            {
                return result;
            }

            bool inNodesSection = false;
            var lines = networkRez.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("Результаты расчета по узлам", StringComparison.OrdinalIgnoreCase))
                {
                    inNodesSection = true;
                    continue;
                }

                if (!inNodesSection)
                {
                    continue;
                }

                if (line.StartsWith("-", StringComparison.Ordinal) || line.StartsWith("Баланс", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                var parts = Regex.Split(line, @"\s+");
                if (parts.Length < 2)
                {
                    continue;
                }

                if (!int.TryParse(parts[0], out var nodeNumber))
                {
                    continue;
                }

                if (double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var uFact))
                {
                    result[nodeNumber] = uFact;
                }
            }

            return result;
        }

        private Color GetNodeVoltageColor(double nominalVoltage, double factualVoltage)
        {
            if (nominalVoltage <= 0 || factualVoltage <= 0)
            {
                return Color.LightBlue;
            }

            double deltaPercent = CalculateVoltageDeviationPercent(nominalVoltage, factualVoltage);
            double absDelta = Math.Abs(deltaPercent);
            if (absDelta <= 5.0)
            {
                return Color.LightGreen;
            }

            if (absDelta <= 10.0)
            {
                return Color.Yellow;
            }

            return Color.IndianRed;
        }

        private double CalculateVoltageDeviationPercent(double uNom, double uFact)
        {
            if (Math.Abs(uNom) < 1e-9)
            {
                return 0;
            }

            return ((uNom - uFact) / uNom) * 100.0;
        }

        private void UpdateHoverTooltip(object element, Point screenPoint)
        {
            if (element == null)
            {
                hoverToolTip.Hide(panel2);
                hoveredElement = null;
                lastTooltipText = null;
                return;
            }

            string text = BuildHoverTooltipText(element);
            if (string.IsNullOrWhiteSpace(text))
            {
                hoverToolTip.Hide(panel2);
                hoveredElement = null;
                lastTooltipText = null;
                return;
            }

            if (!ReferenceEquals(hoveredElement, element) || !string.Equals(lastTooltipText, text, StringComparison.Ordinal))
            {
                hoverToolTip.Hide(panel2);
                hoveredElement = element;
                lastTooltipText = text;
                hoverToolTip.Show(text, panel2, screenPoint.X + 16, screenPoint.Y + 16, 5000);
            }
        }

        private string BuildHoverTooltipText(object element)
        {
            if (element is GraphicNode node)
            {
                return BuildNodeHoverText(node.Data.Number, node.Data.InitialVoltage, node.Data.ActualVoltage, node.Data.CalculatedVoltage, false);
            }

            if (element is GraphicBaseNode baseNode)
            {
                return BuildNodeHoverText(baseNode.Data.Number, baseNode.Data.InitialVoltage, baseNode.Data.ActualVoltage, baseNode.Data.CalculatedVoltage, true);
            }

            if (element is GraphicBranch branch)
            {
                double uNomKv = GetNodeNominalVoltageKv(branch.Data.StartNodeNumber);
                double iaAmp = ConvertPuCurrentComponentToAmperes(branch.Data.CalculatedActiveCurrent, uNomKv);
                double irAmp = ConvertPuCurrentComponentToAmperes(branch.Data.CalculatedReactiveCurrent, uNomKv);
                double iMax = branch.Data.PermissibleCurrent <= 0 ? 600 : branch.Data.PermissibleCurrent;
                return $"Ветвь {branch.Data.StartNodeNumber}-{branch.Data.EndNodeNumber}\n" +
                       $"Iакт={iaAmp:F2} A\n" +
                       $"Iреакт={irAmp:F2} A\n" +
                       $"Iдоп={iMax:F2} A\n" +
                       $"Загрузка={branch.Data.LoadingPercent:F2}%";
            }

            return null;
        }

        private string BuildNodeHoverText(int nodeNumber, double uNom, double telemetryUfact, double calculatedU, bool isBaseNode)
        {
            double uFact = telemetryUfact;
            if (uFact <= 0 && lastCalculatedNodeVoltages.TryGetValue(nodeNumber, out var calcUFact))
            {
                uFact = calcUFact;
            }

            if (uFact <= 0 && calculatedU > 0)
            {
                uFact = calculatedU;
            }

            if (uFact <= 0)
            {
                return $"{(isBaseNode ? "Базисный узел" : "Узел")} {nodeNumber}\nНоминальное напряжение={uNom:F2} кВ\nФактическое напряжение=нет данных\nРасчётное напряжение=нет данных";
            }

            double delta = uNom > 0 ? CalculateVoltageDeviationPercent(uNom, uFact) : 0;
            string calcText = calculatedU > 0 ? $"{calculatedU:F2} кВ" : "нет данных";
            return $"{(isBaseNode ? "Базисный узел" : "Узел")} {nodeNumber}\n" +
                   $"Номинальное напряжение={uNom:F2} кВ\n" +
                   $"Фактическое напряжение={uFact:F2} кВ\n" +
                   $"Расчётное напряжение={calcText}\n" +
                   $"ΔU={delta:F2}%";
        }

        private double ConvertPuCurrentComponentToAmperes(double componentPu, double uNomKv)
        {
            const double sBase = 1000.0;
            double u = uNomKv <= 0 ? 110.0 : uNomKv;
            return (componentPu * sBase) / (Math.Sqrt(3.0) * u) * 1000.0;
        }

        private double GetNodeNominalVoltageKv(int nodeNumber)
        {
            foreach (var element in graphicElements)
            {
                if (element is GraphicNode node && node.Data.Number == nodeNumber)
                {
                    return node.Data.InitialVoltage > 0 ? node.Data.InitialVoltage : 110;
                }

                if (element is GraphicBaseNode baseNode && baseNode.Data.Number == nodeNumber)
                {
                    return baseNode.Data.InitialVoltage > 0 ? baseNode.Data.InitialVoltage : 110;
                }
            }

            return 110;
        }

        // Перевод тока из network.tok в Амперы по заданной формуле.
        private double ConvertTokToAmperes(double re, double im, double uNomKv, double iMax, out bool overloaded)
        {
            double iPu = Math.Sqrt(re * re + im * im);
            const double sBase = 1000.0;
            double u = uNomKv <= 0 ? 110.0 : uNomKv;
            double iAmp = (iPu * sBase) / (Math.Sqrt(3.0) * u) * 1000.0;
            overloaded = iAmp > iMax;
            return iAmp;
        }

        private EngineResult RunCurrentLossesCalculation()
        {
            var cduLines = BuildCduLinesForEngine();
            var engine = new ConsoleApplicationEngine(cduLines, CalculationOptions.Precision, CalculationOptions.MaxIterations);
            return engine.Run();
        }

        private List<string> BuildCduLinesForEngine()
        {
            var lines = new List<string>();

            var baseNodeNumbers = new HashSet<int>(graphicElements.OfType<GraphicBaseNode>().Select(x => x.Data.Number));

            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                if (baseNodeNumbers.Contains(node.Data.Number))
                {
                    continue;
                }

                lines.Add($"0201 0   {node.Data.Number,3}  {node.Data.InitialVoltage,3}     " +
                          $"{FormatInt(node.Data.NominalActivePower),4}  {FormatInt(node.Data.NominalReactivePower),3}  " +
                          $"{FormatInt(node.Data.ActivePowerGeneration),1} {FormatInt(node.Data.ReactivePowerGeneration),1}  " +
                          $"{FormatInt(node.Data.FixedVoltageModule),3} {FormatInt(node.Data.MinReactivePower),1} {FormatInt(node.Data.MaxReactivePower),1}");
            }

            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                lines.Add($"0102 0   {baseNode.Data.Number,3}  {baseNode.Data.InitialVoltage,3}       " +
                          $"{FormatInt(baseNode.Data.NominalActivePower),1}    " +
                          $"{FormatInt(baseNode.Data.NominalReactivePower),1}  " +
                          $"{FormatInt(baseNode.Data.ActivePowerGeneration),1} {FormatInt(baseNode.Data.ReactivePowerGeneration),1}   " +
                          $"{FormatInt(baseNode.Data.FixedVoltageModule),1} " +
                          $"{FormatInt(baseNode.Data.MinReactivePower),1} " +
                          $"{FormatInt(baseNode.Data.MaxReactivePower),1}");
            }

            foreach (var shunt in graphicShunts)
            {
                lines.Add($"0301 0   {shunt.Data.StartNodeNumber,3}      {shunt.Data.EndNodeNumber,2}    " +
                          $"{FormatDouble(shunt.Data.ActiveResistance),4}   " +
                          $"{FormatDouble(shunt.Data.ReactiveResistance),5}");
            }

            foreach (var branch in graphicBranches)
            {
                lines.Add($"0301 0   {branch.Data.StartNodeNumber,3}      {branch.Data.EndNodeNumber,2}    " +
                          $"{FormatDouble(branch.Data.ActiveResistance),4}   " +
                          $"{FormatDouble(branch.Data.ReactiveResistance),5}   " +
                          $"{FormatDouble(branch.Data.ReactiveConductivity, true),6}     " +
                          $"{FormatDouble(branch.Data.TransformationRatio),5} " +
                          $"{FormatInt(branch.Data.ActiveConductivity),1} 0 0");
            }

            return lines;
        }

        private Dictionary<(int Start, int End), (double Active, double Reactive)> ParseBranchCurrentsFromLosses(string lossesText)
        {
            var result = new Dictionary<(int Start, int End), (double Active, double Reactive)>();
            var lines = lossesText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            bool inBranchTable = false;

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("Ветвь Нач.", StringComparison.OrdinalIgnoreCase))
                {
                    inBranchTable = true;
                    continue;
                }

                if (!inBranchTable || string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (!char.IsDigit(line[0]))
                {
                    if (line.StartsWith("Задающие токи узлов", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    continue;
                }

                var tokens = Regex.Split(line, @"\s+");
                if (tokens.Length < 5)
                {
                    continue;
                }

                if (!int.TryParse(tokens[0], out int start) || !int.TryParse(tokens[1], out int end))
                {
                    continue;
                }

                if (!double.TryParse(tokens[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double active))
                {
                    continue;
                }

                if (!double.TryParse(tokens[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double reactive))
                {
                    continue;
                }

                result[(start, end)] = (active, reactive);
            }

            return result;
        }

        private Color GetBranchLoadColor(double loadingPercent)
        {
            if (loadingPercent <= 50) return Color.Blue;
            if (loadingPercent <= 80) return Color.Green;
            if (loadingPercent <= 95) return Color.Yellow;
            if (loadingPercent <= 100) return Color.Orange;
            return Color.Red;
        }

        private void ApplyTheme()
        {
            this.BackColor = ThemePageBackground;
            this.ForeColor = ThemeTextBlack;
            panel1.BackColor = ThemePanelBackground;
            panel2.BackColor = Color.White;

            foreach (Control ctrl in panel1.Controls)
            {
                if (ctrl is Button btn)
                {
                    btn.BackColor = ThemeAccentBlue;
                    btn.ForeColor = ThemeTextBlack;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = ThemeBorderBlue;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.FlatAppearance.MouseOverBackColor = ThemeAccentBlueHover;
                    btn.FlatAppearance.MouseDownBackColor = ThemeAccentBluePressed;
                    btn.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                }
            }

            if (elementsGrid != null)
            {
                elementsGrid.BackgroundColor = ThemePageBackground;
                elementsGrid.ColumnHeadersDefaultCellStyle.BackColor = ThemePanelBackground;
                elementsGrid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeTextBlack;
                elementsGrid.DefaultCellStyle.BackColor = Color.White;
                elementsGrid.DefaultCellStyle.ForeColor = ThemeTextBlack;
                elementsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
                elementsGrid.DefaultCellStyle.SelectionForeColor = ThemeTextBlack;
                elementsGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(220, 252, 231);
                elementsGrid.GridColor = Color.FromArgb(219, 234, 254);
            }

            telemetryEditorForm?.ApplyTheme();
            ApplyBoldFontsRecursive(this);
            RefreshElementsGrid();
            panel2.Invalidate();
        }

        private void ApplyBoldFontsRecursive(Control root)
        {
            if (root == null) return;
            root.Font = new Font(root.Font, FontStyle.Bold);
            foreach (Control child in root.Controls)
            {
                ApplyBoldFontsRecursive(child);
            }
        }

        private void ConfigureToolbarStyle()
        {
            panel1.Padding = new Padding(8);
            panel1.BackColor = ThemePanelBackground;
            foreach (Control ctrl in panel1.Controls)
            {
                if (ctrl is Button btn)
                {
                    btn.BackColor = ThemeAccentBlue;
                    btn.ForeColor = ThemeTextBlack;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = ThemeBorderBlue;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.FlatAppearance.MouseOverBackColor = ThemeAccentBlueHover;
                    btn.FlatAppearance.MouseDownBackColor = ThemeAccentBluePressed;
                    btn.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    btn.Size = new Size(124, 34);
                }
            }

            ArrangeMainToolbarButtons();
        }

        private void ArrangeMainToolbarButtons()
        {
            string[] topOrder = { "buttonAddNode", "buttonAddBaseNode", "buttonAddBranch", "buttonAddShunt", "buttonDelete", "buttonClearAll", "buttonExportData", "buttonImportData", "buttonOpenReport" };
            string[] bottomOrder = { "buttonCalcSettings", "buttonOpenTelemetryForm", "buttonOpenClientSettingsForm" };

            var topButtons = topOrder.Select(name => panel1.Controls.Find(name, false).FirstOrDefault()).OfType<Button>().ToList();
            int x = 12;
            foreach (var button in topButtons)
            {
                button.Location = new Point(x, 12);
                x += button.Width + 8;
            }

            var lowerButtons = bottomOrder.Select(name => panel1.Controls.Find(name, false).FirstOrDefault()).OfType<Button>().ToList();
            int x2 = 12;
            foreach (var button in lowerButtons)
            {
                button.Location = new Point(x2, 52);
                x2 += button.Width + 8;
            }
        }

        private void StartClock()
        {
            uiClockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            uiClockTimer.Tick += (s, e) =>
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                toolStripStatusLabelClock.Text = $"Время: {time}";
                labelTopClock.Text = time;
            };
            string initialTime = DateTime.Now.ToString("HH:mm:ss");
            toolStripStatusLabelClock.Text = $"Время: {initialTime}";
            labelTopClock.Text = initialTime;
            uiClockTimer.Start();
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

        private void buttonApplyStaticIp_Click(object sender, EventArgs e)
        {
            if (comboBoxAdapters.SelectedItem == null)
            {
                MessageBox.Show("Выберите сетевой адаптер", "Сеть", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!IPAddress.TryParse(textBoxStaticIp.Text, out _) ||
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

            string adapter = selectedAdapter.Name;
            string args = $"interface ip set address name=\"{adapter}\" static {textBoxStaticIp.Text} {textBoxMask.Text} {textBoxGateway.Text}";

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

        private void StartGlobalTelemetryPolling()
        {
            telemetryTimer = new System.Windows.Forms.Timer { Interval = AppRuntimeSettings.UpdateIntervalSeconds * 1000 };
            telemetryTimer.Tick += async (s, e) => await PollAllNodeTelemetryAsync();
            telemetryTimer.Start();
            AppRuntimeSettings.UpdateIntervalChanged += seconds =>
            {
                if (telemetryTimer != null)
                {
                    telemetryTimer.Interval = Math.Max(1, seconds) * 1000;
                }
            };
        }

        private async Task PollAllNodeTelemetryAsync()
        {
            if (telemetryPollingInProgress) return;
            telemetryPollingInProgress = true;

            try
            {
                for (int i = 0; i < graphicElements.Count; i++)
                {
                    if (graphicElements[i] is GraphicNode gNode)
                    {
                        var node = gNode.Data;
                        if (!string.IsNullOrWhiteSpace(node.IPAddress) && ShouldPoll(node))
                        {
                            await PollSingleNodeAsync(node);
                        }
                    }
                    else if (graphicElements[i] is GraphicBaseNode gBaseNode)
                    {
                        var baseNode = gBaseNode.Data;
                        if (!string.IsNullOrWhiteSpace(baseNode.IPAddress) && ShouldPoll(baseNode))
                        {
                            await PollGenericDataAsync(baseNode);
                        }
                    }
                }

                for (int i = 0; i < graphicBranches.Count; i++)
                {
                    var branch = graphicBranches[i].Data;
                    if (!string.IsNullOrWhiteSpace(branch.IPAddress) && ShouldPoll(branch))
                    {
                        await PollGenericDataAsync(branch);
                    }
                }

                for (int i = 0; i < graphicShunts.Count; i++)
                {
                    var shunt = graphicShunts[i].Data;
                    if (!string.IsNullOrWhiteSpace(shunt.IPAddress) && ShouldPoll(shunt))
                    {
                        await PollGenericDataAsync(shunt);
                    }
                }
            }
            catch
            {
            }
            finally
            {
                telemetryPollingInProgress = false;
            }

            ApplyRealtimeVisualizationFromTelemetry();
            panel2.Invalidate();
        }

        private bool ShouldPoll(dynamic data)
        {
            object key = (object)data;
            int interval = 2;
            try
            {
                interval = Math.Max(1, Convert.ToInt32(data.MeasurementIntervalSeconds));
            }
            catch { }

            DateTime now = DateTime.UtcNow;
            if (lastTelemetryReadAt.TryGetValue(key, out DateTime last) && (now - last).TotalSeconds < interval)
            {
                return false;
            }

            lastTelemetryReadAt[key] = now;
            return true;
        }

        private async Task PollSingleNodeAsync(Node node)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    int port = 502;
                    int.TryParse(node.Port, out port);

                    var connectTask = client.ConnectAsync(node.IPAddress, port);
                    if (await Task.WhenAny(connectTask, Task.Delay(1200)) != connectTask)
                    {
                        return;
                    }

                    var factory = new ModbusFactory();
                    var master = factory.CreateMaster(client);
                    byte slaveId = 1;
                    byte.TryParse(node.NodeID, out slaveId);

                    UpdateNodeFieldByKey(node, master, slaveId, "Ufact", nameof(Node.ActualVoltage));
                    UpdateNodeFieldByKey(node, master, slaveId, "P", nameof(Node.NominalActivePower));
                    UpdateNodeFieldByKey(node, master, slaveId, "Q", nameof(Node.NominalReactivePower));
                    UpdateNodeFieldByKey(node, master, slaveId, "Pg", nameof(Node.ActivePowerGeneration));
                    UpdateNodeFieldByKey(node, master, slaveId, "Qg", nameof(Node.ReactivePowerGeneration));
                    UpdateNodeFieldByKey(node, master, slaveId, "Uf", nameof(Node.FixedVoltageModule));
                    UpdateNodeFieldByKey(node, master, slaveId, "Qmin", nameof(Node.MinReactivePower));
                    UpdateNodeFieldByKey(node, master, slaveId, "Qmax", nameof(Node.MaxReactivePower));
                }
            }
            catch
            {
            }
        }


        private async Task PollGenericDataAsync(dynamic data)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    int port = 502;
                    int.TryParse(Convert.ToString(data.Port), out port);
                    var connectTask = client.ConnectAsync(Convert.ToString(data.IPAddress), port);
                    if (await Task.WhenAny(connectTask, Task.Delay(1200)) != connectTask) return;

                    var factory = new ModbusFactory();
                    var master = factory.CreateMaster(client);
                    byte slaveId = 1;
                    byte.TryParse(Convert.ToString(data.DeviceID), out slaveId);

                    foreach (var kv in ((Dictionary<string, bool>)data.ParamAutoModes).ToList())
                    {
                        if (!kv.Value) continue;
                        if (!data.ParamRegisters.ContainsKey(kv.Key)) continue;
                        if (!ushort.TryParse(Convert.ToString(data.ParamRegisters[kv.Key]), out ushort addr)) continue;
                        var val = master.ReadHoldingRegisters(slaveId, addr, 1)[0];
                        ApplyParamValue(data, kv.Key, val);
                    }
                }
            }
            catch { }
        }

        private void UpdateNodeFieldByKey(Node node, IModbusMaster master, byte slaveId, string key, string propertyName)
        {
            if (!node.ParamAutoModes.ContainsKey(key) || !node.ParamAutoModes[key]) return;
            if (!node.ParamRegisters.ContainsKey(key)) return;
            if (!ushort.TryParse(node.ParamRegisters[key], out ushort addr)) return;

            var response = master.ReadHoldingRegisters(slaveId, addr, 1);
            double val = response[0];
            if (propertyName == nameof(Node.InitialVoltage)) node.InitialVoltage = val;
            else if (propertyName == nameof(Node.ActualVoltage)) node.ActualVoltage = val;
            else if (propertyName == nameof(Node.NominalActivePower)) node.NominalActivePower = val;
            else if (propertyName == nameof(Node.NominalReactivePower)) node.NominalReactivePower = val;
            else if (propertyName == nameof(Node.ActivePowerGeneration)) node.ActivePowerGeneration = val;
            else if (propertyName == nameof(Node.ReactivePowerGeneration)) node.ReactivePowerGeneration = val;
            else if (propertyName == nameof(Node.FixedVoltageModule)) node.FixedVoltageModule = val;
            else if (propertyName == nameof(Node.MinReactivePower)) node.MinReactivePower = val;
            else if (propertyName == nameof(Node.MaxReactivePower)) node.MaxReactivePower = val;
        }

        private void ApplyRealtimeVisualizationFromTelemetry()
        {
            foreach (var branch in graphicBranches)
            {
                double limit = branch.Data.PermissibleCurrent <= 0 ? 600 : branch.Data.PermissibleCurrent;
                if (branch.Data.CalculatedCurrent > 0)
                {
                    branch.Data.LoadingPercent = limit > 0 ? (branch.Data.CalculatedCurrent / limit) * 100.0 : 0;
                    branch.LoadColor = GetBranchLoadColor(branch.Data.LoadingPercent);
                }
            }

            foreach (var node in graphicElements.OfType<GraphicNode>())
            {
                double uNom = node.Data.InitialVoltage;
                double uFact = node.Data.ActualVoltage > 0 ? node.Data.ActualVoltage : uNom;
                if (uNom > 0 && uFact > 0)
                {
                    double uCalc = node.Data.CalculatedVoltage > 0 ? node.Data.CalculatedVoltage : uFact;
                    node.VoltageColor = GetNodeVoltageColor(uFact, uCalc);
                }
            }

            foreach (var baseNode in graphicElements.OfType<GraphicBaseNode>())
            {
                double uNom = baseNode.Data.InitialVoltage;
                double uFact = baseNode.Data.ActualVoltage > 0 ? baseNode.Data.ActualVoltage : uNom;
                if (uNom > 0 && uFact > 0)
                {
                    double uCalc = baseNode.Data.CalculatedVoltage > 0 ? baseNode.Data.CalculatedVoltage : uFact;
                    baseNode.VoltageColor = GetNodeVoltageColor(uFact, uCalc);
                }
            }

            RefreshElementsGrid();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SetLegacyClientControlsVisibility(false);
            StartClock();
            StartGlobalTelemetryPolling();
            RefreshElementsGrid();
            UpdateCalculationButtonUi();
        }

        private void SetLegacyClientControlsVisibility(bool visible)
        {
            labelAdapter.Visible = visible;
            comboBoxAdapters.Visible = visible;
            labelIp.Visible = visible;
            textBoxStaticIp.Visible = visible;
            labelMask.Visible = visible;
            textBoxMask.Visible = visible;
            labelGateway.Visible = visible;
            textBoxGateway.Visible = visible;
            buttonApplyStaticIp.Visible = visible;
            labelTopClock.Visible = visible;
        }
    }
}
