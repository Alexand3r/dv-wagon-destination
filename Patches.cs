using DV.Logic.Job;
using DV.UI.LocoHUD;
using DV.Utils;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DvMod.WagonDestination
{
    internal static class PlateDestination
    {
        private const string ObjectName = "WagonDestinationText";
        private const int RowWidth = TrainCarPlate.CARGO_AND_JOB_INFO_CHARACTERS_PER_ROW;

        public static string? DestinationFor(TrainCarPlatesController controller)
        {
            // The plate only shows a job id while a job references this car;
            // mirror that so the destination appears and clears with it.
            if (string.IsNullOrEmpty(controller.jobIdText))
                return null;
            var logicCar = controller.trainCar?.logicCar;
            if (logicCar == null)
                return null;
            var job = SingletonBehaviour<JobsManager>.Instance?.GetJobOfCar(logicCar);
            if (job == null)
                return null;
            return FinalTrackForCar(job, logicCar) ?? job.chainData?.chainDestinationYardId;
        }

        public static void UpdatePlates(TrainCarPlatesController controller)
        {
            var dest = DestinationFor(controller);
            foreach (var plate in controller.trainCarPlates)
            {
                var tmp = GetOrCreateText(plate);
                if (tmp != null)
                    tmp.text = dest == null ? string.Empty : dest.PadLeft(RowWidth);
            }
        }

        /// <summary>Cloned from the job-id line so PadLeft columns match, and
        /// sat one line above it.</summary>
        private static TextMeshPro? GetOrCreateText(TrainCarPlate plate)
        {
            var src = plate.cargoMassJobId;
            if (src == null)
                return null;
            var parent = src.transform.parent;
            var existing = parent.Find(ObjectName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = Object.Instantiate(src.gameObject, parent);
                go.name = ObjectName;
            }
            go.transform.localRotation = src.transform.localRotation;
            go.transform.localScale = src.transform.localScale;
            var tmp = go.GetComponent<TextMeshPro>();
            if (tmp == null)
                return null;
            // Rendered line height, not font-asset math: TMP applies its own
            // spacing and auto-sizing on top of the font's metrics.
            tmp.text = "X";
            tmp.ForceMeshUpdate();
            float lineHeight = tmp.textInfo.lineInfo[0].lineHeight * go.transform.localScale.y;
            go.transform.localPosition = src.transform.localPosition + new Vector3(0f, lineHeight, 0f);
            tmp.text = string.Empty;
            return tmp;
        }

        /// <summary>Last match wins: a shunting job moves a car through several
        /// tracks, and only the final one is where it must end up.</summary>
        private static string? FinalTrackForCar(Job job, Car car)
        {
            string? result = null;
            foreach (var data in Flatten(job.tasks))
            {
                if (data.cars != null && data.cars.Contains(car) && data.destinationTrack != null)
                    result = data.destinationTrack.ID.FullDisplayID;
            }
            return result;
        }

        private static IEnumerable<TaskData> Flatten(IEnumerable<Task> tasks)
        {
            foreach (var task in tasks)
            {
                var data = task.GetTaskData();
                if (data.nestedTasks != null && data.nestedTasks.Count > 0)
                {
                    foreach (var nested in Flatten(data.nestedTasks))
                        yield return nested;
                }
                else
                {
                    yield return data;
                }
            }
        }
    }

    [HarmonyPatch(typeof(TrainCarPlatesController), "RefreshDerivedCargoJobData")]
    internal static class TrainCarPlatesControllerPatch
    {
        public static void Postfix(TrainCarPlatesController __instance)
        {
            PlateDestination.UpdatePlates(__instance);
        }
    }

    /// <summary>The HUD renders the same strings in a proportional font, where
    /// the game's space padding no longer lines up as a column, so the
    /// destination is placed by measured geometry instead.</summary>
    internal static class HudDestination
    {
        private const string ObjectName = "WagonDestinationText";

        public static void Update(HUDTrainPlateInfo hud, string? dest)
        {
            var src = hud.cargoMassJobId;
            var row = hud.cargoType;
            if (src == null || row == null)
                return;
            var tmp = GetOrCreateText(src);
            if (tmp == null)
                return;
            if (string.IsNullOrEmpty(dest))
            {
                tmp.text = string.Empty;
                return;
            }
            tmp.text = dest;
            // Copy and original share a local space, so the gap between their
            // right edges is how far the copy must move to align with the job id.
            float dx = RightEdge(src) - RightEdge(tmp);
            var parent = src.transform.parent;
            float dy = parent.InverseTransformPoint(row.transform.position).y
                - parent.InverseTransformPoint(src.transform.position).y;
            tmp.transform.localPosition = src.transform.localPosition + new Vector3(dx, dy, 0f);
        }

        /// <summary>Where the glyphs actually end, which trailing padding
        /// spaces and the field's own alignment both hide.</summary>
        private static float RightEdge(TMP_Text text)
        {
            text.ForceMeshUpdate();
            var info = text.textInfo;
            for (int i = info.characterCount - 1; i >= 0; i--)
            {
                if (info.characterInfo[i].isVisible)
                    return info.characterInfo[i].topRight.x;
            }
            return 0f;
        }

        private static TextMeshProUGUI? GetOrCreateText(TextMeshProUGUI src)
        {
            var parent = src.transform.parent;
            var existing = parent.Find(ObjectName);
            if (existing != null)
                return existing.GetComponent<TextMeshProUGUI>();
            var go = Object.Instantiate(src.gameObject, parent);
            go.name = ObjectName;
            // Keeps a layout group on the panel from reflowing the copy as an
            // extra row.
            var layout = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            // Inherited from the original, it would overwrite the destination
            // with a translated string on every language change.
            foreach (var component in go.GetComponents<Component>())
            {
                if (component.GetType().FullName == "DV.Localization.Localize")
                    Object.Destroy(component);
            }
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = string.Empty;
            return tmp;
        }
    }

    [HarmonyPatch(typeof(HUDTrainPlateInfo), "UpdateFromPlate")]
    internal static class HUDTrainPlateInfoPatch
    {
        public static void Postfix(HUDTrainPlateInfo __instance, TrainCarPlatesController controller)
        {
            HudDestination.Update(__instance, PlateDestination.DestinationFor(controller));
        }
    }
}
