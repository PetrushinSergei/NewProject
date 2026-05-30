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
        private readonly DataGridView nodesGrid;

        public GroupBurdeningForm(IEnumerable<GraphicNode> nodes)
        {
            Text = "Групповое утяжеление";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(640, 360);
            Size = new Size(720, 460);

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

            Controls.Add(nodesGrid);
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
                nodesGrid.Rows.Add(
                    false,
                    node.Data.Number.ToString(CultureInfo.InvariantCulture),
                    node.Data.NominalActivePower.ToString("0.##", CultureInfo.InvariantCulture),
                    node.Data.NominalReactivePower.ToString("0.##", CultureInfo.InvariantCulture),
                    false);
            }
        }

        private static bool IsPqNode(GraphicNode node)
        {
            return node != null
                && node.Data != null
                && Math.Abs(node.Data.FixedVoltageModule) < 1e-9;
        }
    }
}
