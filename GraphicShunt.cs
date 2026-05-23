using System;
using System.Drawing;

namespace PowerGridEditor
{
    public class GraphicShunt
    {
        public Shunt Data { get; set; }
        public object ConnectedNode { get; set; }
        public Point Location { get; set; }
        public static readonly Size ShuntSize = new Size(25, 25); // Квадратный размер
        public bool IsSelected { get; set; }

        public GraphicShunt(Shunt data, object connectedNode)
        {
            Data = data;
            ConnectedNode = connectedNode;

            UpdatePosition();
            IsSelected = false;
        }

        public void Draw(Graphics g)
        {
            if (ConnectedNode == null) return;

            // Рисуем шунт в виде квадрата
            Brush fillBrush = IsSelected ? Brushes.LightGreen : Brushes.Orange;
            Pen borderPen = IsSelected ? new Pen(Color.Red, 2) : new Pen(Color.Black, 1);

            Rectangle shuntRect = new Rectangle(Location, ShuntSize);

            g.FillRectangle(fillBrush, shuntRect);
            g.DrawRectangle(borderPen, shuntRect);
            borderPen.Dispose();

            // ЛИНИЯ БОЛЬШЕ НЕ РИСУЕТСЯ ЗДЕСЬ - она рисуется отдельно в Panel2_Paint

        }

        private void DrawShuntInfo(Graphics g)
        {
            string info = $"X={Data.ReactiveResistance}";

            using (Font font = new Font("Arial", 7))
            using (Brush brush = new SolidBrush(Color.DarkBlue))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                Point shuntCenter = new Point(
                    Location.X + ShuntSize.Width / 2,
                    Location.Y + ShuntSize.Height / 2
                );
                // Рисуем надпись над шунтом
                g.DrawString(info, font, brush, shuntCenter.X, shuntCenter.Y - 20, format);
            }
        }


        public bool Contains(Point point)
        {
            return new Rectangle(Location, ShuntSize).Contains(point);
        }

        public void UpdatePosition()
        {
            // Обновляем позицию относительно центра узла
            if (ConnectedNode != null)
            {
                Point nodeCenter = NodeGraphicsHelper.GetNodeCenter(ConnectedNode);

                // Размещаем шунт справа от узла на фиксированном расстоянии
                Location = new Point(
                    nodeCenter.X + 40, // Отступ от центра узла
                    nodeCenter.Y - ShuntSize.Height / 2 // Центрируем по вертикали
                );
            }
        }

        // Метод для получения номера подключенного узла
        public int GetConnectedNodeNumber()
        {
            return NodeGraphicsHelper.GetNodeNumber(ConnectedNode);
        }
    }
}