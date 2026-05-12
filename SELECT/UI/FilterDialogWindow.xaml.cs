using System.Windows;
using System.Windows.Input;

namespace UnifiedAutoCADTools.UI
{
    public partial class FilterDialogWindow : Window
    {
        // Ghi nhớ trạng thái bộ lọc (UX)
        private static bool _lastLayer = true;
        private static bool _lastColor = false;
        private static bool _lastLinetype = false;
        private static bool _lastBlock = true;

        public bool CheckLayer => cbLayer.IsChecked == true;
        public bool CheckColor => cbColor.IsChecked == true;
        public bool CheckLinetype => cbLinetype.IsChecked == true;
        public bool CheckBlockName => cbBlock.IsChecked == true;

        public FilterDialogWindow(bool isBlock)
        {
            InitializeComponent();
            
            cbLayer.IsChecked = _lastLayer;
            cbColor.IsChecked = _lastColor;
            cbLinetype.IsChecked = _lastLinetype;
            cbBlock.IsChecked = _lastBlock;

            cbBlock.IsEnabled = isBlock;
            if (!isBlock) cbBlock.IsChecked = false;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            _lastLayer = cbLayer.IsChecked == true;
            _lastColor = cbColor.IsChecked == true;
            _lastLinetype = cbLinetype.IsChecked == true;
            _lastBlock = cbBlock.IsChecked == true;

            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
