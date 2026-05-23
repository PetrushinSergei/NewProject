using System.Drawing;

namespace PowerGridEditor
{
    public class GraphicNode
    {
        public Node Data { get; set; }
        public Point Location { get; set; }
        public static readonly Size NodeSize = new Size(60, 60);
        public bool IsSelected { get; set; }
        public Color VoltageColor { get; set; }

        public GraphicNode(Node data, Point location)
        {
            Data = data;
            Location = location;
            IsSelected = false;
            VoltageColor = Color.LightBlue;
        }

        public void Draw(Graphics g)
        {
            // Рисуем узел
            using (Brush fillBrush = new SolidBrush(IsSelected ? Color.LightGreen : VoltageColor))
            {
                g.FillEllipse(fillBrush, new Rectangle(Location, NodeSize));
            }
            g.DrawEllipse(Pens.Black, new Rectangle(Location, NodeSize));
            string nodeMode = Data.FixedVoltageModule == 0 ? "PQ" : "PU";
            DrawNodeTypeLabel(g, nodeMode);

            // Рисуем номер узла
            using (Font font = new Font("Arial", 10, FontStyle.Bold))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;

                using (Brush textBrush = new SolidBrush(Color.Black))
                {
                    g.DrawString(
                        Data.Number.ToString(),
                        font,
                        textBrush,
                        new Rectangle(Location, NodeSize),
                        format
                    );
                }
            }
        }

        private void DrawNodeTypeLabel(Graphics g, string nodeType)
        {
            using (Font modeFont = new Font("Arial", 8, FontStyle.Bold))
            using (Brush modeBrush = new SolidBrush(Color.Black))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(230, Color.White)))
            {
                var textSize = g.MeasureString(nodeType, modeFont);
                int padding = 3;
                float anchorX = Location.X + NodeSize.Width + 6;
                float anchorY = Location.Y + 6;

                var state = g.Transform;
                g.TranslateTransform(anchorX, anchorY);
                g.RotateTransform(-45f);
                var bgRect = new RectangleF(-padding, -textSize.Height - 2, textSize.Width + padding * 2, textSize.Height + 2);
                g.FillRectangle(bgBrush, bgRect);
                g.DrawString(nodeType, modeFont, modeBrush, 0, -textSize.Height - 1);
                g.Transform = state;
            }
        }

        public bool Contains(Point point)
        {
            return new Rectangle(Location, NodeSize).Contains(point);
        }
    }
}
