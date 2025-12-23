using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using UnifiedAutoCADTools.Utils;
using UnifiedAutoCADTools.UI;
using Exception = System.Exception;

namespace UnifiedAutoCADTools.Commands
{
    public class SelectSimilarCommands
    {
        // Class tiện ích giúp bảo vệ biến hệ thống (tự động khôi phục khi dùng xong)
        private class SysVarGuard : IDisposable
        {
            private readonly Dictionary<string, object> _originalValues = new Dictionary<string, object>();
            private readonly string[] _varsToMonitor = { "PICKFIRST", "PICKADD" };

            public SysVarGuard()
            {
                foreach (var name in _varsToMonitor)
                {
                    try
                    {
                        _originalValues[name] = Application.GetSystemVariable(name);
                    }
                    catch { /* Bỏ qua nếu biến không tồn tại */ }
                }
            }

            public void Dispose()
            {
                foreach (var pair in _originalValues)
                {
                    try
                    {
                        // Chỉ khôi phục nếu giá trị hiện tại khác giá trị gốc
                        object currentVal = Application.GetSystemVariable(pair.Key);
                        if (!currentVal.Equals(pair.Value))
                        {
                            Application.SetSystemVariable(pair.Key, pair.Value);
                        }
                    }
                    catch { }
                }
            }
        }

        private class EntityProperties
        {
            public string Layer { get; set; }
            public int ColorIndex { get; set; }
            public string Linetype { get; set; }
            public string Type { get; set; }
            public bool IsBlock { get; set; }
            public string BlockName { get; set; }
        }

        private class FilterOptions
        {
            public bool MatchLayer { get; set; }
            public bool MatchColor { get; set; }
            public bool MatchLinetype { get; set; }
            public bool MatchBlockName { get; set; }
        }

        private struct SearchCriteria
        {
            public string Type;
            public string BlockName;
        }

        /// <summary>
        /// Hàm hỗ trợ lấy vùng chọn đầu vào (Hỗ trợ PickFirst)
        /// </summary>
        private SelectionSet GetSelectionStep1(Editor ed, string promptMsg)
        {
            PromptSelectionResult implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
            {
                ed.SetImpliedSelection(new ObjectId[0]); // Xóa chọn cũ để tránh xung đột
                return implied.Value;
            }

            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = promptMsg
            };
            PromptSelectionResult psr = ed.GetSelection(pso);

            if (psr.Status == PromptStatus.OK) return psr.Value;
            if (psr.Status == PromptStatus.Error) return SelectAll(ed);

            return null;
        }

