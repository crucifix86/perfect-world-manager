using System.Windows;
using System.Windows.Controls;
using PerfectWorldManager.Gui.Controls;

namespace PerfectWorldManager.Gui.Services
{
    public static class NotificationManager
    {
        private static Grid notificationContainer;
        
        public static void Initialize(Grid container)
        {
            notificationContainer = container;
        }
        
        public static void Show(string title, NotificationType type = NotificationType.Info)
        {
            Show(title, null, type);
        }
        
        public static void Show(string title, string message, NotificationType type = NotificationType.Info)
        {
            if (notificationContainer == null)
                return;
                
            Application.Current.Dispatcher.Invoke(() =>
            {
                var notification = new NotificationControl();
                
                // Position the notification
                notification.HorizontalAlignment = HorizontalAlignment.Right;
                notification.VerticalAlignment = VerticalAlignment.Top;
                notification.Margin = new Thickness(0, 10 + (notificationContainer.Children.Count * 90), 10, 0);
                
                // Add to container
                notificationContainer.Children.Add(notification);
                Grid.SetZIndex(notification, 1000);
                
                // Show the notification
                notification.Show(title, message, type);
            });
        }
        
        public static void ShowSuccess(string title, string message = null)
        {
            Show(title, message, NotificationType.Success);
        }
        
        public static void ShowError(string title, string message = null)
        {
            Show(title, message, NotificationType.Error);
        }
        
        public static void ShowWarning(string title, string message = null)
        {
            Show(title, message, NotificationType.Warning);
        }
        
        public static void ShowInfo(string title, string message = null)
        {
            Show(title, message, NotificationType.Info);
        }
    }
}