using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Collections.Generic;

namespace VolumeOSD {
    public class OsdWindow : Window {
        private TextBlock appNameText;
        private Border progressBar;
        private DispatcherTimer fadeTimer;
        
        private Grid singleModeGrid;
        private StackPanel listModePanel;
        private ScrollViewer listScrollViewer;
        
        private bool isInListMode = false;
        private bool isItemLocked = false;
        private DispatcherTimer listModeTimer;
        private List<VolumeOSD.Audio.AppAudioSession> currentSessions = new List<VolumeOSD.Audio.AppAudioSession>();
        private int selectedSessionIndex = 0;

        public bool IsInListMode { get { return isInListMode; } }
        public bool IsItemLocked { get { return isItemLocked; } }

        public string DefaultAppName { get; private set; }
        private Border currentHoldProgressBorder;
        private string configFilePath;

        public OsdWindow() {
            configFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "default_app.txt");
            if (System.IO.File.Exists(configFilePath)) {
                DefaultAppName = System.IO.File.ReadAllText(configFilePath).Trim();
            } else {
                DefaultAppName = "";
            }

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            IsHitTestVisible = false;
            Width = 280;
            Height = 70;
            
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = SystemParameters.WorkArea.Bottom - Height - 60;

            var border = new Border {
                Background = new SolidColorBrush(Color.FromArgb(220, 25, 25, 25)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 10, 20, 10)
            };