        [CommandMethod("SS", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void SelectSimilarFast()
        {
            // Bọc toàn bộ lệnh trong SysVarGuard để đảm bảo không đổi biến hệ thống
            using (new SysVarGuard())
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                Editor ed = doc.Editor;
                Database db = doc.Database;

                try
                {
                    // [Bước 1] Chọn vùng tìm kiếm
                    SelectionSet ss = GetSelectionStep1(ed, "\n[Bước 1] Quét vùng cần lọc (Enter = Tất cả): ");
                    if (ss == null || ss.Count == 0) return;

                    // [Bước 2] Chọn các đối tượng mẫu
                    PromptSelectionOptions psoSample = new PromptSelectionOptions
                    {
                        MessageForAdding = "\n[Bước 2] Chọn các đối tượng mẫu: ",
                        RejectObjectsOnLockedLayers = false
                    };

                    PromptSelectionResult psrSample = ed.GetSelection(psoSample);
                    if (psrSample.Status != PromptStatus.OK || psrSample.Value.Count == 0) return;

                    List<ObjectId> idsToSelect = new List<ObjectId>();

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        // 2.1. Thu thập thông tin mẫu
                        List<SearchCriteria> criteriaList = new List<SearchCriteria>();
                        foreach (SelectedObject sampleObj in psrSample.Value)
                        {
                            Entity sampleEnt = tr.GetObject(sampleObj.ObjectId, OpenMode.ForRead) as Entity;
                            if (sampleEnt == null) continue;

                            string type = sampleEnt.GetType().Name;
                            string blockName = (sampleEnt is BlockReference) ? GeomUtils.GetEffectiveName(sampleEnt, tr) : "";

                            criteriaList.Add(new SearchCriteria { Type = type, BlockName = blockName });
                        }

                        if (criteriaList.Count > 0)
                        {
                            // 2.2. Duyệt và lọc
                            foreach (SelectedObject sobj in ss)
                            {
                                Entity ent = tr.GetObject(sobj.ObjectId, OpenMode.ForRead) as Entity;
                                if (ent == null) continue;

                                string entType = ent.GetType().Name;
                                string entBlockName = (ent is BlockReference) ? GeomUtils.GetEffectiveName(ent, tr) : "";

                                bool match = false;
                                foreach (var criteria in criteriaList)
                                {
                                    if (entType == criteria.Type)
                                    {
                                        if (entType == "BlockReference")
                                        {
                                            if (entBlockName.Equals(criteria.BlockName, StringComparison.OrdinalIgnoreCase))
                                            {
                                                match = true;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            match = true;
                                            break;
                                        }
                                    }
                                }
                                if (match) idsToSelect.Add(ent.ObjectId);
                            }
                        }
                        tr.Commit();
                    }

                    // [Bước 3] Thực hiện chọn đối tượng (Ra ngoài Transaction)
                    if (idsToSelect.Count > 0)
                    {
                        ed.SetImpliedSelection(idsToSelect.ToArray());
                        ed.WriteMessage($"\nĐã chọn {idsToSelect.Count} đối tượng tương tự.");
                    }
                    else
                    {
                        ed.WriteMessage("\nKhông tìm thấy đối tượng tương tự trong vùng chọn.");
                    }
                }
                catch (Exception ex) { ed.WriteMessage($"\nLỗi SS: {ex.Message}"); }
            }
        }

        [CommandMethod("SSADV", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void SelectSimilarAdvanced()
        {
            using (new SysVarGuard())
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                Editor ed = doc.Editor;
                Database db = doc.Database;

                try
                {
                    // [Bước 1] Chọn vùng tìm kiếm
                    SelectionSet ss = GetSelectionStep1(ed, "\n[Bước 1] Quét vùng cần lọc (Enter = Tất cả): ");
                    if (ss == null || ss.Count == 0) return;

                    // [Bước 2] Chọn đối tượng mẫu
                    PromptEntityOptions peo = new PromptEntityOptions("\n[Bước 2] Chọn đối tượng mẫu: ");
                    PromptEntityResult per = ed.GetEntity(peo);
                    if (per.Status != PromptStatus.OK) return;

                    EntityProperties props = new EntityProperties();

                    // Transaction 1: Lấy thông tin mẫu
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        Entity ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
                        props.Layer = ent.Layer;
                        props.ColorIndex = ent.ColorIndex;
                        props.Linetype = ent.Linetype;
                        props.Type = ent.GetType().Name;
                        props.IsBlock = (ent is BlockReference);
                        if (props.IsBlock) props.BlockName = GeomUtils.GetEffectiveName(ent, tr);
                        tr.Commit();
                    }

                    FilterOptions options = new FilterOptions();
                    using (var dlg = new FilterDialog(props.IsBlock))
                    {
                        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                        options.MatchLayer = dlg.CheckLayer;
                        options.MatchColor = dlg.CheckColor;
                        options.MatchLinetype = dlg.CheckLinetype;
                        options.MatchBlockName = dlg.CheckBlockName;
                    }

                    List<ObjectId> idsToSelect = new List<ObjectId>();

                    // Transaction 2: Quét và lọc
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (SelectedObject sobj in ss)
                        {
                            Entity ent = tr.GetObject(sobj.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            bool match = true;

                            if (ent.GetType().Name != props.Type) match = false;
                            if (match && options.MatchLayer && ent.Layer != props.Layer) match = false;
                            if (match && options.MatchColor && ent.ColorIndex != props.ColorIndex) match = false;
                            if (match && options.MatchLinetype && ent.Linetype != props.Linetype) match = false;
                            if (match && options.MatchBlockName && props.IsBlock)
                            {
                                if (!GeomUtils.GetEffectiveName(ent, tr).Equals(props.BlockName, StringComparison.OrdinalIgnoreCase))
                                    match = false;
                            }
                            if (match) idsToSelect.Add(ent.ObjectId);
                        }
                        tr.Commit();
                    }

                    // [Bước 3] Chọn đối tượng (Ra ngoài Transaction)
                    if (idsToSelect.Count > 0)
                    {
                        ed.SetImpliedSelection(idsToSelect.ToArray());
                        ed.WriteMessage($"\nĐã chọn {idsToSelect.Count} đối tượng (SSADV).");
                    }
                    else
                    {
                        ed.WriteMessage("\nKhông tìm thấy đối tượng nào.");
                    }
                }
                catch (Exception ex) { ed.WriteMessage($"\nLỗi SSADV: {ex.Message}"); }
            }
        }

        private SelectionSet SelectAll(Editor ed)
        {
            PromptSelectionResult psr = ed.SelectAll();
            return (psr.Status == PromptStatus.OK) ? psr.Value : null;
        }
    }
}