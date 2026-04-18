using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Exception = System.Exception;

namespace UnifiedAutoCADTools.Commands
{
	public class ReselectCommands
	{
		[CommandMethod("RS", CommandFlags.Modal | CommandFlags.UsePickSet)]
		public void ReselectObjects()
		{
			Document doc = Application.DocumentManager.MdiActiveDocument;
			if (doc == null) return;
			
			Editor ed = doc.Editor;

			try
			{
				// Truy xuất IDs của tài liệu CỤ THỂ đang mở
				if (!PluginInitialization.LastSelectedIdsDict.TryGetValue(doc, out ObjectId[] lastIds) || lastIds == null || lastIds.Length == 0)
				{
					ed.WriteMessage("\n[Hệ thống] Chưa có đối tượng nào để chọn lại trong bản vẽ này.");
					return;
				}

				// Capacity để list khỏi phải resize tự động, nâng hiệu suất chút đỉnh
				List<ObjectId> validIds = new List<ObjectId>(lastIds.Length);

				using (OpenCloseTransaction tr = doc.TransactionManager.StartOpenCloseTransaction())
				{
					foreach (ObjectId id in lastIds)
					{
						// Kiểm tra an toàn trước khi trả ID vào lại vùng chọn
						if (id.IsValid && !id.IsNull && !id.IsErased && id.IsResident && id.Database == doc.Database)
						{
							validIds.Add(id);
						}
					}
					tr.Commit();
				}

				if (validIds.Count > 0)
				{
					ed.SetImpliedSelection(validIds.ToArray());
					ed.WriteMessage($"\n[Hệ thống] Đã khôi phục vùng chọn: {validIds.Count} đối tượng.");
				}
				else
				{
					ed.WriteMessage("\n[Hệ thống] Các đối tượng cũ đã bị xóa hoàn toàn.");
					PluginInitialization.LastSelectedIdsDict.Remove(doc); // Dọn rác
				}
			}
			catch (Exception ex)
			{
				ed.WriteMessage($"\nLỗi RS: {ex.Message}");
			}
		}
	}
}