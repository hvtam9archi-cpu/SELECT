using Autodesk.Windows;
using System;
using System.Windows.Controls;

namespace UnifiedAutoCADTools
{
    public static class RibbonSetup
    {
        public static void InitializeRibbon()
        {
            try
            {
                if (ComponentManager.Ribbon == null)
                {
                    ComponentManager.ItemInitialized += ComponentManager_ItemInitialized;
                }
                else
                {
                    CreateRibbon();
                }
            }
            catch
            {
                // Silent catch - Ribbon chưa sẵn sàng
            }
        }

        private static void ComponentManager_ItemInitialized(object sender, RibbonItemEventArgs e)
        {
            if (ComponentManager.Ribbon != null)
            {
                ComponentManager.ItemInitialized -= ComponentManager_ItemInitialized;
                CreateRibbon();
            }
        }

        private static void CreateRibbon()
        {
            RibbonControl ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;

            string tabTitle = "TH Tools";
            string tabId = "THTools_Tab";
            RibbonTab rtab = null;

            foreach (RibbonTab tab in ribbon.Tabs)
            {
                if (tab.Title == tabTitle || tab.Id == tabId)
                {
                    rtab = tab;
                    break;
                }
            }

            if (rtab == null)
            {
                rtab = new RibbonTab
                {
                    Title = tabTitle,
                    Id = tabId
                };
                ribbon.Tabs.Add(rtab);
            }

            // Check if panel already exists to prevent duplicate panels on reload
            string panelId = "SELECT_Panel";
            RibbonPanel panel = null;
            foreach (RibbonPanel p in rtab.Panels)
            {
                if (p.Source.Id == panelId)
                {
                    panel = p;
                    break;
                }
            }

            if (panel == null)
            {
                RibbonPanelSource panelSrc = new RibbonPanelSource { Title = "Selection", Id = panelId };
                panel = new RibbonPanel { Source = panelSrc };
                rtab.Panels.Add(panel);

                // Add buttons for commands
                RibbonButton btnSS = new RibbonButton
                {
                    Text = "Select Similar",
                    ShowText = true,
                    ShowImage = true,
                    Image = CreateIconWpf("SS", "#2563EB", 16),
                    LargeImage = CreateIconWpf("SS", "#2563EB", 32),
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    CommandParameter = "SS ",
                    CommandHandler = new RibbonCommandHandler()
                };

                RibbonButton btnSSAdv = new RibbonButton
                {
                    Text = "Advanced Select",
                    ShowText = true,
                    ShowImage = true,
                    Image = CreateIconWpf("Adv", "#1D4ED8", 16),
                    LargeImage = CreateIconWpf("Adv", "#1D4ED8", 32),
                    Size = RibbonItemSize.Large,
                    Orientation = Orientation.Vertical,
                    CommandParameter = "SSADV ",
                    CommandHandler = new RibbonCommandHandler()
                };

                RibbonButton btnQQ = new RibbonButton
                {
                    Text = "Isolate",
                    ShowText = true,
                    ShowImage = true,
                    Image = CreateIconWpf("Iso", "#10B981", 16),
                    Size = RibbonItemSize.Standard,
                    CommandParameter = "QQ ",
                    CommandHandler = new RibbonCommandHandler()
                };

                RibbonButton btnAQ = new RibbonButton
                {
                    Text = "Hide",
                    ShowText = true,
                    ShowImage = true,
                    Image = CreateIconWpf("Hide", "#EF4444", 16),
                    Size = RibbonItemSize.Standard,
                    CommandParameter = "AQ ",
                    CommandHandler = new RibbonCommandHandler()
                };

                RibbonButton btnQA = new RibbonButton
                {
                    Text = "Unisolate",
                    ShowText = true,
                    ShowImage = true,
                    Image = CreateIconWpf("Unh", "#F59E0B", 16),
                    Size = RibbonItemSize.Standard,
                    CommandParameter = "QA ",
                    CommandHandler = new RibbonCommandHandler()
                };
                
                RibbonRowPanel rowPanel = new RibbonRowPanel();
                rowPanel.Items.Add(btnQQ);
                rowPanel.Items.Add(new RibbonRowBreak());
                rowPanel.Items.Add(btnAQ);
                rowPanel.Items.Add(new RibbonRowBreak());
                rowPanel.Items.Add(btnQA);

                panelSrc.Items.Add(btnSS);
                panelSrc.Items.Add(btnSSAdv);
                panelSrc.Items.Add(new RibbonSeparator());
                panelSrc.Items.Add(rowPanel);
            }

            rtab.IsActive = true;
        }

        private static System.Windows.Media.Imaging.BitmapImage CreateIconWpf(string text, string hexColor, int size)
        {
            var dv = new System.Windows.Media.DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                var brush = new System.Windows.Media.SolidColorBrush(color);
                dc.DrawRoundedRectangle(brush, null, new System.Windows.Rect(0, 0, size, size), 4, 4);

                double fontSize = size == 32 ? 11 : 8;
                var tf = new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Segoe UI"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal);
                
                // VisualStudio 2022 / .NET 4.8 FormattedText constructor
                var ft = new System.Windows.Media.FormattedText(
                    text, 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    System.Windows.FlowDirection.LeftToRight, 
                    tf, 
                    fontSize, 
                    System.Windows.Media.Brushes.White, 
                    1.25);
                
                dc.DrawText(ft, new System.Windows.Point((size - ft.Width) / 2, (size - ft.Height) / 2));
            }

            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(dv);
            
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            
            var ms = new System.IO.MemoryStream();
            encoder.Save(ms);
            ms.Position = 0;

            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.StreamSource = ms;
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();

            return image;
        }
    }

    public class RibbonCommandHandler : System.Windows.Input.ICommand
    {
#pragma warning disable 0067
        public event EventHandler CanExecuteChanged;
#pragma warning restore 0067

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            RibbonButton btn = parameter as RibbonButton;
            if (btn != null && btn.CommandParameter != null)
            {
                Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.SendStringToExecute(
                    (string)btn.CommandParameter, true, false, false);
            }
        }
    }
}
