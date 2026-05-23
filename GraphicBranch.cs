using System;
using System.Drawing;

namespace PowerGridEditor
{
    public class GraphicBranch
    {
        public Branch Data { get; set; }
        public object StartNode { get; set; }
        public object EndNode { get; set; }
        public bool IsSelected { get; set; }
        public Color LoadColor { get; set; } = Color.Black;

        public GraphicBranch(Branch data, object startNode, object endNode)
        {
            Data = data;
            StartNode = startNode;
            EndNode = endNode;
            IsSelected = false;
        }

        public void Draw(Graphics g)
        {
            if (StartNode == null || EndNode == null) return;

            Point startCenter = NodeGraphicsHelper.GetNodeCenter(StartNode);
            Point endCenter = NodeGraphicsHelper.GetNodeCenter(EndNode);

            // Выбираем цвет в зависимости от выделения
            Pen pen = IsSelected ? new Pen(Color.Red, 3) : new Pen(LoadColor, 2);

            // Рисуем линию от центра к центру
            g.DrawLine(pen, startCenter, endCenter);
            pen.Dispose();

        }


        private void DrawBranchInfo(Graphics g, Point start, Point end)
        {
            // Вычисляем середину линии
            Point middle = new Point(
                (start.X + end.X) / 2,
                (start.Y + end.Y) / 2
            );

            string info = $"R={Data.ActiveResistance:F1} X={Data.ReactiveResistance:F2}";

            using (Font font = new Font("Arial", 8))
            using (Brush brush = new SolidBrush(Color.DarkBlue))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                g.DrawString(info, font, brush, middle.X, middle.Y - 15, format);
            }
        }

        public bool Contains(Point point)
        {
            if (StartNode == null || EndNode == null) return false;

            Point start = NodeGraphicsHelper.GetNodeCenter(StartNode);
            Point end = NodeGraphicsHelper.GetNodeCenter(EndNode);

            return IsPointNearLine(point, start, end, 8);
        }

        private bool IsPointNearLine(Point point, Point lineStart, Point lineEnd, double tolerance)
        {
            double distance = DistanceFromPointToLine(point, lineStart, lineEnd);
            return distance <= tolerance;
        }

        private double DistanceFromPointToLine(Point point, Point lineStart, Point lineEnd)
        {
            double numerator = Math.Abs(
                (lineEnd.Y - lineStart.Y) * point.X -
                (lineEnd.X - lineStart.X) * point.Y +
                lineEnd.X * lineStart.Y -
                lineEnd.Y * lineStart.X
            );

            double denominator = Math.Sqrt(
                Math.Pow(lineEnd.Y - lineStart.Y, 2) +
                Math.Pow(lineEnd.X - lineStart.X, 2)
            );

            return numerator / denominator;
        }

        public int GetStartNodeNumber()
        {
            return NodeGraphicsHelper.GetNodeNumber(StartNode);
        }

        public int GetEndNodeNumber()
        {
            return NodeGraphicsHelper.GetNodeNumber(EndNode);
        }
    }
}
