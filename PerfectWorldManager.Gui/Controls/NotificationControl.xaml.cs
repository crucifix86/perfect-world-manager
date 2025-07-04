using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PerfectWorldManager.Gui.Controls
{
    public enum NotificationType
    {
        Success,
        Error,
        Warning,
        Info
    }

    public partial class NotificationControl : UserControl
    {
        private DispatcherTimer autoCloseTimer;
        
        public NotificationControl()
        {
            InitializeComponent();
            
            autoCloseTimer = new DispatcherTimer();
            autoCloseTimer.Interval = TimeSpan.FromSeconds(5);
            autoCloseTimer.Tick += (s, e) => Close();
        }

        public void Show(string title, string message, NotificationType type)
        {
            TitleText.Text = title;
            
            if (!string.IsNullOrEmpty(message))
            {
                MessageText.Text = message;
                MessageText.Visibility = Visibility.Visible;
            }
            else
            {
                MessageText.Visibility = Visibility.Collapsed;
            }
            
            SetNotificationType(type);
            
            var slideIn = FindResource("SlideIn") as Storyboard;
            slideIn?.Begin();
            
            autoCloseTimer.Start();
        }
        
        private void SetNotificationType(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Success:
                    NotificationBorder.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    ColorIndicator.Fill = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                    IconPath.Data = Geometry.Parse("M9,20.42L2.79,14.21L5.62,11.38L9,14.77L18.88,4.88L21.71,7.71L9,20.42Z");
                    IconPath.Fill = Brushes.White;
                    break;
                    
                case NotificationType.Error:
                    NotificationBorder.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    ColorIndicator.Fill = new SolidColorBrush(Color.FromRgb(183, 28, 28));
                    IconPath.Data = Geometry.Parse("M12,2C17.53,2 22,6.47 22,12C22,17.53 17.53,22 12,22C6.47,22 2,17.53 2,12C2,6.47 6.47,2 12,2M15.59,7L12,10.59L8.41,7L7,8.41L10.59,12L7,15.59L8.41,17L12,13.41L15.59,17L17,15.59L13.41,12L17,8.41L15.59,7Z");
                    IconPath.Fill = Brushes.White;
                    break;
                    
                case NotificationType.Warning:
                    NotificationBorder.Background = new SolidColorBrush(Color.FromRgb(255, 171, 64));
                    ColorIndicator.Fill = new SolidColorBrush(Color.FromRgb(230, 124, 0));
                    IconPath.Data = Geometry.Parse("M12,2L1,21H23M12,6L19.53,19H4.47M11,10V14H13V10M11,16V18H13V16");
                    IconPath.Fill = Brushes.White;
                    break;
                    
                case NotificationType.Info:
                    NotificationBorder.Background = new SolidColorBrush(Color.FromRgb(76, 194, 255));
                    ColorIndicator.Fill = new SolidColorBrush(Color.FromRgb(0, 145, 234));
                    IconPath.Data = Geometry.Parse("M11,9H13V7H11M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M11,17H13V11H11V17Z");
                    IconPath.Fill = Brushes.White;
                    break;
            }
            
            TitleText.Foreground = Brushes.White;
            MessageText.Foreground = Brushes.White;
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        public void Close()
        {
            autoCloseTimer.Stop();
            
            var slideOut = FindResource("SlideOut") as Storyboard;
            if (slideOut != null)
            {
                slideOut.Completed += (s, e) =>
                {
                    if (Parent is Panel parent)
                    {
                        parent.Children.Remove(this);
                    }
                };
                slideOut.Begin();
            }
        }
    }
}