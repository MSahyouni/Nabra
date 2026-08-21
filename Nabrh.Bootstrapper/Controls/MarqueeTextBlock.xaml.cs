using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace ERPUI.Controls
{
    public partial class MarqueeTextBlock : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(MarqueeTextBlock), new PropertyMetadata(string.Empty, OnTextChanged));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        private DoubleAnimation? _marqueeAnimation;
        private TranslateTransform _translateTransform;

        public MarqueeTextBlock()
        {
            InitializeComponent();
            _translateTransform = new TranslateTransform();
            MainText.RenderTransform = _translateTransform;
            
            this.Loaded += (s, e) => StartMarquee();
            this.SizeChanged += (s, e) => StartMarquee();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MarqueeTextBlock marquee)
            {
                marquee.StartMarquee();
            }
        }

        private void StartMarquee()
        {
            if (!this.IsLoaded) return;

            _translateTransform.BeginAnimation(TranslateTransform.XProperty, null);
            _translateTransform.X = 0;

            MainText.TextTrimming = TextTrimming.None;
            MainText.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
            
            double textWidth = MainText.DesiredSize.Width;
            double containerWidth = this.ActualWidth;

            if (textWidth > containerWidth && containerWidth > 0)
            {
                double distance = textWidth - containerWidth;
                
                _marqueeAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = distance,
                    Duration = new Duration(TimeSpan.FromSeconds(distance / 20.0)), // 20 pixels per second
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                
                _translateTransform.BeginAnimation(TranslateTransform.XProperty, _marqueeAnimation);
            }
            else
            {
                MainText.TextTrimming = TextTrimming.CharacterEllipsis;
            }
        }
    }
}

