namespace PowerGridEditor
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.panel1 = new System.Windows.Forms.Panel();
            this.labelGateway = new System.Windows.Forms.Label();
            this.labelMask = new System.Windows.Forms.Label();
            this.labelIp = new System.Windows.Forms.Label();
            this.labelAdapter = new System.Windows.Forms.Label();
            this.buttonApplyStaticIp = new System.Windows.Forms.Button();
            this.textBoxGateway = new System.Windows.Forms.TextBox();
            this.textBoxMask = new System.Windows.Forms.TextBox();
            this.textBoxStaticIp = new System.Windows.Forms.TextBox();
            this.comboBoxAdapters = new System.Windows.Forms.ComboBox();
            this.buttonImportData = new System.Windows.Forms.Button();
            this.buttonOpenReport = new System.Windows.Forms.Button();
            this.buttonExportData = new System.Windows.Forms.Button();
            this.buttonClearAll = new System.Windows.Forms.Button();
            this.buttonDelete = new System.Windows.Forms.Button();
            this.buttonAddShunt = new System.Windows.Forms.Button();
            this.buttonAddBranch = new System.Windows.Forms.Button();
            this.buttonAddBaseNode = new System.Windows.Forms.Button();
            this.buttonAddNode = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabelClock = new System.Windows.Forms.ToolStripStatusLabel();
            this.labelTopClock = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(208)))), ((int)(((byte)(224)))), ((int)(((byte)(241)))));
            this.panel1.Controls.Add(this.labelTopClock);
            this.panel1.Controls.Add(this.labelGateway);
            this.panel1.Controls.Add(this.labelMask);
            this.panel1.Controls.Add(this.labelIp);
            this.panel1.Controls.Add(this.labelAdapter);
            this.panel1.Controls.Add(this.buttonApplyStaticIp);
            this.panel1.Controls.Add(this.textBoxGateway);
            this.panel1.Controls.Add(this.textBoxMask);
            this.panel1.Controls.Add(this.textBoxStaticIp);
            this.panel1.Controls.Add(this.comboBoxAdapters);
            this.panel1.Controls.Add(this.buttonImportData);
            this.panel1.Controls.Add(this.buttonOpenReport);
            this.panel1.Controls.Add(this.buttonExportData);
            this.panel1.Controls.Add(this.buttonClearAll);
            this.panel1.Controls.Add(this.buttonDelete);
            this.panel1.Controls.Add(this.buttonAddShunt);
            this.panel1.Controls.Add(this.buttonAddBranch);
            this.panel1.Controls.Add(this.buttonAddBaseNode);
            this.panel1.Controls.Add(this.buttonAddNode);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1500, 92);
            this.panel1.TabIndex = 0;
            // 
            // labelTopClock
            // 
            this.labelTopClock.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelTopClock.AutoSize = true;
            this.labelTopClock.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.labelTopClock.Location = new System.Drawing.Point(1285, 8);
            this.labelTopClock.Name = "labelTopClock";
            this.labelTopClock.Size = new System.Drawing.Size(106, 19);
            this.labelTopClock.TabIndex = 18;
            this.labelTopClock.Text = "00:00:00";
            // 
            // 
            // labelGateway
            // 
            this.labelGateway.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelGateway.AutoSize = true;
            this.labelGateway.Location = new System.Drawing.Point(1285, 60);
            this.labelGateway.Name = "labelGateway";
            this.labelGateway.Size = new System.Drawing.Size(44, 13);
            this.labelGateway.TabIndex = 17;
            this.labelGateway.Text = "Шлюз:";
            // 
            // labelMask
            // 
            this.labelMask.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelMask.AutoSize = true;
            this.labelMask.Location = new System.Drawing.Point(1144, 60);
            this.labelMask.Name = "labelMask";
            this.labelMask.Size = new System.Drawing.Size(42, 13);
            this.labelMask.TabIndex = 16;
            this.labelMask.Text = "Маска:";
            // 
            // labelIp
            // 
            this.labelIp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelIp.AutoSize = true;
            this.labelIp.Location = new System.Drawing.Point(998, 60);
            this.labelIp.Name = "labelIp";
            this.labelIp.Size = new System.Drawing.Size(20, 13);
            this.labelIp.TabIndex = 15;
            this.labelIp.Text = "IP:";
            // 
            // labelAdapter
            // 
            this.labelAdapter.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelAdapter.AutoSize = true;
            this.labelAdapter.Location = new System.Drawing.Point(998, 35);
            this.labelAdapter.Name = "labelAdapter";
            this.labelAdapter.Size = new System.Drawing.Size(56, 13);
            this.labelAdapter.TabIndex = 14;
            this.labelAdapter.Text = "Адаптер:";
            // 
            // buttonApplyStaticIp
            // 
            this.buttonApplyStaticIp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonApplyStaticIp.FlatAppearance.BorderSize = 2;
            this.buttonApplyStaticIp.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonApplyStaticIp.Location = new System.Drawing.Point(1406, 30);
            this.buttonApplyStaticIp.Name = "buttonApplyStaticIp";
            this.buttonApplyStaticIp.Size = new System.Drawing.Size(86, 48);
            this.buttonApplyStaticIp.TabIndex = 13;
            this.buttonApplyStaticIp.Text = "Применить IP";
            this.buttonApplyStaticIp.UseVisualStyleBackColor = true;
            this.buttonApplyStaticIp.Click += new System.EventHandler(this.buttonApplyStaticIp_Click);
            // 
            // textBoxGateway
            // 
            this.textBoxGateway.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxGateway.Location = new System.Drawing.Point(1335, 57);
            this.textBoxGateway.Name = "textBoxGateway";
            this.textBoxGateway.Size = new System.Drawing.Size(65, 20);
            this.textBoxGateway.TabIndex = 12;
            // 
            // textBoxMask
            // 
            this.textBoxMask.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxMask.Location = new System.Drawing.Point(1192, 57);
            this.textBoxMask.Name = "textBoxMask";
            this.textBoxMask.Size = new System.Drawing.Size(87, 20);
            this.textBoxMask.TabIndex = 11;
            this.textBoxMask.Text = "255.255.255.0";
            // 
            // textBoxStaticIp
            // 
            this.textBoxStaticIp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxStaticIp.Location = new System.Drawing.Point(1024, 57);
            this.textBoxStaticIp.Name = "textBoxStaticIp";
            this.textBoxStaticIp.Size = new System.Drawing.Size(114, 20);
            this.textBoxStaticIp.TabIndex = 10;
            // 
            // comboBoxAdapters
            // 
            this.comboBoxAdapters.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxAdapters.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxAdapters.FormattingEnabled = true;
            this.comboBoxAdapters.Location = new System.Drawing.Point(1060, 30);
            this.comboBoxAdapters.Name = "comboBoxAdapters";
            this.comboBoxAdapters.Size = new System.Drawing.Size(340, 21);
            this.comboBoxAdapters.TabIndex = 9;
            // 
            // buttonImportData
            // 
            this.buttonImportData.FlatAppearance.BorderSize = 2;
            this.buttonImportData.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonImportData.Location = new System.Drawing.Point(806, 12);
            this.buttonImportData.Name = "buttonImportData";
            this.buttonImportData.Size = new System.Drawing.Size(90, 30);
            this.buttonImportData.TabIndex = 7;
            this.buttonImportData.Text = "Импорт";
            this.buttonImportData.UseVisualStyleBackColor = true;
            this.buttonImportData.Click += new System.EventHandler(this.buttonImportData_Click);
            // 
            // buttonOpenReport
            // 
            this.buttonOpenReport.FlatAppearance.BorderSize = 2;
            this.buttonOpenReport.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonOpenReport.Location = new System.Drawing.Point(902, 12);
            this.buttonOpenReport.Name = "buttonOpenReport";
            this.buttonOpenReport.Size = new System.Drawing.Size(90, 30);
            this.buttonOpenReport.TabIndex = 8;
            this.buttonOpenReport.Text = "Расчёт";
            this.buttonOpenReport.UseVisualStyleBackColor = true;
            this.buttonOpenReport.Click += new System.EventHandler(this.buttonOpenReport_Click);
            // 
            // buttonExportData
            // 
            this.buttonExportData.FlatAppearance.BorderSize = 2;
            this.buttonExportData.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonExportData.Location = new System.Drawing.Point(710, 12);
            this.buttonExportData.Name = "buttonExportData";
            this.buttonExportData.Size = new System.Drawing.Size(90, 30);
            this.buttonExportData.TabIndex = 6;
            this.buttonExportData.Text = "Экспорт";
            this.buttonExportData.UseVisualStyleBackColor = true;
            this.buttonExportData.Click += new System.EventHandler(this.buttonExportData_Click);
            // 
            // buttonClearAll
            // 
            this.buttonClearAll.FlatAppearance.BorderSize = 2;
            this.buttonClearAll.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonClearAll.Location = new System.Drawing.Point(614, 12);
            this.buttonClearAll.Name = "buttonClearAll";
            this.buttonClearAll.Size = new System.Drawing.Size(90, 30);
            this.buttonClearAll.TabIndex = 5;
            this.buttonClearAll.Text = "Очистить";
            this.buttonClearAll.UseVisualStyleBackColor = true;
            this.buttonClearAll.Click += new System.EventHandler(this.buttonClearAll_Click);
            // 
            // buttonDelete
            // 
            this.buttonDelete.FlatAppearance.BorderSize = 2;
            this.buttonDelete.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonDelete.Location = new System.Drawing.Point(518, 12);
            this.buttonDelete.Name = "buttonDelete";
            this.buttonDelete.Size = new System.Drawing.Size(90, 30);
            this.buttonDelete.TabIndex = 4;
            this.buttonDelete.Text = "Удалить";
            this.buttonDelete.UseVisualStyleBackColor = true;
            this.buttonDelete.Click += new System.EventHandler(this.buttonDelete_Click);
            // 
            // buttonAddShunt
            // 
            this.buttonAddShunt.FlatAppearance.BorderSize = 2;
            this.buttonAddShunt.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonAddShunt.Location = new System.Drawing.Point(422, 12);
            this.buttonAddShunt.Name = "buttonAddShunt";
            this.buttonAddShunt.Size = new System.Drawing.Size(90, 30);
            this.buttonAddShunt.TabIndex = 3;
            this.buttonAddShunt.Text = "Шунт";
            this.buttonAddShunt.UseVisualStyleBackColor = true;
            this.buttonAddShunt.Click += new System.EventHandler(this.buttonAddShunt_Click);
            // 
            // buttonAddBranch
            // 
            this.buttonAddBranch.FlatAppearance.BorderSize = 2;
            this.buttonAddBranch.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonAddBranch.Location = new System.Drawing.Point(326, 12);
            this.buttonAddBranch.Name = "buttonAddBranch";
            this.buttonAddBranch.Size = new System.Drawing.Size(90, 30);
            this.buttonAddBranch.TabIndex = 2;
            this.buttonAddBranch.Text = "Ветвь";
            this.buttonAddBranch.UseVisualStyleBackColor = true;
            this.buttonAddBranch.Click += new System.EventHandler(this.buttonAddBranch_Click);
            // 
            // buttonAddBaseNode
            // 
            this.buttonAddBaseNode.FlatAppearance.BorderSize = 2;
            this.buttonAddBaseNode.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonAddBaseNode.Location = new System.Drawing.Point(180, 12);
            this.buttonAddBaseNode.Name = "buttonAddBaseNode";
            this.buttonAddBaseNode.Size = new System.Drawing.Size(140, 30);
            this.buttonAddBaseNode.TabIndex = 1;
            this.buttonAddBaseNode.Text = "Базисный Узел";
            this.buttonAddBaseNode.UseVisualStyleBackColor = true;
            this.buttonAddBaseNode.Click += new System.EventHandler(this.buttonAddBaseNode_Click);
            // 
            // buttonAddNode
            // 
            this.buttonAddNode.FlatAppearance.BorderSize = 2;
            this.buttonAddNode.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonAddNode.Location = new System.Drawing.Point(12, 12);
            this.buttonAddNode.Name = "buttonAddNode";
            this.buttonAddNode.Size = new System.Drawing.Size(162, 30);
            this.buttonAddNode.TabIndex = 0;
            this.buttonAddNode.Text = "Узел";
            this.buttonAddNode.UseVisualStyleBackColor = true;
            this.buttonAddNode.Click += new System.EventHandler(this.buttonAddNode_Click);
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(0, 92);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(1500, 547);
            this.panel2.TabIndex = 1;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabelClock});
            this.statusStrip1.Location = new System.Drawing.Point(0, 639);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1500, 22);
            this.statusStrip1.TabIndex = 2;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabelClock
            // 
            this.toolStripStatusLabelClock.Name = "toolStripStatusLabelClock";
            this.toolStripStatusLabelClock.Size = new System.Drawing.Size(152, 17);
            this.toolStripStatusLabelClock.Text = "Время: 00:00:00";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1500, 661);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.panel1);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Редактор электрической сети";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.Form1_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button buttonAddNode;
        private System.Windows.Forms.Button buttonClearAll;
        private System.Windows.Forms.Button buttonDelete;
        private System.Windows.Forms.Button buttonAddShunt;
        private System.Windows.Forms.Button buttonAddBranch;
        private System.Windows.Forms.Button buttonAddBaseNode;
        private System.Windows.Forms.Button buttonExportData;
        private System.Windows.Forms.Button buttonImportData;
        private System.Windows.Forms.Button buttonOpenReport;
        private System.Windows.Forms.ComboBox comboBoxAdapters;
        private System.Windows.Forms.TextBox textBoxStaticIp;
        private System.Windows.Forms.TextBox textBoxMask;
        private System.Windows.Forms.TextBox textBoxGateway;
        private System.Windows.Forms.Button buttonApplyStaticIp;
        private System.Windows.Forms.Label labelAdapter;
        private System.Windows.Forms.Label labelIp;
        private System.Windows.Forms.Label labelMask;
        private System.Windows.Forms.Label labelGateway;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelClock;
        private System.Windows.Forms.Label labelTopClock;
    }
}
