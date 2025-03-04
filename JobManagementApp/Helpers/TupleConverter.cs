using JobManagementApp.Models;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows.Data;

namespace JobManagementApp.Helpers
{
    public class TupleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return Tuple.Create(values[0], values[1]);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class JobParamConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length > 2)
            {
                return new JobParamModel
                {
                    Scenario = values[0].ToString(),
                    Eda = values[1].ToString(),
                    FilePath = values[2].ToString(),
                    FileName = values[3].ToString()
                };
            }
            else
            {
                return new JobParamModel
                {
                    Scenario = values[0].ToString(),
                    Eda = values[1].ToString(),
                };
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
}
