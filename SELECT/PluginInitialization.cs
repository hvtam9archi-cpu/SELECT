using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
// Thêm dòng này để chỉ định rõ Exception là System.Exception
using Exception = System.Exception;

// Đăng ký toàn bộ Assembly
[assembly: ExtensionApplication(typeof(UnifiedAutoCADTools.PluginInitialization))]
[assembly: CommandClass(typeof(UnifiedAutoCADTools.Commands.ReselectCommands))]
[assembly: CommandClass(typeof(UnifiedAutoCADTools.Commands.VisibilityCommands))]
[assembly: CommandClass(typeof(UnifiedAutoCADTools.Commands.SelectSimilarCommands))]

namespace UnifiedAutoCADTools
{
    public class PluginInitialization : IExtensionApplication
    {
        // Static state để lưu ID cho lệnh Reselect
        public static ObjectId[] LastSelectedIds = null;

        public void Initialize()
        {
            try
            {
                DocumentCollection docManager = Application.DocumentManager;
                if (docManager.MdiActiveDocument != null)
                {
                    docManager.MdiActiveDocument.ImpliedSelectionChanged += OnImpliedSelectionChanged;
                }
                docManager.DocumentCreated += (s, e) =>
                {
                    e.Document.ImpliedSelectionChanged += OnImpliedSelectionChanged;
                };
            }
            catch (Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\nError Init: {ex.Message}");
            }
        }

        public void Terminate() { }

        private void OnImpliedSelectionChanged(object sender, EventArgs e)
        {
            // Không try-catch ở đây để tránh overhead, sự kiện này gọi rất nhiều lần
            Document doc = sender as Document;
            if (doc != null)
            {
                PromptSelectionResult result = doc.Editor.SelectImplied();
                if (result.Status == PromptStatus.OK && result.Value != null && result.Value.Count > 0)
                {
                    LastSelectedIds = result.Value.GetObjectIds();
                }
            }
        }
    }
}