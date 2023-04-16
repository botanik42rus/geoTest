using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace geoTest
{
    internal class GeoViewModel : BaseViewModel
    {
        public GeoViewModel()
        {
            locker = new object();
        }

        public DrawableObject SelectedObject;

        private string _filePath;
        public string FilePath
        {
            get =>_filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged(nameof(FilePath));
                OpenFile(value);
            }
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        private bool _isSave = false;
        public bool IsSave
        {
            get => _isSave;
            set
            {
                _isSave = value;
                OnPropertyChanged(nameof(IsSave));
            }
        }

        private Command _openFileCommand;
        public Command OpenFileCommand
        {
            get
            {
                return _openFileCommand ?? (new Command(o =>
                {
                    OpenFileDialog ofd = new OpenFileDialog();
                    ofd.Filter = "Text File (*.txt)|*.txt";
                    ofd.Multiselect = false;
                    if (ofd.ShowDialog() == true)
                    {
                        FilePath = ofd.FileName;
                    }
                }
            ));
            }
        }

        private Command _saveFileCommand;
        public Command SaveFileCommand
        {
            get
            {
                return _saveFileCommand ?? (new Command(o =>
                {
                    if (IsSave) return;
                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.Filter = "Text File (*.txt)|*.txt";
                    if(sfd.ShowDialog() == true)
                    {
                        IsSave = true;
                        SelectedObject = null;
                        Thread th = new Thread(SaveFile);
                        th.IsBackground = true;
                        th.Start(sfd.FileName);
                    }
                }
            ));
            }
        }

        object locker;
        void SaveFile(object fileName)
        {
            lock (locker)
            {
                File.WriteAllText((string)fileName, SavePointToString());
                IsSave = false;
            }
        }

        private Command _removeElementCommand;
        public Command RemoveElementCommand
        {
            get
            {
                return _removeElementCommand ?? (new Command(o =>
                {
                    if (SelectedObject != null)
                    {
                        DrawableObjects.Remove(DrawableObjects.Where(i => i.Polygon == SelectedObject.Polygon).FirstOrDefault());

                        if (SelectedObject.Polygon is Ellipse)
                        {
                            MainWindow.currentMap.Children.Remove((Ellipse)SelectedObject.Polygon);
                        }
                        else if (SelectedObject.Polygon is Polygon)
                        {
                            MainWindow.currentMap.Children.Remove((Polygon)SelectedObject.Polygon);
                        }
                    }

                    SelectedObject = null;
                }));
            }
        }

        private List<DrawableObject> _drawableObjects;
        public List<DrawableObject> DrawableObjects
        {
            get => _drawableObjects;
            set
            {
                ClearMap();
                _drawableObjects = value;
                if(value != null) DrawPolygons();
            }
        }
        void ClearMap()
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                MainWindow.currentMap.Children.Clear();
                MainWindow.currentMap.Height = 0;
                MainWindow.currentMap.Width = 0;
            });
        }

        bool IsDataPartialCorrupted = false;
        private void OpenFile(string path)
        {
            try
            {
                DrawableObjects = getItems(File.ReadAllLines(path));
                StatusMessage = !IsDataPartialCorrupted ?  "Документ прочитан без ошибок." : 
                    "В данных файла имеются недопустимые символы или колличество точек в некоторых строках не кратно 2.";
            }
            catch(FileNotFoundException fileEx)
            {
                DrawableObjects = null;
                StatusMessage = "Не удалось загрузить файл. Проверьте путь и формат.";
            }
        }

        string SavePointToString()
        {
            string pointsString = "";
            foreach(var item in DrawableObjects)
            {
                for(int i = 0; i < item.coordinateList.Count; i++)
                {
                    Point point = item.coordinateList[i];
                    pointsString += $"{point.X} {point.Y}";
                    if (i != item.coordinateList.Count - 1) pointsString += " ";
                }
                pointsString += "\r\n";
            }

            return pointsString;
        }

        public List<DrawableObject> getItems(IEnumerable<string> linesArray)
        {
            if (linesArray.Count() == 0) return null;
            List<DrawableObject> drawables = new List<DrawableObject>();
            int iter = 0;
            foreach (string line in linesArray)
            {
                DrawableObject currentObject = new DrawableObject();

                string[] points = line.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                if (points.Length % 2 != 0) 
                {
                    IsDataPartialCorrupted = true;
                    continue;
                }

                for (int i = 0; i < points.Length - 1; i += 2)
                {
                    try
                    {
                        currentObject.coordinateList.Add(new Point(Int32.Parse(points[i]), Int32.Parse(points[i + 1])));
                    }
                    catch
                    {
                        IsDataPartialCorrupted=true;
                    }
                }


                Random random = new Random();
                if (currentObject.coordinateList.Count == 1)
                {
                    Ellipse dot = new Ellipse();
                    dot.Stroke = new SolidColorBrush(Colors.Black);
                    dot.StrokeThickness = 3;
                    dot.Fill = new SolidColorBrush(Color.FromRgb((byte)random.Next(1, 255), (byte)random.Next(1, 255), (byte)random.Next(1, 255)));
                    dot.Height = 10;
                    dot.Width = 10;
                    dot.MouseDown += Polygon_MouseDown;
                    Canvas.SetLeft(dot, currentObject.coordinateList.FirstOrDefault().X);
                    Canvas.SetTop(dot, currentObject.coordinateList.FirstOrDefault().Y);
                    Canvas.SetZIndex(dot, iter);
                    currentObject.Polygon = dot;
                }
                else
                {
                    Polygon polygon = new Polygon();
                    polygon.Stroke = new SolidColorBrush(Colors.Black);
                    polygon.Fill = new SolidColorBrush(Color.FromRgb((byte)random.Next(1, 255), (byte)random.Next(1, 255), (byte)random.Next(1, 255)));
                    polygon.StrokeThickness = 2;
                    PointCollection pointCollection = new PointCollection();
                    foreach (Point point in currentObject.coordinateList)
                    {
                        pointCollection.Add(point);
                    }
                    polygon.Points = pointCollection;
                    Canvas.SetZIndex(polygon, iter);
                    polygon.MouseDown += Polygon_MouseDown;
                    currentObject.Polygon = polygon;
                }
                drawables.Add(currentObject);
            }
            return drawables;
        }

        void DrawPolygons()
        {
            int iter = 0;
            foreach(var drawableObject in DrawableObjects)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    if(drawableObject.Polygon is Ellipse)
                    {
                        Ellipse dot = (Ellipse)drawableObject.Polygon;
                        MainWindow.currentMap.Height = MainWindow.currentMap.Height < drawableObject.coordinateList.FirstOrDefault().Y ? drawableObject.coordinateList.FirstOrDefault().Y : MainWindow.currentMap.Height;
                        MainWindow.currentMap.Width = MainWindow.currentMap.Width < drawableObject.coordinateList.FirstOrDefault().X ? drawableObject.coordinateList.FirstOrDefault().X : MainWindow.currentMap.Width;
                        MainWindow.currentMap.Children.Add(dot);
                    }
                    else if(drawableObject.Polygon is Polygon)
                    {
                        Polygon polygon = (Polygon)drawableObject.Polygon;
                        foreach (Point point in drawableObject.coordinateList)
                        {
                            MainWindow.currentMap.Height = MainWindow.currentMap.Height < point.Y ? point.Y : MainWindow.currentMap.Height;
                            MainWindow.currentMap.Width = MainWindow.currentMap.Width < point.X ? point.X : MainWindow.currentMap.Width;
                        }
                        MainWindow.currentMap.Children.Add(polygon);
                    }
                    iter++;
                });
            }
        }

        private void Polygon_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if(SelectedObject != null)
            {
                if(SelectedObject.Polygon is Polygon)
                {
                    Polygon poly = (Polygon)SelectedObject.Polygon;
                    poly.Stroke = new SolidColorBrush(Colors.Black);
                }
                else if(SelectedObject.Polygon is Ellipse)
                {
                    Ellipse ellipse = (Ellipse)SelectedObject.Polygon;
                    ellipse.Stroke = new SolidColorBrush(Colors.Black);
                }
            }
            if(sender is Polygon)
            {
                Polygon polygon = (Polygon)sender;
                polygon.Stroke = new SolidColorBrush(Colors.Red);
                SelectedObject = DrawableObjects.Where(x=>x.Polygon == polygon).LastOrDefault();
            }
            else if(sender is Ellipse)
            {
                Ellipse ellipse = (Ellipse)sender;
                ellipse.Stroke = new SolidColorBrush(Colors.Red);
                SelectedObject = DrawableObjects.Where(x => x.Polygon == ellipse).LastOrDefault();
            }
        }
    }
}
