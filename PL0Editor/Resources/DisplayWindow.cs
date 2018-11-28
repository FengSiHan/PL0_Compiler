using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PL0Editor
{
    class DisplayWindow : Window
    {
        private TextBox box;
        public DisplayWindow(MainWindow parent)
        {
            Init(parent);
        }
        public new void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Hide();
            e.Cancel = true;
        }
        private void Init(MainWindow parent)
        {
            FontFamily = new FontFamily("Consolas");
            Height = 400;
            Width = 600;
            Owner = parent;
            //Left = parent.Width - Width + 100;
            //Top = 200;
            Padding = new Thickness(0);
            this.WindowStyle = WindowStyle.None;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            Grid grid = new Grid();
            grid.Margin = new Thickness(0, 20, 0, 0);

            RowDefinition row = new RowDefinition();
            RowDefinition row1 = new RowDefinition();
            grid.RowDefinitions.Add(row);
            grid.RowDefinitions.Add(row1);
            row1.Height = new GridLength(50);

            //Background = new SolidColorBrush(Color.FromRgb(0x1c,0x97,0xcc));

            box = new TextBox();
            box.Margin = new Thickness(1.5);
            box.FontFamily = new FontFamily("Consolas");
            box.IsReadOnly = true;
            box.BorderBrush = new SolidColorBrush(Colors.Transparent);
            box.BorderThickness = new Thickness(0);
            box.TextWrapping = TextWrapping.Wrap;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            box.VerticalAlignment = VerticalAlignment.Stretch;
            box.HorizontalAlignment = HorizontalAlignment.Stretch;

            Button button = new Button
            {
                //button.Width = 70;
                //button.Background = new SolidColorBrush(Color.FromRgb(0x1c, 0x97, 0xcc));
                Content = "OK",
                BorderThickness = new Thickness(0),
                BorderBrush = new SolidColorBrush(Colors.Transparent)
            };


            grid.Children.Add(button);
            grid.Children.Add(box);
            Grid.SetRow(box, 0);
            Grid.SetRow(button, 1);
            AddChild(grid);

            button.Click += (i, j) => Hide();

        }
        public bool? Show(string text)
        {
            box.Text = text;
            return this.ShowDialog();
        }
    }
}
