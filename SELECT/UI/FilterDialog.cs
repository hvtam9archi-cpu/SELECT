using System.Drawing;
using System.Windows.Forms;

namespace UnifiedAutoCADTools.UI
{
    public class FilterDialog : Form
    {
        private readonly CheckBox cbLayer;
        private readonly CheckBox cbColor;
        private readonly CheckBox cbLinetype;
        private readonly CheckBox cbBlock;
        private readonly Button btnOK;
        private readonly Button btnCancel;

        public bool CheckLayer => cbLayer.Checked;
        public bool CheckColor => cbColor.Checked;
        public bool CheckLinetype => cbLinetype.Checked;
        public bool CheckBlockName => cbBlock.Checked;

        public FilterDialog(bool isBlock)
        {
            this.Text = "Lọc Đối Tượng Nâng Cao";
            this.Size = new Size(250, 220);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int y = 20;
            int x = 20;

            cbLayer = CreateCheck("Layer", x, y, true);
            y += 30;
            cbColor = CreateCheck("Color", x, y, false);
            y += 30;
            cbLinetype = CreateCheck("Linetype", x, y, false);
            y += 30;
            cbBlock = CreateCheck("Block Name", x, y, true);

            cbBlock.Enabled = isBlock;
            if (!isBlock) cbBlock.Checked = false;

            y += 40;

            btnOK = new Button
            {
                Text = "OK",
                Location = new Point(30, y),
                DialogResult = DialogResult.OK
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(120, y),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[] { cbLayer, cbColor, cbLinetype, cbBlock, btnOK, btnCancel });
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private CheckBox CreateCheck(string text, int x, int y, bool isChecked)
        {
            return new CheckBox
            {
                Text = text,
                Location = new Point(x, y),
                Checked = isChecked,
                AutoSize = true
            };
        }
    }
}