            singleModeGrid = new Grid();
            singleModeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            singleModeGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            appNameText = new TextBlock {
                Foreground = Brushes.White,
                FontSize = 14,
                FontFamily = new FontFamily("Microsoft YaHei"),
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(appNameText, 0);

            var progressGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            
            var track = new Border {
                Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                Height = 6,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            progressBar = new Border {
                Background = Brushes.White,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            
            progressGrid.Children.Add(track);
            progressGrid.Children.Add(progressBar);
            Grid.SetRow(progressGrid, 1);

            singleModeGrid.Children.Add(appNameText);
            singleModeGrid.Children.Add(progressGrid);
            
            listModePanel = new StackPanel { Orientation = Orientation.Vertical };
            listScrollViewer = new ScrollViewer {
                Content = listModePanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Visibility = Visibility.Collapsed
            };

            var containerGrid = new Grid();
            containerGrid.Children.Add(singleModeGrid);
            containerGrid.Children.Add(listScrollViewer);

            border.Child = containerGrid;
            Content = border;

            fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.0) };
            fadeTimer.Tick += (s, e) => {
                fadeTimer.Stop();
                var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
                anim.Completed += (s2, e2) => { 
                    this.Visibility = Visibility.Hidden; 
                };
                this.BeginAnimation(UIElement.OpacityProperty, anim);
            };

            listModeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.0) };
            listModeTimer.Tick += (s, e) => {
                ExitListMode();
            };
        }

        public void UpdateVolume(string appName, double percent) {
            if (isInListMode) return; // 单模式更新不影响列表模式
            
            singleModeGrid.Visibility = Visibility.Visible;
            listScrollViewer.Visibility = Visibility.Collapsed;
            this.Height = 70;
            this.Top = SystemParameters.WorkArea.Bottom - this.Height - 60;

            if(appName.Length > 0) {
                appName = char.ToUpper(appName[0]) + appName.Substring(1);
            }
            appNameText.Text = appName + "  " + percent + "%";
            this.Visibility = Visibility.Visible;
            
            this.BeginAnimation(UIElement.OpacityProperty, null);
            this.Opacity = 1.0;

            double targetWidth = (Width - 40) * (percent / 100.0);
            if(targetWidth < 0) targetWidth = 0;
            if(targetWidth > (Width - 40)) targetWidth = Width - 40;

            var anim = new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(20)); // 极速动画
            progressBar.BeginAnimation(Border.WidthProperty, anim);

            fadeTimer.Stop();
            fadeTimer.Start();
        }

        public void ToggleListMode() {
            if (isInListMode) {
                SetDefaultApp();
            } else {
                EnterListMode();
            }
        }

        private void EnterListMode() {
            currentSessions = VolumeOSD.Audio.AudioManager.GetActiveSessions();
            if (currentSessions.Count == 0) return;
            
            isInListMode = true;
            isItemLocked = false;
            selectedSessionIndex = 0;
            
            singleModeGrid.Visibility = Visibility.Collapsed;
            listScrollViewer.Visibility = Visibility.Visible;
            
            double newHeight = 40 + currentSessions.Count * 50;
            if (newHeight > SystemParameters.WorkArea.Height * 0.7) {
                newHeight = SystemParameters.WorkArea.Height * 0.7;
            }
            this.Height = newHeight;
            this.Top = SystemParameters.WorkArea.Bottom - this.Height - 60;
            
            RenderListMode();
            
            this.Visibility = Visibility.Visible;
            this.BeginAnimation(UIElement.OpacityProperty, null);
            this.Opacity = 1.0;
            
            listModeTimer.Stop();
            listModeTimer.Start();
            fadeTimer.Stop();
        }

        private void ExitListMode() {
            isInListMode = false;
            isItemLocked = false;
            listModeTimer.Stop();
            
            var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
            anim.Completed += (s2, e2) => { 
                this.Visibility = Visibility.Hidden; 
                singleModeGrid.Visibility = Visibility.Visible;
                listScrollViewer.Visibility = Visibility.Collapsed;
                this.Height = 70;
                this.Top = SystemParameters.WorkArea.Bottom - this.Height - 60;
            };
            this.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        public void StartHoldProgress() {
            if (currentHoldProgressBorder != null && isInListMode) {
                var anim = new DoubleAnimation(0, this.Width - 20, TimeSpan.FromMilliseconds(3000));
                currentHoldProgressBorder.BeginAnimation(Border.WidthProperty, anim);
                listModeTimer.Stop();
            }
        }

        public void CancelHoldProgress() {
            if (currentHoldProgressBorder != null) {
                currentHoldProgressBorder.BeginAnimation(Border.WidthProperty, null);
                currentHoldProgressBorder.Width = 0;
                if (isInListMode) {
                    listModeTimer.Start();
                }
            }
        }

        public void SetDefaultApp() {
            if (isInListMode && currentSessions.Count > 0 && selectedSessionIndex >= 0 && selectedSessionIndex < currentSessions.Count) {
                var session = currentSessions[selectedSessionIndex];
                DefaultAppName = session.AppName;
                try {
                    System.IO.File.WriteAllText(configFilePath, DefaultAppName);
                } catch { }
                RenderListMode();
                
                var anim = new DoubleAnimation(1.0, 0.3, TimeSpan.FromMilliseconds(150));
                anim.AutoReverse = true;
                this.BeginAnimation(UIElement.OpacityProperty, anim);
                
                listModeTimer.Stop();
                listModeTimer.Start();
            }
        }

        public void ToggleItemLock() {
            if (!isInListMode) return;
            isItemLocked = !isItemLocked;
            RenderListMode();
            ResetListTimer();
        }

        public void ListScroll(int direction) {
            if (currentSessions.Count == 0) return;
            selectedSessionIndex += direction;
            if (selectedSessionIndex < 0) selectedSessionIndex = currentSessions.Count - 1;
            if (selectedSessionIndex >= currentSessions.Count) selectedSessionIndex = 0;
            
            RenderListMode();
            ResetListTimer();
            listScrollViewer.ScrollToVerticalOffset(selectedSessionIndex * 50 - listScrollViewer.ActualHeight / 2 + 25);
        }

        public void ChangeSelectedVolume(float step) {
            if (currentSessions.Count == 0 || selectedSessionIndex < 0 || selectedSessionIndex >= currentSessions.Count) return;
            var session = currentSessions[selectedSessionIndex];
            VolumeOSD.Audio.AudioManager.ChangeAppVolume(session, step);
            RenderListMode();
            ResetListTimer();
        }

        private void ResetListTimer() {
            listModeTimer.Stop();
            listModeTimer.Start();
        }

        public void RenderListMode() {
            listModePanel.Children.Clear();
            Brush highlightBrush = isItemLocked ? Brushes.Gold : Brushes.LightSkyBlue;

            for (int i = 0; i < currentSessions.Count; i++) {
                var session = currentSessions[i];
                var itemGrid = new Grid { Margin = new Thickness(0, 5, 0, 5), Height = 40 };
                itemGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                itemGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var displayName = session.AppName;
                if(displayName.Length > 0) displayName = char.ToUpper(displayName[0]) + displayName.Substring(1);

                if (!string.IsNullOrEmpty(DefaultAppName) && session.AppName.Equals(DefaultAppName, StringComparison.OrdinalIgnoreCase)) {
                    displayName = "[默认] " + displayName;
                }

                var text = new TextBlock {
                    Text = displayName + "  " + Math.Round(session.Volume * 100) + "%",
                    Foreground = (i == selectedSessionIndex) ? highlightBrush : Brushes.White,
                    FontSize = 14,
                    FontFamily = new FontFamily("Microsoft YaHei"),
                    FontWeight = (i == selectedSessionIndex) ? FontWeights.Bold : FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 0, 0, 3)
                };
                Grid.SetRow(text, 0);

                var track = new Border {
                    Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                    Height = 4,
                    CornerRadius = new CornerRadius(2),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                
                double targetWidth = (Width - 40) * session.Volume;
                if(targetWidth < 0) targetWidth = 0;
                if(targetWidth > (Width - 40)) targetWidth = Width - 40;

                var bar = new Border {
                    Background = (i == selectedSessionIndex) ? highlightBrush : Brushes.White,
                    Height = 4,
                    CornerRadius = new CornerRadius(2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = targetWidth
                };
                
                var progGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
                progGrid.Children.Add(track);
                progGrid.Children.Add(bar);
                Grid.SetRow(progGrid, 1);
                
                itemGrid.Children.Add(text);
                itemGrid.Children.Add(progGrid);
                
                if (i == selectedSessionIndex) {
                    var bg = new Border {
                        Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                        CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(-10, -2, -10, -2)
                    };
                    
                    currentHoldProgressBorder = new Border {
                        Background = new SolidColorBrush(Color.FromArgb(80, 255, 215, 0)),
                        CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(-10, -2, -10, -2),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Width = 0
                    };

                    var wrapper = new Grid();
                    wrapper.Children.Add(bg);
                    wrapper.Children.Add(currentHoldProgressBorder);
                    wrapper.Children.Add(itemGrid);
                    listModePanel.Children.Add(wrapper);
                } else {
                    listModePanel.Children.Add(itemGrid);
                }
            }
        }
    }
}
