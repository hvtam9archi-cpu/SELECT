using System;
using System.Collections.Generic;
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
		private class SysVarGuard : IDisposable
		{
			private readonly Dictionary<string, object> _originalValues = new Dictionary<string, object>();
			private readonly string[] _varsToMonitor = { "PICKFIRST", "PICKADD" };

			public SysVarGuard()
			{
				foreach (var name in _varsToMonitor)
				{
					try { _originalValues[name] = Application.GetSystemVariable(name); }
					catch { /* Bỏ qua nếu biến không tồn tại */ }
				}
			}

			public void Dispose()
			{
				foreach (var pair in _originalValues)
				{
					try
					{
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
			public string DxfName { get; set; }
			public bool IsBlock { get; set; }
			public string BlockName { get; set; }
		}

		private struct SearchCriteria
		{
			public string Type;
			public string BlockName;
		}

		private SelectionSet GetSelectionStep1(Editor ed, string promptMsg)
		{
			PromptSelectionResult implied = ed.SelectImplied();
			if (implied.Status == PromptStatus.OK && implied.Value != null && implied.Value.Count > 0)
			{
				ed.SetImpliedSelection(new ObjectId[0]);
				return implied.Value;
			}

			PromptSelectionOptions pso = new PromptSelectionOptions { MessageForAdding = promptMsg };
			PromptSelectionResult psr = ed.GetSelection(pso);

			if (psr.Status == PromptStatus.OK) return psr.Value;
			if (psr.Status == PromptStatus.Error) return SelectAll(ed);

			return null;
		}

		private SelectionSet SelectAll(Editor ed)
		{
			PromptSelectionResult psr = ed.SelectAll();
			return (psr.Status == PromptStatus.OK) ? psr.Value : null;
		}

		[CommandMethod("SS", CommandFlags.UsePickSet | CommandFlags.Redraw)]
		public void SelectSimilarFast()
		{
			using (new SysVarGuard())
			{
				Document doc = Application.DocumentManager.MdiActiveDocument;
				Editor ed = doc.Editor;
				Database db = doc.Database;

				try
				{
					SelectionSet ss = GetSelectionStep1(ed, "\n[Bước 1] Quét vùng cần lọc (Enter = Tất cả): ");
					if (ss == null || ss.Count == 0) return;

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
												match = true; break;
											}
										}
										else { match = true; break; }
									}
								}
								if (match) idsToSelect.Add(ent.ObjectId);
							}
						}
						tr.Commit();
					}

					if (idsToSelect.Count > 0)
					{
						ed.SetImpliedSelection(idsToSelect.ToArray());
						ed.WriteMessage($"\nĐã chọn {idsToSelect.Count} đối tượng tương tự.");
					}
					else ed.WriteMessage("\nKhông tìm thấy đối tượng tương tự.");
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
					PromptEntityOptions peo = new PromptEntityOptions("\n[Bước 1] Chọn đối tượng mẫu: ");
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
						props.DxfName = ent.GetRXClass().DxfName;

						props.IsBlock = (ent is BlockReference);
						if (props.IsBlock) props.BlockName = GeomUtils.GetEffectiveName(ent, tr);
						tr.Commit();
					}

					bool matchLayer, matchColor, matchLinetype, matchBlockName;
					using (var dlg = new FilterDialog(props.IsBlock))
					{
						if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

						matchLayer = dlg.CheckLayer;
						matchColor = dlg.CheckColor;
						matchLinetype = dlg.CheckLinetype;
						matchBlockName = dlg.CheckBlockName;
					}

					// Xây dựng SelectionFilter tối ưu Core C++
					List<TypedValue> filterValues = new List<TypedValue>
					{
						new TypedValue((int)DxfCode.Start, props.DxfName)
					};

					if (matchLayer) filterValues.Add(new TypedValue((int)DxfCode.LayerName, props.Layer));
					if (matchColor) filterValues.Add(new TypedValue((int)DxfCode.Color, props.ColorIndex));
					if (matchLinetype) filterValues.Add(new TypedValue((int)DxfCode.LinetypeName, props.Linetype));
					if (matchBlockName && props.IsBlock) filterValues.Add(new TypedValue((int)DxfCode.BlockName, props.BlockName));

					SelectionFilter filter = new SelectionFilter(filterValues.ToArray());

					PromptSelectionOptions pso = new PromptSelectionOptions { MessageForAdding = "\n[Bước 3] Quét vùng cần lọc (Enter = Tất cả): " };
					PromptSelectionResult psr = ed.GetSelection(pso, filter);

					if (psr.Status == PromptStatus.Error) psr = ed.SelectAll(filter);

					if (psr.Status == PromptStatus.OK && psr.Value.Count > 0)
					{
						ed.SetImpliedSelection(psr.Value.GetObjectIds());
						ed.WriteMessage($"\nĐã chọn {psr.Value.Count} đối tượng.");
					}
					else
					{
						ed.WriteMessage("\nKhông tìm thấy đối tượng nào khớp yêu cầu.");
					}
				}
				catch (Exception ex) { ed.WriteMessage($"\nLỗi SSADV: {ex.Message}"); }
			}
		}
	}
}