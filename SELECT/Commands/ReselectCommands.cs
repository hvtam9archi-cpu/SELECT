using System;
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
                ed.WriteMessage("\nChưa có đối tượng nào để chọn lại.");
                return;
            }

            try
            {
                ObjectIdCollection validIds = new ObjectIdCollection();

                // Transaction ngắn chỉ để check ID validity
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in PluginInitialization.LastSelectedIds)
                    {
                        if (id.IsValid && !id.IsErased && id.Database == doc.Database)
                        {
                            validIds.Add(id);
                        }
                    }
                    tr.Commit();
                }

                if (validIds.Count > 0)
                {
                    ObjectId[] idsArray = new ObjectId[validIds.Count];
                    validIds.CopyTo(idsArray, 0);
                    ed.SetImpliedSelection(idsArray);
                    ed.WriteMessage("\nĐã chọn lại: {0} đối tượng.", validIds.Count);
                }
                else
                {
                    ed.WriteMessage("\nCác đối tượng cũ không còn tồn tại hoặc ở bản vẽ khác.");
                    PluginInitialization.LastSelectedIds = null;
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage("\nLỗi RS: " + ex.Message);
            }
        }
    }
}