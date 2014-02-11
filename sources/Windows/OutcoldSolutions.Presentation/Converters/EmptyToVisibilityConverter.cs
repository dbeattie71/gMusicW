namespace OutcoldSolutions.Converters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class EmptyToVisibilityConverter : VisibilityConverterBase 
    {
        public override object Convert(object value, Type targetType, object parameter, string language)
        {
            var enumerable = value as IEnumerable<object>;
            if (enumerable != null)
            {
                return this.ConvertToVisibility(!enumerable.Any());
            }

            return this.ConvertToVisibility(true);
        }

        public override object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
