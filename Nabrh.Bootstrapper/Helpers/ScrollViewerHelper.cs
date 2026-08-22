using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ERPUI.Helpers
{
    /// <summary>
    /// Global helper ensuring smooth mouse wheel scrolling across all ScrollViewer containers in the application.
    /// </summary>
    public static class ScrollViewerHelper
    {
        private static bool _isInitialized;

        public static void InitializeGlobalMouseWheelScrolling()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;

            // Register global handler for all ScrollViewers in WPF
            EventManager.RegisterClassHandler(
                typeof(ScrollViewer),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnScrollViewerPreviewMouseWheel),
                true);
        }

        private static void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
                return;

            // If the scrollviewer cannot scroll vertically at all, return
            if (scrollViewer.ScrollableHeight <= 0)
                return;

            // Check if mouse wheel event originated from a nested child ScrollViewer
            if (e.OriginalSource is DependencyObject originalSource)
            {
                var childScrollViewer = FindParentScrollViewer(originalSource);
                if (childScrollViewer != null && childScrollViewer != scrollViewer && childScrollViewer.ScrollableHeight > 0)
                {
                    bool canChildScroll = e.Delta < 0
                        ? childScrollViewer.VerticalOffset < childScrollViewer.ScrollableHeight
                        : childScrollViewer.VerticalOffset > 0;

                    if (canChildScroll)
                    {
                        // Allow child scrollviewer to handle its own scrolling first
                        return;
                    }
                }
            }

            // Calculate responsive smooth offset step
            double scrollDelta = e.Delta * 0.5; // Responsive multiplier for natural feel
            double newOffset = scrollViewer.VerticalOffset - scrollDelta;

            if (newOffset < 0)
                newOffset = 0;
            else if (newOffset > scrollViewer.ScrollableHeight)
                newOffset = scrollViewer.ScrollableHeight;

            if (Math.Abs(newOffset - scrollViewer.VerticalOffset) > 0.01)
            {
                scrollViewer.ScrollToVerticalOffset(newOffset);
                e.Handled = true;
            }
        }

        private static ScrollViewer? FindParentScrollViewer(DependencyObject child)
        {
            try
            {
                var parent = VisualTreeHelper.GetParent(child);
                while (parent != null && parent is not ScrollViewer)
                {
                    parent = VisualTreeHelper.GetParent(parent);
                }
                return parent as ScrollViewer;
            }
            catch
            {
                return null;
            }
        }
    }
}

