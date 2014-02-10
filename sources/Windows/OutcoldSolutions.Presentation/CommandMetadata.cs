// --------------------------------------------------------------------------------------------------------------------
// Outcold Solutions (http://outcoldman.com)
// --------------------------------------------------------------------------------------------------------------------
namespace OutcoldSolutions
{
    using System;

    using Windows.UI.Xaml.Controls;

    /// <summary>
    /// The command metadata.
    /// </summary>
    public class CommandMetadata
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandMetadata"/> class.
        /// </summary>
        /// <param name="symbol">
        /// The icon name.
        /// </param>
        /// <param name="command">
        /// The command.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="symbol"/> or <paramref name="command"/> are null.
        /// </exception>
        public CommandMetadata(Symbol symbol, DelegateCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            this.Symbol = symbol;
            this.Command = command;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandMetadata"/> class.
        /// </summary>
        /// <param name="symbol">
        /// The icon name.
        /// </param>
        /// <param name="title">
        /// The title.
        /// </param>
        /// <param name="command">
        /// The command.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// If <paramref name="iconName"/> or <paramref name="command"/> or <paramref name="title"/> are null.
        /// </exception>
        public CommandMetadata(Symbol symbol, string title, DelegateCommand command)
            : this(symbol, command)
        {
            if (title == null)
            {
                throw new ArgumentNullException("title");
            }

            this.Title = title;
        }

        /// <summary>
        /// Gets or sets the icon name.
        /// </summary>
        public Symbol Symbol { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the command.
        /// </summary>
        public DelegateCommand Command { get; set; }
    }
}