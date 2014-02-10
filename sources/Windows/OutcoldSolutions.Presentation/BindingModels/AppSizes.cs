namespace OutcoldSolutions.BindingModels
{
    using Windows.UI.Xaml;

    public class AppSizes : BindingModelBase
    {
        private bool isLarge;
        private bool isMedium;
        private bool isSmall;

        public AppSizes()
        {
            Window.Current.SizeChanged += (sender, args) => this.UpdateApplicationSizes(args.Size.Width);
            this.UpdateApplicationSizes(Window.Current.Bounds.Width);
        }

        public bool IsLarge
        {
            get
            {
                return this.isLarge;
            }
            set
            {
                this.isLarge = value;
                this.RaiseCurrentPropertyChanged();
            }
        }

        public bool IsMedium
        {
            get
            {
                return this.isMedium;
            }
            set
            {
                this.isMedium = value;
                this.RaiseCurrentPropertyChanged();
            }
        }

        public bool IsMediumOrLarge
        {
            get
            {
                return this.IsMedium || this.IsLarge;
            }
        }

        public bool IsMediumOrSmall
        {
            get
            {
                return this.IsMedium || this.IsSmall;
            }
        }

        public bool IsSmall
        {
            get
            {
                return this.isSmall;
            }
            set
            {
                this.isSmall = value;
                this.RaiseCurrentPropertyChanged();
            }
        }

        private void UpdateApplicationSizes(double width)
        {
            this.IsLarge = 1024 <= width;
            this.isMedium = 700 <= width && width < 1024;
            this.IsSmall = width < 700;
            this.RaisePropertyChanged(() => this.IsMediumOrLarge);
            this.RaisePropertyChanged(() => this.IsMediumOrSmall);
        }
    }
}
