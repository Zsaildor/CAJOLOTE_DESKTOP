using System.Threading.Tasks;
using System.Windows;

namespace Cajolote.Messages
{
    public class ShowModalMessage
    {
        public string Message { get; }
        public string Title { get; }
        public MessageBoxButton Button { get; }
        public MessageBoxImage Icon { get; }
        public TaskCompletionSource<MessageBoxResult> Tcs { get; }

        public ShowModalMessage(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            Message = message;
            Title = title;
            Button = button;
            Icon = icon;
            Tcs = new TaskCompletionSource<MessageBoxResult>();
        }
    }
}
