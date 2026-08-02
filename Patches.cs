using DV.Logic.Job;
using DV.UI.LocoHUD;
using DV.Utils;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DvMod.WagonDestination
{
    internal static class PlateDestination
    {
        private const string ObjectName = "WagonDestinationText";
        private const int RowWidth = TrainCarPlate.CARGO_AND_JOB_INFO_CHARACTERS_PER_ROW;

        /// <summary>Destination track for this car ("GF-D6I"), or the chain's
        /// destination yard when no task names a track. Null without a job.</summary>
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

        /// <summary>A dedicated text object cloned from the job-id line: same
        /// font, size and row geometry, one line higher — so PadLeft lands the
        /// destination in exactly the job id's column.</summary>
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
            // Measure the actual rendered line height (TMP's own metrics, in
            // the text object's local units) instead of trusting font-asset
            // math, then convert to the parent's space via the local scale.
            tmp.text = "X";
            tmp.ForceMeshUpdate();
            float lineHeight = tmp.textInfo.lineInfo[0].lineHeight * go.transform.localScale.y;
            go.transform.localPosition = src.transform.localPosition + new Vector3(0f, lineHeight, 0f);
            tmp.text = string.Empty;
            return tmp;
        }

        /// <summary>The last task in the job that moves this car to a named
        /// track — its destination is where the car must end up. Covers
        /// per-cut targets in shunting jobs, not just the chain's final yard.</summary>
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

    /// <summary>Runs after the game rewrites the cargo/job rows (job assigned,
    /// cargo loaded/unloaded), for the physical plates on the car.</summary>
    [HarmonyPatch(typeof(TrainCarPlatesController), "RefreshDerivedCargoJobData")]
    internal static class TrainCarPlatesControllerPatch
    {
        public static void Postfix(TrainCarPlatesController __instance)
        {
            PlateDestination.UpdatePlates(__instance);
        }
    }

    /// <summary>The loco HUD mirrors the plate's text fields; its layout is
    /// proportional, so the destination rides the cargo row with a plain
    /// separator instead of column padding.</summary>
    [HarmonyPatch(typeof(HUDTrainPlateInfo), "UpdateFromPlate")]
    internal static class HUDTrainPlateInfoPatch
    {
        public static void Postfix(HUDTrainPlateInfo __instance, TrainCarPlatesController controller)
        {
            var dest = PlateDestination.DestinationFor(controller);
            if (!string.IsNullOrEmpty(dest))
                __instance.cargoType.text = controller.cargoTypeText + "   " + dest;
        }
    }
}
