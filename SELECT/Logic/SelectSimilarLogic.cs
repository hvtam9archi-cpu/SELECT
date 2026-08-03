using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using UnifiedAutoCADTools.UI;
using UnifiedAutoCADTools.Utils;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace UnifiedAutoCADTools.Logic
{
    internal static class SelectSimilarLogic
    {
        internal static void ExecuteFast(Document document)
        {
            Editor editor = document.Editor;
            ObjectId[] searchSpaceIds = GetSearchSpaceObjectIds(
                editor,
                "\n[Bước 1] Quét vùng cần lọc (Enter = Tất cả): ");

            if (searchSpaceIds.Length == 0)
            {
                return;
            }

            PromptSelectionOptions sampleOptions = new PromptSelectionOptions
            {
                MessageForAdding = "\n[Bước 2] Chọn các đối tượng mẫu: ",
                RejectObjectsOnLockedLayers = false
            };

            PromptSelectionResult sampleResult = editor.GetSelection(sampleOptions);
            if (sampleResult.Status != PromptStatus.OK ||
                sampleResult.Value == null ||
                sampleResult.Value.Count == 0)
            {
                return;
            }

            List<ObjectId> matchingIds = new List<ObjectId>();

            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                FastSearchCriteria criteria = CreateFastCriteria(
                    sampleResult.Value.GetObjectIds(),
                    transaction);

                if (!criteria.IsEmpty)
                {
                    foreach (ObjectId candidateId in searchSpaceIds)
                    {
                        Entity candidate = transaction.GetObject(candidateId, OpenMode.ForRead, false) as Entity;
                        if (candidate != null && criteria.Matches(candidate, transaction))
                        {
                            matchingIds.Add(candidateId);
                        }
                    }
                }

                transaction.Commit();
            }

            SetResultSelection(editor, matchingIds);
        }

        internal static void ExecuteAdvanced(Document document)
        {
            Editor editor = document.Editor;
            PromptEntityOptions sampleOptions = new PromptEntityOptions(
                "\n[Bước 1] Chọn đối tượng mẫu: ");
            PromptEntityResult sampleResult = editor.GetEntity(sampleOptions);

            if (sampleResult.Status != PromptStatus.OK)
            {
                return;
            }

            EntitySample sample;
            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                Entity sampleEntity = transaction.GetObject(
                    sampleResult.ObjectId,
                    OpenMode.ForRead,
                    false) as Entity;

                if (sampleEntity == null)
                {
                    transaction.Abort();
                    editor.WriteMessage("\nKhông thể đọc đối tượng mẫu.");
                    return;
                }

                sample = EntitySample.FromEntity(sampleEntity, transaction);
                transaction.Commit();
            }

            FilterDialogWindow dialog = new FilterDialogWindow(sample.IsBlock);
            if (Application.ShowModalWindow(dialog) != true)
            {
                return;
            }

            AdvancedSearchCriteria criteria = new AdvancedSearchCriteria(
                dialog.CheckLayer,
                dialog.CheckColor,
                dialog.CheckLinetype,
                dialog.CheckBlockName);

            SelectionFilter nativeFilter = CreateNativeFilter(sample, criteria);
            PromptSelectionOptions searchOptions = new PromptSelectionOptions
            {
                MessageForAdding = "\n[Bước 3] Quét vùng cần lọc (Enter = Tất cả): "
            };

            PromptSelectionResult candidateResult = editor.GetSelection(searchOptions, nativeFilter);
            if (candidateResult.Status == PromptStatus.Error ||
                candidateResult.Status == PromptStatus.None)
            {
                candidateResult = editor.SelectAll(nativeFilter);
            }

            if (candidateResult.Status != PromptStatus.OK ||
                candidateResult.Value == null ||
                candidateResult.Value.Count == 0)
            {
                editor.WriteMessage("\nKhông tìm thấy đối tượng nào khớp yêu cầu.");
                return;
            }

            ObjectId[] candidateIds = candidateResult.Value.GetObjectIds();
            List<ObjectId> matchingIds = new List<ObjectId>(candidateIds.Length);

            using (DocumentLock documentLock = document.LockDocument())
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId candidateId in candidateIds)
                {
                    Entity candidate = transaction.GetObject(candidateId, OpenMode.ForRead, false) as Entity;
                    if (candidate != null && MatchesAdvanced(candidate, sample, criteria, transaction))
                    {
                        matchingIds.Add(candidateId);
                    }
                }

                transaction.Commit();
            }

            SetResultSelection(editor, matchingIds);
        }

        private static ObjectId[] GetSearchSpaceObjectIds(Editor editor, string promptMessage)
        {
            PromptSelectionResult impliedResult = editor.SelectImplied();
            if (impliedResult.Status == PromptStatus.OK &&
                impliedResult.Value != null &&
                impliedResult.Value.Count > 0)
            {
                ObjectId[] impliedIds = impliedResult.Value.GetObjectIds();
                editor.SetImpliedSelection(Array.Empty<ObjectId>());
                return impliedIds;
            }

            PromptSelectionOptions options = new PromptSelectionOptions
            {
                MessageForAdding = promptMessage
            };

            PromptSelectionResult result = editor.GetSelection(options);
            if (result.Status == PromptStatus.Error || result.Status == PromptStatus.None)
            {
                result = editor.SelectAll();
            }

            return result.Status == PromptStatus.OK && result.Value != null
                ? result.Value.GetObjectIds()
                : Array.Empty<ObjectId>();
        }

        private static FastSearchCriteria CreateFastCriteria(
            IEnumerable<ObjectId> sampleIds,
            Transaction transaction)
        {
            FastSearchCriteria criteria = new FastSearchCriteria();

            foreach (ObjectId sampleId in sampleIds)
            {
                Entity sample = transaction.GetObject(sampleId, OpenMode.ForRead, false) as Entity;
                if (sample == null)
                {
                    continue;
                }

                if (sample is BlockReference)
                {
                    string effectiveName = GeomUtils.GetEffectiveName(sample, transaction);
                    if (!string.IsNullOrEmpty(effectiveName))
                    {
                        criteria.BlockNames.Add(effectiveName);
                    }
                }
                else
                {
                    criteria.EntityDxfNames.Add(sample.GetRXClass().DxfName);
                }
            }

            return criteria;
        }

        private static SelectionFilter CreateNativeFilter(
            EntitySample sample,
            AdvancedSearchCriteria criteria)
        {
            List<TypedValue> values = new List<TypedValue>
            {
                new TypedValue((int)DxfCode.Start, sample.DxfName)
            };

            if (criteria.MatchLayer)
            {
                values.Add(new TypedValue((int)DxfCode.LayerName, sample.Layer));
            }

            // Color, linetype, and effective dynamic-block name are verified below.
            // Their DXF groups may be omitted for ByLayer values or contain an
            // anonymous block name, which would otherwise create false negatives.
            return new SelectionFilter(values.ToArray());
        }

        private static bool MatchesAdvanced(
            Entity candidate,
            EntitySample sample,
            AdvancedSearchCriteria criteria,
            Transaction transaction)
        {
            if (!string.Equals(
                    candidate.GetRXClass().DxfName,
                    sample.DxfName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (criteria.MatchLayer &&
                !string.Equals(candidate.Layer, sample.Layer, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (criteria.MatchColor && candidate.EntityColor != sample.Color)
            {
                return false;
            }

            if (criteria.MatchLinetype &&
                !string.Equals(candidate.Linetype, sample.Linetype, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!criteria.MatchBlockName)
            {
                return true;
            }

            if (!(candidate is BlockReference))
            {
                return false;
            }

            string candidateBlockName = GeomUtils.GetEffectiveName(candidate, transaction);
            return string.Equals(
                candidateBlockName,
                sample.BlockName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void SetResultSelection(Editor editor, List<ObjectId> matchingIds)
        {
            if (matchingIds.Count == 0)
            {
                editor.SetImpliedSelection(Array.Empty<ObjectId>());
                editor.WriteMessage("\nKhông tìm thấy đối tượng tương tự.");
                return;
            }

            editor.SetImpliedSelection(matchingIds.ToArray());
            editor.WriteMessage($"\nĐã chọn {matchingIds.Count} đối tượng tương tự.");
        }

        private sealed class FastSearchCriteria
        {
            internal FastSearchCriteria()
            {
                EntityDxfNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                BlockNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            internal HashSet<string> EntityDxfNames { get; }

            internal HashSet<string> BlockNames { get; }

            internal bool IsEmpty => EntityDxfNames.Count == 0 && BlockNames.Count == 0;

            internal bool Matches(Entity candidate, Transaction transaction)
            {
                if (candidate is BlockReference)
                {
                    string effectiveName = GeomUtils.GetEffectiveName(candidate, transaction);
                    return BlockNames.Contains(effectiveName);
                }

                return EntityDxfNames.Contains(candidate.GetRXClass().DxfName);
            }
        }

        private sealed class EntitySample
        {
            private EntitySample()
            {
            }

            internal string Layer { get; private set; }

            internal EntityColor Color { get; private set; }

            internal string Linetype { get; private set; }

            internal string DxfName { get; private set; }

            internal bool IsBlock { get; private set; }

            internal string BlockName { get; private set; }

            internal static EntitySample FromEntity(Entity entity, Transaction transaction)
            {
                bool isBlock = entity is BlockReference;
                return new EntitySample
                {
                    Layer = entity.Layer,
                    Color = entity.EntityColor,
                    Linetype = entity.Linetype,
                    DxfName = entity.GetRXClass().DxfName,
                    IsBlock = isBlock,
                    BlockName = isBlock
                        ? GeomUtils.GetEffectiveName(entity, transaction)
                        : string.Empty
                };
            }
        }

        private sealed class AdvancedSearchCriteria
        {
            internal AdvancedSearchCriteria(
                bool matchLayer,
                bool matchColor,
                bool matchLinetype,
                bool matchBlockName)
            {
                MatchLayer = matchLayer;
                MatchColor = matchColor;
                MatchLinetype = matchLinetype;
                MatchBlockName = matchBlockName;
            }

            internal bool MatchLayer { get; }

            internal bool MatchColor { get; }

            internal bool MatchLinetype { get; }

            internal bool MatchBlockName { get; }
        }
    }
}
