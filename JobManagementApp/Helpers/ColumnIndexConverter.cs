﻿using System;
using System.Globalization;
using System.Windows.Data;

namespace JobManagementApp.Helpers
{
    public class ColumnIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int index = (int)value;
            // 3列の場合
            return index % 3;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
