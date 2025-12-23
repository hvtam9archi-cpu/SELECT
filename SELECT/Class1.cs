using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms; // Cần tham chiếu System.Windows.Forms
using System.Drawing;       // Cần tham chiếu System.Drawing

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

// Đăng ký các class xử lý lệnh và khởi tạo plugin
[assembly: CommandClass(typeof(UnifiedAutoCADTools.UnifiedCommands))]
[assembly: ExtensionApplication(typeof(UnifiedAutoCADTools.UnifiedPlugin))]

namespace UnifiedAutoCADTools
{
	/// <summary>
	/// Class khởi tạo Plugin - Chịu trách nhiệm xử lý sự kiện cho lệnh Reselect (RS)
	/// </summary>
	public class UnifiedPlugin : IExtensionApplication
	{
		public static ObjectId[] LastSelectedIds = null;

		public void Initialize()
		{
			DocumentCollection docManager = Application.DocumentManager;
			if (docManager.MdiActiveDocument != null)
			{
				docManager.MdiActiveDocument.ImpliedSelectionChanged += OnImpliedSelectionChanged;
			}
			docManager.DocumentCreated += (s, e) =>
			{
				e.Document.ImpliedSelectionChanged += OnImpliedSelectionChanged;
			};
		}

		public void Terminate() { }

		private void OnImpliedSelectionChanged(object sender, EventArgs e)
		{
			Document doc = sender as Document;
			if (doc != null)
			{
				PromptSelectionResult result = doc.Editor.SelectImplied();
				if (result.Status == PromptStatus.OK && result.Value != null && result.Value.Count > 0)
				{
					LastSelectedIds = result.Value.GetObjectIds();
				}
			}
		}
	}

	/// <summary>
	/// Class chứa toàn bộ các lệnh (RS, AQ, QA, QQ, SS, SSADV)
	/// </summary>
	public class UnifiedCommands
	{
		// =============================================================
		// PHẦN 1: RESELECT (CHỌN LẠI)
		// =============================================================
		[CommandMethod("RS")]
		public void ReselectObjects()
		{
			Document doc = Application.DocumentManager.MdiActiveDocument;
			Editor ed = doc.Editor;

			if (UnifiedPlugin.LastSelectedIds == null || UnifiedPlugin.LastSelectedIds.Length == 0)
			{
				ed.WriteMessage("\nChưa có đối tượng nào để chọn lại.");
				return;
			}

			try
			{
				ObjectIdCollection validIds = new ObjectIdCollection();
				using (Transaction tr = doc.TransactionManager.StartTransaction())
				{
					foreach (ObjectId id in UnifiedPlugin.LastSelectedIds)
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
					UnifiedPlugin.LastSelectedIds = null;
				}
			}
			catch (Exception ex) { ed.WriteMessage("\nLỗi: " + ex.Message); }
		}

		// =============================================================
		// PHẦN 2: HIDE - SHOW - ISOLATE (ẨN/HIỆN/CÔ LẬP)
		// =============================================================
		[CommandMethod("AQ", CommandFlags.UsePickSet | CommandFlags.Redraw | CommandFlags.Modal)]
		public void HideObjects()
		{
			Document doc = Application.DocumentManager.MdiActiveDocument;
			Database db = doc.Database;
			Editor ed = doc.Editor;

			PromptSelectionResult selRes = GetSelectionWithPickFirst(ed, "\nChọn đối tượng để ẩn: ");
			if (selRes.Status != PromptStatus.OK) return;

			db.DisableUndoRecording(true);
			try
			{
				using (OpenCloseTransaction tr = db.TransactionManager.StartOpenCloseTransaction())
				{
					HashSet<ObjectId> lockedLayers = GetLockedLayerIds(tr, db);
					int count = 0;
					foreach (SelectedObject selObj in selRes.Value)
					{
						if (FastSetVisibility(tr, selObj.ObjectId, false, lockedLayers)) count++;
					}
					tr.Commit();
					if (count > 0) ed.WriteMessage($"\nĐã ẩn {count} đối tượng.");
				}
			}
			catch (Exception ex) { ed.WriteMessage("\nLỗi: " + ex.Message); }
			finally { db.DisableUndoRecording(false); }
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
					HashSet<ObjectId> lockedLayers = GetLockedLayerIds(tr, db);
					int count = 0;
					foreach (SelectedObject selObj in selRes.Value)
					{
						if (FastSetVisibility(tr, selObj.ObjectId, true, lockedLayers)) count++;
					}
					tr.Commit();
					ed.WriteMessage($"\nĐã hiện lại {count} đối tượng.");
				}
			}
			finally { db.DisableUndoRecording(false); }
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
					HashSet<ObjectId> lockedLayers = GetLockedLayerIds(tr, db);
					BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

