using Avalonia.Controls;
using DiplomApp.ViewModels;
using System;
using System.Text;
using Avalonia;
using DiplomApp.ViewModels;
using Avalonia.Data.Converters;
using System.Globalization;
using Avalonia.Markup.Xaml.Styling;

namespace DiplomApp.Views 
{ 
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void LoadStyles(string uri)
        {
            var style = new StyleInclude(new Uri("resm:Styles?assembly=DiplomApp"))
            {
                Source = new Uri(uri)
            };

            // Очистка текущих стилей
            Styles.Clear();
            // Применение нового файла стилей
            Styles.Add(style);
        }

    }

    public class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value!;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

