using System;
using System.Globalization;
using System.Windows.Data;
using JobManagementApp.Models;

namespace JobManagementApp.Helpers
{
    public class ExecutionToJapaneseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is emExecution execution)
            {
                switch (execution)
                {
                    case emExecution.AUTO:
                        return "自動起動（exe）";
                    case emExecution.UNYO:
                        return "運用処理管理R 更新";
                    case emExecution.CMD:
                        return "コマンドプロンプト";
                    case emExecution.ETC:
                        return "その他";
                }
            }
            return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value as string)
            {
                case "自動起動（exe）":
                    return emExecution.AUTO;
                case "運用処理管理R 更新":
                    return emExecution.UNYO;
                case "コマンドプロンプト":
                    return emExecution.CMD;
                case "その他":
                    return emExecution.ETC;
            }
            return value;
        }
    }
    public class StatusToJapaneseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is emStatus execution)
            {
                switch (execution)
                {
                    case emStatus.WAIT:
                        return "待機中";
                    case emStatus.RUN:
                        return "実行中";
                    case emStatus.SUCCESS:
                        return "正常終了";
                    case emStatus.ERROR:
                        return "異常終了";
                }
            }
            return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value as string)
            {
                case "待機中":
                    return emStatus.WAIT;
                case "実行中":
                    return emStatus.RUN;
                case "正常終了":
                    return emStatus.SUCCESS;
                case "異常終了":
                    return emStatus.ERROR;
            }
            return value;
        }
    }

    public class FileTypeToJapaneseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is emFileType fileType)
            {
                switch (fileType)
                {
                    case emFileType.LOG:
                        return "ログ";
                    case emFileType.RECEIVE:
                        return "受信";
                    case emFileType.SEND:
                        return "送信";
                    case emFileType.ETC:
                        return "その他";
                }
            }
            return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value as string)
            {
                case "ログ":
                    return emFileType.LOG;
                case "受信":
                    return emFileType.RECEIVE;
                case "送信":
                    return emFileType.SEND;
                case "その他":
                    return emFileType.ETC;
            }
            return value;
        }
    }

    public class ObserverStatusToJapaneseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is emObserverStatus execution)
            {
                switch (execution)
                {
                    case emObserverStatus.SUCCESS:
                        return "取得済";
                    case emObserverStatus.OBSERVER:
                        return "監視中";
                    case emObserverStatus.STOP:
                        return "停止中";
                }
            }
            return value;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value as string)
            {
                case "取得済":
                    return emObserverStatus.SUCCESS;
                case "監視中":
                    return emObserverStatus.OBSERVER;
                case "停止中":
                    return emObserverStatus.STOP;
            }
            return value;
        }
    }
}
