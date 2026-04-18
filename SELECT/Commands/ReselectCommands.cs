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
		[CommandMethod("RS")]
		public void ReselectObjects()
		{
			Document doc = Application.DocumentManager.MdiActiveDocument;
			Editor ed = doc.Editor;

			if (PluginInitialization.LastSelectedIds == null || PluginInitialization.LastSelectedIds.Length == 0)
			{
				ed.WriteMessage("\n[Hệ thống] Chưa có đối tượng nào để chọn lại.");
				return;
			}

			try
			{
				List<ObjectId> validIds = new List<ObjectId>(PluginInitialization.LastSelectedIds.Length);

				using (OpenCloseTransaction tr = doc.TransactionManager.StartOpenCloseTransaction())
				{
					foreach (ObjectId id in PluginInitialization.LastSelectedIds)
					{
						if (id.IsValid && !id.IsNull && !id.IsErased && id.Database == doc.Database)
						{
							validIds.Add(id);
						}
					}
					tr.Commit();
				}

				if (validIds.Count > 0)
				{
					ed.SetImpliedSelection(validIds.ToArray());
					ed.WriteMessage($"\nĐã khôi phục vùng chọn: {validIds.Count} đối tượng.");
				}
				else
				{
					ed.WriteMessage("\nCác đối tượng cũ đã bị xóa hoặc thuộc bản vẽ khác.");
					PluginInitialization.LastSelectedIds = null; // Clean up memory
				}
			}
			catch (Exception ex)
			{
				ed.WriteMessage($"\nLỗi RS: {ex.Message}");
			}
		}
	}
}