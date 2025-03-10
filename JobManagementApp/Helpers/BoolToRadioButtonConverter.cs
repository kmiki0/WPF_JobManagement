using System;
using System.Windows.Data;
using System.Globalization;

namespace JobManagementApp.Helpers
{
    public class BoolToRadioButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string parameterString)
            {
                return boolValue == bool.Parse(parameterString);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && parameter is string parameterString)
            {
                return isChecked == bool.Parse(parameterString);
            }
            return false;
        }
    }
}
