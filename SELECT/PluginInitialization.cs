using System;
using System.Collections.Generic;
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
		// Tối ưu: Lưu Selection cuối cùng cho TỪNG TÀI LIỆU
		public static Dictionary<Document, ObjectId[]> LastSelectedIdsDict { get; private set; } = new Dictionary<Document, ObjectId[]>();

		public void Initialize()
		{
			try
			{
				DocumentCollection docManager = Application.DocumentManager;

				// 1. Quét qua TOÀN BỘ bản vẽ đang mở để đăng ký event (xử lý case NETLOAD muộn)
				foreach (Document doc in docManager)
				{
					if (doc != null)
					{
						doc.ImpliedSelectionChanged += OnImpliedSelectionChanged;
					}
				}

				// 2. Lắng nghe lúc tạo mới VÀ đóng tài liệu
				docManager.DocumentCreated += OnDocumentCreated;
				docManager.DocumentDestroyed += OnDocumentDestroyed;
			}
			catch (Exception ex)
			{
				Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n[Hệ thống] Lỗi khởi tạo Plugin: {ex.Message}");
			}
		}

		public void Terminate()
		{
			try
			{
				DocumentCollection docManager = Application.DocumentManager;
				if (docManager != null)
				{
					docManager.DocumentCreated -= OnDocumentCreated;
					docManager.DocumentDestroyed -= OnDocumentDestroyed;

					foreach (Document doc in docManager)
					{
						if (doc != null)
						{
							doc.ImpliedSelectionChanged -= OnImpliedSelectionChanged;
						}
					}
				}
				LastSelectedIdsDict.Clear();
			}
			catch { /* Im lặng khi Terminate */ }
		}

		private void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
		{
			if (e.Document != null)
			{
				e.Document.ImpliedSelectionChanged += OnImpliedSelectionChanged;
			}
		}

		private void OnDocumentDestroyed(object sender, DocumentDestroyedEventArgs e)
		{
			// Tránh Memory Leak do giữ mảng ObjectId của bản vẽ đã bị tắt
			var keysToRemove = new List<Document>();
			foreach (var key in LastSelectedIdsDict.Keys)
			{
				if (key.IsDisposed)
					keysToRemove.Add(key);
			}

			foreach (var key in keysToRemove)
			{
				LastSelectedIdsDict.Remove(key);
			}
		}

		private void OnImpliedSelectionChanged(object sender, EventArgs e)
		{
			if (!(sender is Document doc)) return;

			try
			{
				PromptSelectionResult result = doc.Editor.SelectImplied();

				// Cập nhật lại list kể cả khi người dùng nhấn Esc bỏ chọn (Selection rỗng)
				if (result.Status == PromptStatus.OK && result.Value != null && result.Value.Count > 0)
				{
					LastSelectedIdsDict[doc] = result.Value.GetObjectIds();
				}
			}
			catch
			{
				// Silent catch, theo đúng logic cũ. Không block AutoCAD nếu có sự cố background selection.
			}
		}
	}
}