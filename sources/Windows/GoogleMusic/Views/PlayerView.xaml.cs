// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------

namespace OutcoldSolutions.GoogleMusic.Views
{
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;

    using OutcoldSolutions.Views;

    public interface IPlayerView : IView
    {
    }

    public sealed partial class PlayerView : ViewBase, IPlayerView
    {
        public PlayerView()
        {
            this.InitializeComponent();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            var sizes = this.Container.Resolve<IMainFrame>().Sizes;
            sizes.SizeChanges += (sender, args) =>
            {
                Grid.SetRow(this.SongMetadataPanel, sizes.IsSmall ? 0 : 1);
                Grid.SetColumn(this.SongMetadataPanel, sizes.IsSmall ? 0 : 1);
                Grid.SetColumnSpan(this.SongMetadataPanel, sizes.IsSmall ? 3 : 2);

                this.SongProgressPanel.VerticalAlignment = sizes.IsSmall
                    ? VerticalAlignment.Bottom
                    : VerticalAlignment.Top;

                Grid.SetColumnSpan(this.SongProgressPanel, sizes.IsSmall ? 2 : 1);

                this.SongProgressPanel.Orientation = sizes.IsSmall ? Orientation.Horizontal : Orientation.Vertical;
            };
        }
    }
}