					int hiddenCount = 0;
					foreach (ObjectId objId in btr)
					{
						if (!idsToKeep.Contains(objId) && !objId.IsErased)
						{
							if (FastSetVisibility(tr, objId, false, lockedLayers)) hiddenCount++;
						}
					}
					tr.Commit();
					ed.WriteMessage($"\nĐã cô lập. Ẩn {hiddenCount} đối tượng.");
				}
			}
			finally { db.DisableUndoRecording(false); }
		}

		// =============================================================
		// PHẦN 3: SELECT SIMILAR (SS & SSADV)
		// =============================================================
		[CommandMethod("SS")]
		public void SelectSimilarFast()
		{
			Document doc = Application.DocumentManager.MdiActiveDocument;
			Editor ed = doc.Editor;
			Database db = doc.Database;

			try
			{
				PromptSelectionOptions pso = new PromptSelectionOptions
				{
					MessageForAdding = "\n[Bước 1] Quét vùng cần lọc (Enter = Tất cả): "
				};
				PromptSelectionResult psr = ed.GetSelection(pso);
				SelectionSet ss = (psr.Status == PromptStatus.OK) ? psr.Value :
								  (psr.Status == PromptStatus.Error ? SelectAll(ed) : null);

				if (ss == null || ss.Count == 0) return;

				PromptEntityOptions peo = new PromptEntityOptions("\n[Bước 2] Chọn đối tượng mẫu: ");
				peo.SetRejectMessage("\nBạn chưa chọn đối tượng.");
				PromptEntityResult per = ed.GetEntity(peo);
				if (per.Status != PromptStatus.OK) return;

				using (Transaction tr = db.TransactionManager.StartTransaction())
				{
					Entity sampleEnt = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
					if (sampleEnt == null) return;

					string sampleType = sampleEnt.GetType().Name;
					string sampleBlockName = GetEffectiveName(sampleEnt);
					List<ObjectId> idsToSelect = new List<ObjectId>();

					foreach (SelectedObject sobj in ss)
					{
						Entity ent = tr.GetObject(sobj.ObjectId, OpenMode.ForRead) as Entity;
						if (ent == null) continue;

						bool match = false;
						if (ent.GetType().Name == sampleType)
						{
							if (ent is BlockReference)
							{
								if (GetEffectiveName(ent).Equals(sampleBlockName, StringComparison.OrdinalIgnoreCase))
									match = true;
							}
							else match = true;
						}
						if (match) idsToSelect.Add(ent.ObjectId);
					}

					if (idsToSelect.Count > 0)
					{
						ed.SetImpliedSelection(idsToSelect.ToArray());
						ed.WriteMessage($"\nĐã chọn {idsToSelect.Count} đối tượng tương tự.");
					}
					else ed.WriteMessage("\nKhông tìm thấy đối tượng tương tự.");
					tr.Commit();
				}
			}
			catch (Exception ex) { ed.WriteMessage($"\nLỗi: {ex.Message}"); }
		}

		[CommandMethod("SSADV")]
		public void SelectSimilarAdvanced()
		{
			Document doc = Application.DocumentManager.MdiActiveDocument;
			Editor ed = doc.Editor;
			Database db = doc.Database;

			try
			{
				PromptSelectionOptions pso = new PromptSelectionOptions
				{
					MessageForAdding = "\n[Bước 1] Quét vùng cần lọc (Enter = Tất cả): "
				};
				PromptSelectionResult psr = ed.GetSelection(pso);
				SelectionSet ss = (psr.Status == PromptStatus.OK) ? psr.Value :
								  (psr.Status == PromptStatus.Error ? SelectAll(ed) : null);

				if (ss == null || ss.Count == 0) return;

				PromptEntityOptions peo = new PromptEntityOptions("\n[Bước 2] Chọn đối tượng mẫu: ");
				PromptEntityResult per = ed.GetEntity(peo);
				if (per.Status != PromptStatus.OK) return;

				EntityProperties props = new EntityProperties();
				using (Transaction tr = db.TransactionManager.StartTransaction())
				{
					Entity ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
					props.Layer = ent.Layer;
					props.ColorIndex = ent.ColorIndex;
					props.Linetype = ent.Linetype;
					props.Type = ent.GetType().Name;
					props.IsBlock = (ent is BlockReference);
					if (props.IsBlock) props.BlockName = GetEffectiveName(ent);
					tr.Commit();
				}

				FilterOptions options = new FilterOptions();
				using (var dlg = new FilterDialog(props.IsBlock))
				{
					// SỬA LỖI Ở ĐÂY: Chỉ định rõ System.Windows.Forms.DialogResult
					if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

					options.MatchLayer = dlg.CheckLayer;
					options.MatchColor = dlg.CheckColor;
					options.MatchLinetype = dlg.CheckLinetype;
					options.MatchBlockName = dlg.CheckBlockName;
				}

				List<ObjectId> idsToSelect = new List<ObjectId>();
				using (Transaction tr = db.TransactionManager.StartTransaction())
				{
					foreach (SelectedObject sobj in ss)
					{
						Entity ent = tr.GetObject(sobj.ObjectId, OpenMode.ForRead) as Entity;
						bool match = true;

						if (ent.GetType().Name != props.Type) match = false;
						if (match && options.MatchLayer && ent.Layer != props.Layer) match = false;
						if (match && options.MatchColor && ent.ColorIndex != props.ColorIndex) match = false;
						if (match && options.MatchLinetype && ent.Linetype != props.Linetype) match = false;
						if (match && options.MatchBlockName && props.IsBlock)
						{
							if (!GetEffectiveName(ent).Equals(props.BlockName, StringComparison.OrdinalIgnoreCase))
								match = false;
						}
						if (match) idsToSelect.Add(ent.ObjectId);
					}
					tr.Commit();
				}

				if (idsToSelect.Count > 0)
				{
					ed.SetImpliedSelection(idsToSelect.ToArray());
					ed.WriteMessage($"\nĐã chọn {idsToSelect.Count} đối tượng (SSADV).");
				}
				else ed.WriteMessage("\nKhông tìm thấy đối tượng nào.");
			}
			catch (Exception ex) { ed.WriteMessage($"\nLỗi: {ex.Message}"); }
		}

		// =============================================================
		// CÁC HÀM HỖ TRỢ (HELPER METHODS)
		// =============================================================

		private SelectionSet SelectAll(Editor ed)
		{
			PromptSelectionResult psr = ed.SelectAll();
			return (psr.Status == PromptStatus.OK) ? psr.Value : null;
		}

		private string GetEffectiveName(Entity ent)
		{
			BlockReference blkRef = ent as BlockReference;
			if (blkRef != null)
			{
				if (blkRef.IsDynamicBlock)
				{
					using (Transaction tr = ent.Database.TransactionManager.StartTransaction())
					{
						BlockTableRecord btr = tr.GetObject(blkRef.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
						tr.Commit();
						return btr.Name;
					}
				}
				return blkRef.Name;
			}
			return "";
		}

		private HashSet<ObjectId> GetLockedLayerIds(Transaction tr, Database db)
		{
			HashSet<ObjectId> lockedIds = new HashSet<ObjectId>();
			LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
			foreach (ObjectId layerId in lt)
			{
				LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);
				if (ltr.IsLocked) lockedIds.Add(layerId);
			}
			return lockedIds;
		}

		private bool FastSetVisibility(Transaction tr, ObjectId objId, bool makeVisible, HashSet<ObjectId> lockedLayers)
		{
			try
			{
				Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
				if (ent == null) return false;
				if (ent.Visible == makeVisible) return false;
				if (lockedLayers.Contains(ent.LayerId)) return false;

				ent.UpgradeOpen();
				ent.Visible = makeVisible;
				ent.DowngradeOpen();
				return true;
			}
			catch { return false; }
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

		// =============================================================
		// CÁC CLASS HỖ TRỢ DỮ LIỆU & UI
		// =============================================================

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

		public class FilterDialog : Form
		{
			private readonly CheckBox cbLayer;
			private readonly CheckBox cbColor;
			private readonly CheckBox cbLinetype;
			private readonly CheckBox cbBlock;
			private readonly Button btnOK;
			private readonly Button btnCancel;

			public bool CheckLayer => cbLayer.Checked;
			public bool CheckColor => cbColor.Checked;
			public bool CheckLinetype => cbLinetype.Checked;
			public bool CheckBlockName => cbBlock.Checked;

			public FilterDialog(bool isBlock)
			{
				this.Text = "Lọc Đối Tượng Nâng Cao";
				this.Size = new Size(250, 220);
				this.StartPosition = FormStartPosition.CenterScreen;
				this.FormBorderStyle = FormBorderStyle.FixedDialog;
				this.MaximizeBox = false;
				this.MinimizeBox = false;

				int y = 20;
				int x = 20;

				cbLayer = CreateCheck("Layer", x, y, true);
				y += 30;
				cbColor = CreateCheck("Color", x, y, false);
				y += 30;
				cbLinetype = CreateCheck("Linetype", x, y, false);
				y += 30;
				cbBlock = CreateCheck("Block Name", x, y, true);

				cbBlock.Enabled = isBlock;
				if (!isBlock) cbBlock.Checked = false;

				y += 40;

				btnOK = new Button
				{
					Text = "OK",
					Location = new Point(30, y),
					// SỬA LỖI Ở ĐÂY: Dùng System.Windows.Forms.DialogResult.OK
					DialogResult = System.Windows.Forms.DialogResult.OK
				};

				btnCancel = new Button
				{
					Text = "Cancel",
					Location = new Point(120, y),
					// SỬA LỖI Ở ĐÂY: Dùng System.Windows.Forms.DialogResult.Cancel
					DialogResult = System.Windows.Forms.DialogResult.Cancel
				};

				this.Controls.AddRange(new Control[] { cbLayer, cbColor, cbLinetype, cbBlock, btnOK, btnCancel });
				this.AcceptButton = btnOK;
				this.CancelButton = btnCancel;
			}

			private CheckBox CreateCheck(string text, int x, int y, bool isChecked)
			{
				return new CheckBox
				{
					Text = text,
					Location = new Point(x, y),
					Checked = isChecked,
					AutoSize = true
				};
			}
		}
	}
}