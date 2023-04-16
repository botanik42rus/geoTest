using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;

namespace geoTest
{
    class DrawableObject
    {
        public List<Point> coordinateList;
        public object Polygon;
        public DrawableObject()
        {
            coordinateList = new List<Point>();
        }
    }

}
