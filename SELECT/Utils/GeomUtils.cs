using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace UnifiedAutoCADTools.Utils
{
    public static class GeomUtils
    {
        /// <summary>
        /// Lấy tên Block kể cả Dynamic Block. 
        /// Tối ưu: Yêu cầu truyền Transaction đang mở để tránh tạo lồng transaction.
        /// </summary>
        public static string GetEffectiveName(Entity ent, Transaction tr)
        {
            BlockReference blkRef = ent as BlockReference;
            if (blkRef != null)
            {
                if (blkRef.IsDynamicBlock)
                {
                    // Sử dụng Transaction được truyền vào, không tạo mới
                    var btr = tr.GetObject(blkRef.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                    return btr != null ? btr.Name : blkRef.Name;
                }
                return blkRef.Name;
            }
            return "";
        }

        public static HashSet<ObjectId> GetLockedLayerIds(Transaction tr, Database db)
        {
            HashSet<ObjectId> lockedIds = new HashSet<ObjectId>();
            // Sử dụng LayerTableId trực tiếp, nhanh hơn mở SymbolTable
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            foreach (ObjectId layerId in layerTable)
            {
                var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
                if (ltr.IsLocked) lockedIds.Add(layerId);
            }
            return lockedIds;
        }

        public static bool FastSetVisibility(Transaction tr, ObjectId objId, bool makeVisible, HashSet<ObjectId> lockedLayers)
        {
            try
            {
                var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                if (ent == null) return false;

                // Check nhanh để tránh UpgradeOpen không cần thiết
                if (ent.Visible == makeVisible) return false;
                if (lockedLayers.Contains(ent.LayerId)) return false;

                ent.UpgradeOpen();
                ent.Visible = makeVisible;
                ent.DowngradeOpen(); // Downgrade ngay sau khi sửa xong
                return true;
            }
            catch
            {
                // Bỏ qua lỗi truy cập object (nếu có)
                return false;
            }
        }
    }
}