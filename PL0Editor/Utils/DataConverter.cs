using System;
using System.Globalization;
using System.Windows.Data;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace PL0Editor
{
    [ValueConversion(typeof(Compiler.Position), typeof(string))]
    public class PositionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Compiler.Position pos = value as Compiler.Position;
            return pos.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
