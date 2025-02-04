using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfLocalLLM
{
    public class RoleToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as string) == "我(user): "
                ? new SolidColorBrush(Color.FromArgb(20, 135, 206, 235))
                : new SolidColorBrush(Color.FromArgb(20, 144, 238, 144));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
