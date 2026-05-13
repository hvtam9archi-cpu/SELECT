using System;
using System.Windows.Input;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace UnifiedAutoCADTools
{
    public static class RibbonSetup
    {
        private const string TabId = "TH_TOOLS_TAB";
        private const string TabTitle = "TH Tools";
        private static RibbonCommandHandler _cmdHandler = new RibbonCommandHandler();

        public static void InitializeRibbon()
        {
            Application.Idle += OnIdle;
        }

        private static void OnIdle(object sender, EventArgs e)
        {
            Application.Idle -= OnIdle;
            CreateRibbon();
        }

        private static void CreateRibbon()
        {
            RibbonControl ribbon = ComponentManager.Ribbon;
            if (ribbon == null) return;

            // 1. Tìm hoặc Tạo Tab "TH Tools"
            RibbonTab rtb = ribbon.FindTab(TabId);
            if (rtb == null)
            {
                rtb = new RibbonTab { Title = TabTitle, Id = TabId };
                ribbon.Tabs.Add(rtb);
            }

            // 2. Tìm hoặc Tạo Panel "Selection"
            string panelId = "SELECT_Panel";
            bool panelExists = false;
            foreach (RibbonPanel p in rtb.Panels)
            {
                if (p.Source.Id == panelId)
                {
                    panelExists = true;
                    break;
                }
            }

            if (!panelExists)
            {
                RibbonPanelSource rps = new RibbonPanelSource { Title = "Selection", Id = panelId };
                RibbonPanel rp = new RibbonPanel { Source = rps };

                // Add buttons for commands
                RibbonButton btnSS = CreateLargeButton("SS", "Select Similar", "SS", "#2563EB");
                RibbonButton btnSSAdv = CreateLargeButton("Adv", "Advanced Select", "SSADV", "#1D4ED8");

                RibbonButton btnQQ = CreateButton("Iso", "Isolate", "QQ", "#10B981");
                RibbonButton btnAQ = CreateButton("Hide", "Hide", "AQ", "#EF4444");
                RibbonButton btnQA = CreateButton("Unh", "Unisolate", "QA", "#F59E0B");

                RibbonRowPanel rowPanel = new RibbonRowPanel();
                rowPanel.Items.Add(btnQQ);
                rowPanel.Items.Add(new RibbonRowBreak());
                rowPanel.Items.Add(btnAQ);
                rowPanel.Items.Add(new RibbonRowBreak());
                rowPanel.Items.Add(btnQA);

                rps.Items.Add(btnSS);
                rps.Items.Add(btnSSAdv);
                rps.Items.Add(new RibbonSeparator());
                rps.Items.Add(rowPanel);

                rtb.Panels.Add(rp);
            }

            rtb.IsActive = true;
        }

        private static RibbonButton CreateButton(string id, string text, string command, string hexColor)
        {
            return new RibbonButton
            {
                Id = id,
                Text = text,
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Standard,
                Image = GetTextBitmap(id, hexColor, 16),
                CommandParameter = "\x03\x03" + command + " ",
                CommandHandler = _cmdHandler
            };
        }

        private static RibbonButton CreateLargeButton(string id, string text, string command, string hexColor)
        {
            return new RibbonButton
            {
                Id = id,
                Text = text,
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Large,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                Image = GetTextBitmap(id, hexColor, 16),
                LargeImage = GetTextBitmap(id, hexColor, 32),
                CommandParameter = "\x03\x03" + command + " ",
                CommandHandler = _cmdHandler
            };
        }

        private static System.Windows.Media.ImageSource GetTextBitmap(string text, string hexColor, int size)
        {
            System.Windows.Media.DrawingVisual visual = new System.Windows.Media.DrawingVisual();
            using (System.Windows.Media.DrawingContext dc = visual.RenderOpen())
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                dc.DrawRectangle(new System.Windows.Media.SolidColorBrush(color), null, new System.Windows.Rect(0, 0, size, size));
                
                dc.DrawRectangle(null, new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 0.5), new System.Windows.Rect(0.5, 0.5, size - 1, size - 1));

                double fontSize = size == 32 ? 14 : 9;
                System.Windows.Media.FormattedText ft = new System.Windows.Media.FormattedText(
                    text.Length > 3 ? text.Substring(0, 3) : text,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    new System.Windows.Media.Typeface(new System.Windows.Media.FontFamily("Segoe UI"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
                    fontSize,
                    System.Windows.Media.Brushes.White,
                    1.0);
                
                dc.DrawText(ft, new System.Windows.Point((size - ft.Width) / 2, (size - ft.Height) / 2));
            }
            
            System.Windows.Media.Imaging.RenderTargetBitmap rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
        }
    }

    public class RibbonCommandHandler : ICommand
    {
        public event EventHandler CanExecuteChanged;
        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter)
        {
            string cmd = null;
            if (parameter is RibbonButton btn)
                cmd = btn.CommandParameter as string;
            else if (parameter is string s)
                cmd = s;

            if (!string.IsNullOrEmpty(cmd))
            {
                Autodesk.AutoCAD.ApplicationServices.Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.SendStringToExecute(cmd, true, false, true);
                }
            }
        }
    }
}
