using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using GDMENUCardManager.Core;
using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;

namespace GDMENUCardManager
{
    internal static class DragDropHandler
    {
        private static bool IsMenu(object o) {
            return o is GdItem g &&
            (g.Name == "GDMENU" || g.Name == "openMenu" ||
            g.Ip?.Name == "GDMENU" || g.Ip?.Name == "openMenu");
        }

        public static void DragOver(IDropInfo dropInfo)
        {
            var list = dropInfo.TargetCollection.TryGetList();
            bool menuAtTop = list != null && list.Count > 0 && IsMenu(list[0]);

            // only reserve index 0 when a menu actually occupies it
            if (menuAtTop && dropInfo.UnfilteredInsertIndex == 0)
                return;

            if (dropInfo.DragInfo == null)
            {
                if (dropInfo.Data is DataObject data && data.ContainsFileDropList())
                    dropInfo.Effects = DragDropEffects.Copy;
            }
            else
            {
                // cant drag the menu itself
                var dragged = DefaultDropHandler.ExtractData(dropInfo.Data).OfType<object>();
                if (menuAtTop && dragged.Any(IsMenu))
                    return;

                if (DefaultDropHandler.CanAcceptData(dropInfo))
                    dropInfo.Effects = DragDropEffects.Move;
            }

            if (dropInfo.Effects != DragDropEffects.None)
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
        }

        /// <summary>
        /// Dropped files go through <see cref="Manager.AddGames"/>, so they get the same
        /// invalid file and duplicate report as the add buttons. Row moves report nothing.
        /// </summary>
        public static async Task<(List<string> Invalid, List<string> SkippedDuplicates)> Drop(IDropInfo dropInfo, Manager manager)
        {
            var insertIndex = dropInfo.UnfilteredInsertIndex;
            var destinationList = dropInfo.TargetCollection.TryGetList();

            bool menuAtTop = destinationList != null && destinationList.Count > 0 && IsMenu(destinationList[0]);
            if (menuAtTop && insertIndex == 0)
                insertIndex = 1; // never above the menu

            if (dropInfo.DragInfo == null)
            {
                if (dropInfo.Data is DataObject data && data.ContainsFileDropList())
                    return await manager.AddGames(data.GetFileDropList().Cast<string>().ToList(), insertIndex);
            }
            else
            {
                var data = DefaultDropHandler.ExtractData(dropInfo.Data).OfType<object>().ToList();

                var sourceList = dropInfo.DragInfo.SourceCollection.TryGetList();
                if (sourceList != null)
                {
                    foreach (var o in data)
                    {
                        if (menuAtTop && IsMenu(o))
                            continue;

                        var index = sourceList.IndexOf(o);
                        if (index != -1)
                        {
                            sourceList.RemoveAt(index);
                            if (destinationList != null && Equals(sourceList, destinationList) && index < insertIndex)
                                --insertIndex;
                        }
                    }
                }

                if (destinationList != null)
                    foreach (var o in data)
                        destinationList.Insert(insertIndex++, o);
            }
            return (new List<string>(), new List<string>());
        }
    }
}
