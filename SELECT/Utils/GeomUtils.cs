using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using AcRx = Autodesk.AutoCAD.Runtime;

namespace UnifiedAutoCADTools.Utils
{
	public static class GeomUtils
	{
		public static string GetEffectiveName(Entity ent, Transaction tr)
		{
			if (ent is BlockReference blkRef)
			{
				if (blkRef.IsDynamicBlock)
				{
					var btr = tr.GetObject(blkRef.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
					return btr != null ? btr.Name : blkRef.Name;
				}
				return blkRef.Name;
			}
			return string.Empty;
		}

		public static HashSet<ObjectId> GetLockedLayerIds(Transaction tr, Database db)
		{
			HashSet<ObjectId> lockedIds = new HashSet<ObjectId>();
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
			// Bảo vệ vòng ngoài: Bỏ qua ID không hợp lệ hoặc đã bị xóa
			if (objId.IsNull || !objId.IsValid || objId.IsErased) return false;

			try
			{
				var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
				if (ent == null) return false;

				// Tối ưu hiệu suất: Check nhanh để tránh UpgradeOpen không cần thiết
				if (ent.Visible == makeVisible) return false;
				if (lockedLayers.Contains(ent.LayerId)) return false;

				ent.UpgradeOpen();
				ent.Visible = makeVisible;
				ent.DowngradeOpen();
				return true;
			}
			catch (AcRx.Exception)
			{
				// Chỉ catch lỗi đặc thù của AutoCAD (VD: object lock bởi process khác). 
				// Các lỗi nghiêm trọng (Out of memory) vẫn sẽ ném ra để Host xử lý.
				return false;
			}
		}
	}
}