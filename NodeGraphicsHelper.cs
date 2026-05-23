using System.Drawing;

namespace PowerGridEditor
{
    public static class NodeGraphicsHelper
    {
        public static Point GetNodeCenter(object node)
        {
            if (node is GraphicNode graphicNode)
            {
                return new Point(
                    graphicNode.Location.X + GraphicNode.NodeSize.Width / 2,
                    graphicNode.Location.Y + GraphicNode.NodeSize.Height / 2
                );
            }

            if (node is GraphicBaseNode graphicBaseNode)
            {
                return new Point(
                    graphicBaseNode.Location.X + GraphicBaseNode.NodeSize.Width / 2,
                    graphicBaseNode.Location.Y + GraphicBaseNode.NodeSize.Height / 2
                );
            }

            return Point.Empty;
        }

        public static int GetNodeNumber(object node)
        {
            if (node is GraphicNode graphicNode)
            {
                return graphicNode.Data.Number;
            }

            if (node is GraphicBaseNode graphicBaseNode)
            {
                return graphicBaseNode.Data.Number;
            }

            return 0;
        }
    }
}
