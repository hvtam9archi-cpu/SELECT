using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using UnifiedAutoCADTools.Logic;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace UnifiedAutoCADTools.Commands
{
    public sealed class SelectSimilarCommands
    {
        [CommandMethod("SS", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void SelectSimilarFast()
        {
            Execute("SS", SelectSimilarLogic.ExecuteFast);
        }

        [CommandMethod("SSADV", CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public void SelectSimilarAdvanced()
        {
            Execute("SSADV", SelectSimilarLogic.ExecuteAdvanced);
        }

        private static void Execute(string commandName, Action<Document> commandAction)
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            try
            {
                commandAction(document);
            }
            catch (System.Exception exception)
            {
                document.Editor.WriteMessage($"\nLỗi {commandName}: {exception.Message}");
            }
        }
    }
}
