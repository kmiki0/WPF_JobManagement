using System;
using System.Globalization;
using System.Windows.Data;

namespace JobManagementApp.Helpers
{
    public class IntToRadioButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is int intValue && parameter is string parameterString)
                {
                    if (int.TryParse(parameterString, out int targetValue))
                    {
                        return intValue == targetValue;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                // ログ出力（必要に応じて）
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is bool isChecked && isChecked && parameter is string parameterString)
                {
                    if (int.TryParse(parameterString, out int targetValue))
                    {
                        return targetValue;
                    }
                }
                return Binding.DoNothing;
            }
            catch (Exception ex)
            {
                // ログ出力（必要に応じて）
                return Binding.DoNothing;
            }
        }
    }
}