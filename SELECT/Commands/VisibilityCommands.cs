using System;
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
        [CommandMethod("AQ", CommandFlags.UsePickSet | CommandFlags.Redraw | CommandFlags.Modal)]
        public void HideObjects()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptSelectionResult selRes = GetSelectionWithPickFirst(ed, "\nChọn đối tượng để ẩn: ");
            if (selRes.Status != PromptStatus.OK) return;

            db.DisableUndoRecording(true); // Tắt Undo tạm thời để tăng tốc
            try
            {
                using (OpenCloseTransaction tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    HashSet<ObjectId> lockedLayers = GeomUtils.GetLockedLayerIds(tr, db);
                    int count = 0;
                    foreach (SelectedObject selObj in selRes.Value)
                    {
                        if (GeomUtils.FastSetVisibility(tr, selObj.ObjectId, false, lockedLayers))
                            count++;
                    }
                    tr.Commit();
                    if (count > 0) ed.WriteMessage($"\nĐã ẩn {count} đối tượng.");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\nLỗi AQ: " + ex.Message);
            }
            finally
            {
                db.DisableUndoRecording(false);
            }
        }

        [CommandMethod("QA", CommandFlags.Modal)]
        public void ShowAllObjects()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            TypedValue[] filterList = { new TypedValue(60, 1) };
            SelectionFilter filter = new SelectionFilter(filterList);
            PromptSelectionResult selRes = ed.SelectAll(filter);

            if (selRes.Status != PromptStatus.OK || selRes.Value.Count == 0)
            {
                ed.WriteMessage("\nKhông có đối tượng nào đang bị ẩn.");
                return;
            }

            db.DisableUndoRecording(true);
            try
            {
                using (OpenCloseTransaction tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    HashSet<ObjectId> lockedLayers = GeomUtils.GetLockedLayerIds(tr, db);
                    int count = 0;
                    foreach (SelectedObject selObj in selRes.Value)
                    {
                        if (GeomUtils.FastSetVisibility(tr, selObj.ObjectId, true, lockedLayers))
                            count++;
                    }
                    tr.Commit();
                    ed.WriteMessage($"\nĐã hiện lại {count} đối tượng.");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\nLỗi QA: " + ex.Message);
            }
            finally
            {
                db.DisableUndoRecording(false);
            }
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

            db.DisableUndoRecording(true);
            try
            {
                using (OpenCloseTransaction tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    HashSet<ObjectId> lockedLayers = GeomUtils.GetLockedLayerIds(tr, db);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                    int hiddenCount = 0;
                    foreach (ObjectId objId in btr)
                    {
                        if (!idsToKeep.Contains(objId) && !objId.IsErased)
                        {
                            if (GeomUtils.FastSetVisibility(tr, objId, false, lockedLayers))
                                hiddenCount++;
                        }
                    }
                    tr.Commit();
                    ed.WriteMessage($"\nĐã cô lập. Ẩn {hiddenCount} đối tượng.");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\nLỗi QQ: " + ex.Message);
            }
            finally
            {
                db.DisableUndoRecording(false);
            }
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