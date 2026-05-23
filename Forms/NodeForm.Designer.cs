namespace PowerGridEditor
{
    partial class NodeForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabParams, tabSettings;
        private System.Windows.Forms.Button btnCheckIP, buttonSave, buttonCancel;
        private System.Windows.Forms.ComboBox comboBoxProtocol;
        private System.Windows.Forms.TextBox textBoxIP, textBoxPort, textBoxID;

        // Массивы для удобного управления строками параметров
        private System.Windows.Forms.TextBox[] paramBoxes;
        private System.Windows.Forms.CheckBox[] paramChecks;
        private System.Windows.Forms.TextBox[] addrBoxes;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabParams = new System.Windows.Forms.TabPage { Text = "Параметры узла" };
            this.tabSettings = new System.Windows.Forms.TabPage { Text = "Настройки связи" };

            this.paramBoxes = new System.Windows.Forms.TextBox[11];
            this.paramChecks = new System.Windows.Forms.CheckBox[11];
            this.addrBoxes = new System.Windows.Forms.TextBox[11];

            this.buttonSave = new System.Windows.Forms.Button { Text = "Сохранить", Size = new System.Drawing.Size(100, 32) };
            this.buttonCancel = new System.Windows.Forms.Button { Text = "Отмена", Size = new System.Drawing.Size(100, 32) };

            this.SuspendLayout();

            // Настройка вкладок
            this.tabControl.Location = new System.Drawing.Point(12, 12);
            this.tabControl.Size = new System.Drawing.Size(760, 470);
            this.tabControl.Controls.Add(tabParams);
            this.tabControl.Controls.Add(tabSettings);

            // Полные названия параметров для отображения
            string[] names = {
                "Номер узла:",
                "Номинальное напряжение (кВ):",
                "Фактическое напряжение (кВ):",
                "Расчётное напряжение (кВ):",
                "Активная мощность (МВт):",
                "Реактивная мощность (Мвар):",
                "Генерация P (МВт):",
                "Генерация Q (Мвар):",
                "Модуль напряжения:",
                "Мин. реактивная мощность:",
                "Макс. реактивная мощность:"
            };

            int y = 20;
            for (int i = 0; i < names.Length; i++)
            {
                // 1. Метка с названием параметра
                var lblName = new System.Windows.Forms.Label
                {
                    Text = names[i],
                    Location = new System.Drawing.Point(10, y + 3),
                    Size = new System.Drawing.Size(180, 20)
                };

                // 2. Основное поле значения (Константа)
                paramBoxes[i] = new System.Windows.Forms.TextBox
                {
                    Location = new System.Drawing.Point(200, y),
                    Size = new System.Drawing.Size(75, 23)
                };

                tabParams.Controls.Add(lblName);
                tabParams.Controls.Add(paramBoxes[i]);

                // 3. Элементы телеметрии (кроме Номера узла - индекс 0)
                if (i > 0)
                {
                    paramChecks[i] = new System.Windows.Forms.CheckBox
                    {
                        Text = "Телеметрия",
                        Location = new System.Drawing.Point(285, y + 2),
                        Size = new System.Drawing.Size(100, 20)
                    };

                    var lblAddr = new System.Windows.Forms.Label
                    {
                        Text = "Адрес:",
                        Location = new System.Drawing.Point(395, y + 3),
                        Size = new System.Drawing.Size(50, 20)
                    };

                    addrBoxes[i] = new System.Windows.Forms.TextBox
                    {
                        Location = new System.Drawing.Point(450, y),
                        Size = new System.Drawing.Size(60, 23)
                    };

                    tabParams.Controls.Add(paramChecks[i]);
                    tabParams.Controls.Add(lblAddr);
                    tabParams.Controls.Add(addrBoxes[i]);
                }

                y += 35; // Шаг по вертикали для следующей строки
            }

            // Настройка вкладки "Связь"
            SetupSettingsTab();

            // Кнопки управления формой
            this.buttonSave.Location = new System.Drawing.Point(530, 500);
            this.buttonCancel.Location = new System.Drawing.Point(645, 500);

            this.ClientSize = new System.Drawing.Size(784, 550);
            this.Controls.AddRange(new System.Windows.Forms.Control[] { tabControl, buttonSave, buttonCancel });
            this.Text = "Редактор параметров узла";
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;

            this.ResumeLayout(false);
        }

        private void SetupSettingsTab()
        {
            int leftLabel = 20;
            int leftInput = 140;
            int top = 30;
            int rowGap = 42;

            var lblProt = new System.Windows.Forms.Label { Text = "Протокол:", Location = new System.Drawing.Point(leftLabel, top + 3), Size = new System.Drawing.Size(110, 23) };
            comboBoxProtocol = new System.Windows.Forms.ComboBox
            {
                Location = new System.Drawing.Point(leftInput, top),
                Size = new System.Drawing.Size(190, 23),
                DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
            };
            comboBoxProtocol.Items.AddRange(new object[] { "Modbus TCP", "МЭК-104" });
            tabSettings.Controls.AddRange(new System.Windows.Forms.Control[] { lblProt, comboBoxProtocol });

            top += rowGap;
            var lblIp = new System.Windows.Forms.Label { Text = "IP адрес:", Location = new System.Drawing.Point(leftLabel, top + 3), Size = new System.Drawing.Size(110, 23) };
            textBoxIP = new System.Windows.Forms.TextBox { Location = new System.Drawing.Point(leftInput, top), Size = new System.Drawing.Size(190, 23) };
            btnCheckIP = new System.Windows.Forms.Button { Text = "Пинг", Location = new System.Drawing.Point(leftInput + 205, top - 1), Size = new System.Drawing.Size(100, 25) };
            tabSettings.Controls.AddRange(new System.Windows.Forms.Control[] { lblIp, textBoxIP, btnCheckIP });

            top += rowGap;
            var lblPort = new System.Windows.Forms.Label { Text = "TCP Порт:", Location = new System.Drawing.Point(leftLabel, top + 3), Size = new System.Drawing.Size(110, 23) };
            textBoxPort = new System.Windows.Forms.TextBox { Location = new System.Drawing.Point(leftInput, top), Size = new System.Drawing.Size(100, 23) };
            tabSettings.Controls.AddRange(new System.Windows.Forms.Control[] { lblPort, textBoxPort });

            top += rowGap;
            var lblId = new System.Windows.Forms.Label { Text = "ID устройства:", Location = new System.Drawing.Point(leftLabel, top + 3), Size = new System.Drawing.Size(110, 23) };
            textBoxID = new System.Windows.Forms.TextBox { Location = new System.Drawing.Point(leftInput, top), Size = new System.Drawing.Size(100, 23) };
            tabSettings.Controls.AddRange(new System.Windows.Forms.Control[] { lblId, textBoxID });

            top += rowGap;
            var lblInterval = new System.Windows.Forms.Label { Text = "Интервал измерения\r\n(сек):", Location = new System.Drawing.Point(leftLabel, top + 1), Size = new System.Drawing.Size(150, 34) };
            numericMeasurementInterval = new System.Windows.Forms.NumericUpDown
            {
                Location = new System.Drawing.Point(leftInput, top),
                Size = new System.Drawing.Size(100, 23),
                Minimum = 1,
                Maximum = 3600
            };
            tabSettings.Controls.AddRange(new System.Windows.Forms.Control[] { lblInterval, numericMeasurementInterval });
        }

    }
}
