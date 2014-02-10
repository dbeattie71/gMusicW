namespace OutcoldSolutions.Converters
{
    using System;

    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Data;

    public class SymbolToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return new SymbolIcon((Symbol)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
