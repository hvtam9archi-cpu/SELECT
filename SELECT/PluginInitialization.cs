using System;
using System.Xml.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Exception = System.Exception;

[assembly: ExtensionApplication(typeof(UnifiedAutoCADTools.PluginInitialization))]
[assembly: CommandClass(typeof(UnifiedAutoCADTools.Commands.ReselectCommands))]
[assembly: CommandClass(typeof(UnifiedAutoCADTools.Commands.VisibilityCommands))]
[assembly: CommandClass(typeof(UnifiedAutoCADTools.Commands.SelectSimilarCommands))]

namespace UnifiedAutoCADTools
{
	public class PluginInitialization : IExtensionApplication
	{
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

				docManager.DocumentCreated += OnDocumentCreated;
			}
			catch (Exception ex)
			{
				Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n[Hệ thống] Lỗi khởi tạo Plugin: {ex.Message}");
			}
		}

		public void Terminate()
		{
			// Dọn dẹp an toàn khi Plugin bị unload hoặc AutoCAD tắt
			try
			{
				DocumentCollection docManager = Application.DocumentManager;
				if (docManager != null)
				{
					docManager.DocumentCreated -= OnDocumentCreated;
					foreach (Document doc in docManager)
					{
						doc.ImpliedSelectionChanged -= OnImpliedSelectionChanged;
					}
				}
				LastSelectedIds = null;
			}
			catch { /* Bỏ qua lỗi trong quá trình Terminate */ }
		}

		private void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
		{
			if (e.Document != null)
			{
				e.Document.ImpliedSelectionChanged += OnImpliedSelectionChanged;
			}
		}

		private void OnImpliedSelectionChanged(object sender, EventArgs e)
		{
			if (!(sender is Document doc)) return;

			try
			{
				PromptSelectionResult result = doc.Editor.SelectImplied();
				if (result.Status == PromptStatus.OK && result.Value != null && result.Value.Count > 0)
				{
					LastSelectedIds = result.Value.GetObjectIds();
				}
			}
			catch
			{
				// Event này gọi liên tục nên bắt lỗi âm thầm để không spam Command Line
			}
		}
	}
}