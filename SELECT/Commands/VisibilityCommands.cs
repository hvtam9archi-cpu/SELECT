using System.Collections.Generic;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using UnifiedAutoCADTools.Utils;
using Exception = System.Exception;

namespace UnifiedAutoCADTools.Commands
{
	public class VisibilityCommands
	{
		// --- Core Helper: Tái sử dụng logic Transaction an toàn ---
		private void RunVisibilityTransaction(Database db, Editor ed, string cmdName, System.Action<OpenCloseTransaction, HashSet<ObjectId>> coreLogic)
		{
			db.DisableUndoRecording(true);
			try
			{
				using (OpenCloseTransaction tr = db.TransactionManager.StartOpenCloseTransaction())
				{
					HashSet<ObjectId> lockedLayers = GeomUtils.GetLockedLayerIds(tr, db);
					coreLogic(tr, lockedLayers);
					tr.Commit();
				}
			}
			catch (Exception ex)
			{
				ed.WriteMessage($"\nLỗi {cmdName}: {ex.Message}");
			}
			finally
			{
				db.DisableUndoRecording(false);
			}
		}

		[CommandMethod("AQ", CommandFlags.UsePickSet | CommandFlags.Redraw | CommandFlags.Modal)]
		public void HideObjects()
		{
			Document doc = Application.DocumentManager.MdiActiveDocument;
			Editor ed = doc.Editor;

			PromptSelectionResult selRes = GetSelectionWithPickFirst(ed, "\nChọn đối tượng để ẩn: ");
			if (selRes.Status != PromptStatus.OK) return;

			RunVisibilityTransaction(doc.Database, ed, "AQ", (tr, lockedLayers) =>
			{
				int count = 0;
				foreach (SelectedObject selObj in selRes.Value)
				{
					if (GeomUtils.FastSetVisibility(tr, selObj.ObjectId, false, lockedLayers))
						count++;
				}
				if (count > 0) ed.WriteMessage($"\nĐã ẩn {count} đối tượng.");
			});
		}

		[CommandMethod("QA", CommandFlags.Modal)]
		public void ShowAllObjects()
		{
			Document doc = Application.DocumentManager.MdiActiveDocument;
			Editor ed = doc.Editor;

			TypedValue[] filterList = { new TypedValue(60, 1) };
			SelectionFilter filter = new SelectionFilter(filterList);
			PromptSelectionResult selRes = ed.SelectAll(filter);

			if (selRes.Status != PromptStatus.OK || selRes.Value.Count == 0)
			{
				ed.WriteMessage("\nKhông có đối tượng nào đang bị ẩn.");
				return;
			}

			RunVisibilityTransaction(doc.Database, ed, "QA", (tr, lockedLayers) =>
			{
				int count = 0;
				foreach (SelectedObject selObj in selRes.Value)
				{
					if (GeomUtils.FastSetVisibility(tr, selObj.ObjectId, true, lockedLayers))
						count++;
				}
				ed.WriteMessage($"\nĐã hiện lại {count} đối tượng.");
			});
		}

		[CommandMethod("QQ", CommandFlags.UsePickSet | CommandFlags.Redraw | CommandFlags.Modal)]
		public void IsolateObjects()
		{
			Document doc = Application.DocumentManager.MdiActiveDocument;
			Database db = doc.Database;
			Editor ed = doc.Editor;

			PromptSelectionResult keepSel = GetSelectionWithPickFirst(ed, "\nChọn đối tượng để cô lập (giữ lại): ");
			if (keepSel.Status != PromptStatus.OK) return;

			HashSet<ObjectId> idsToKeep = new HashSet<ObjectId>(keepSel.Value.GetObjectIds());

			RunVisibilityTransaction(db, ed, "QQ", (tr, lockedLayers) =>
			{
				BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
				int hiddenCount = 0;

				foreach (ObjectId objId in btr)
				{
					if (!objId.IsErased && !idsToKeep.Contains(objId))
					{
						if (GeomUtils.FastSetVisibility(tr, objId, false, lockedLayers))
							hiddenCount++;
					}
				}
				ed.WriteMessage($"\nĐã cô lập. Ẩn {hiddenCount} đối tượng.");
			});
		}

		private PromptSelectionResult GetSelectionWithPickFirst(Editor ed, string promptMsg)
		{
			PromptSelectionResult impliedSel = ed.SelectImplied();
			if (impliedSel.Status == PromptStatus.OK && impliedSel.Value.Count > 0)
			{
				ed.SetImpliedSelection(new ObjectId[0]);
				return impliedSel;
			}
			PromptSelectionOptions pso = new PromptSelectionOptions { MessageForAdding = promptMsg };
			return ed.GetSelection(pso);
		}
	}
}