using DV.Logic.Job;
using DV.UI.LocoHUD;
using DV.Utils;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
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
            bool jobRowMode = Main.settings.destinationOnJobRow;
            bool ownRow = dest != null && !jobRowMode;
            controller.cargoMassJobIdText = JobRowText(controller, jobRowMode ? dest : null);
            foreach (var plate in controller.trainCarPlates)
            {
                var tmp = GetOrCreateText(plate);
                if (tmp == null)
                    continue;
                tmp.text = ownRow ? dest!.PadLeft(RowWidth) : string.Empty;
                if (plate.cargoMassJobId != null)
                    plate.cargoMassJobId.text = controller.cargoMassJobIdText;
                if (plate.carMassLength != null)
                    plate.carMassLength.text = CarMassLengthText(controller);
                if (plate.cargoType == null)
                    continue;
                if (ownRow && Main.settings.ellipsizeCargoName)
                    CargoNameClipper.Apply(plate.cargoType, tmp);
                else
                    CargoNameClipper.Restore(plate.cargoType);
                if (ownRow)
                    AlignBaseline(plate.cargoType, tmp);
            }
        }

        /// <summary>Re-runs the game's own row rebuild (and through the patch,
        /// this mod's) on every live plate, so a settings change shows up
        /// without waiting for the next cargo or job event.</summary>
        public static void RefreshAll()
        {
            if (!Main.enabled)
                return;
            foreach (var controller in Resources.FindObjectsOfTypeAll<TrainCarPlatesController>())
            {
                // Prefabs and pooled copies have no plate list yet.
                if (controller.gameObject.scene.IsValid() && controller.trainCarPlates != null)
                    controller.RefreshDerivedCargoJobData();
            }
        }

        /// <summary>The job row, optionally with the destination squeezed in
        /// after the job id; the mass drops to tonnes ("36000kg" -> "36t")
        /// when so configured, which also frees the columns the destination
        /// takes.</summary>
        public static string JobRowText(TrainCarPlatesController controller, string? dest)
        {
            string mass = ShortenMass(controller.cargoMassText);
            string right = dest == null
                ? controller.jobIdText
                : controller.jobIdText + " " + dest;
            return mass + right.PadLeft(Mathf.Max(0, RowWidth - mass.Length));
        }

        /// <summary>The car's own mass-and-length row, rebuilt from the game's
        /// string so the tonnes option covers the base weight too.</summary>
        public static string CarMassLengthText(TrainCarPlatesController controller)
        {
            string text = controller.carMassLengthText;
            int kgAt = text.IndexOf("kg");
            if (kgAt < 0)
                return text;
            string mass = ShortenMass(text.Substring(0, kgAt + 2));
            string length = text.Substring(kgAt + 2).Trim();
            return mass + length.PadLeft(
                Mathf.Max(0, TrainCarPlate.VEHICLE_INFO_CHARACTERS_PER_ROW - mass.Length));
        }

        private static string ShortenMass(string mass)
        {
            if (Main.settings.massInTonnes
                && mass.EndsWith("kg") && int.TryParse(mass.Substring(0, mass.Length - 2), out var kg))
                return $"{kg / 1000f:0.#}t";
            return mass;
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

        /// <summary>The cargo name renders in a larger font than the job row
        /// the destination is cloned from, so sitting one line height above it
        /// leaves the baselines apart; snap the destination's baseline to the
        /// name's.</summary>
        private static void AlignBaseline(TextMeshPro row, TextMeshPro dest)
        {
            row.ForceMeshUpdate();
            dest.ForceMeshUpdate();
            if (row.textInfo.characterCount == 0 || dest.textInfo.characterCount == 0)
                return;
            var parent = dest.transform.parent;
            float rowBaseline = parent.InverseTransformPoint(
                row.transform.TransformPoint(new Vector3(0f, row.textInfo.lineInfo[0].baseline, 0f))).y;
            float destBaseline = parent.InverseTransformPoint(
                dest.transform.TransformPoint(new Vector3(0f, dest.textInfo.lineInfo[0].baseline, 0f))).y;
            var pos = dest.transform.localPosition;
            pos.y += rowBaseline - destBaseline;
            dest.transform.localPosition = pos;
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

    /// <summary>The destination shares its row with the cargo name, so long
    /// names run under it; clip the name with an ellipsis at the destination's
    /// left edge. Margin instead of rect width so nothing else (layout groups,
    /// the plate mesh) sees a size change. Units differ between the world
    /// plates and the HUD canvas, so the clip point comes from geometry, not
    /// character counts.</summary>
    internal static class CargoNameClipper
    {
        private static readonly Dictionary<TMP_Text, (TextOverflowModes overflow, float rightMargin)> originals
            = new Dictionary<TMP_Text, (TextOverflowModes, float)>();

        // What the game's rows use untouched; also the fallback when this
        // assembly was hot-reloaded and the remembered originals are gone but
        // the clip is still stuck on the live text.
        private static readonly (TextOverflowModes overflow, float rightMargin) Vanilla
            = (TextOverflowModes.Overflow, 0f);

        public static void Apply(TMP_Text row, TMP_Text dest)
        {
            float destLeft = LeftEdge(dest);
            if (float.IsNaN(destLeft))
                return;
            var world = dest.rectTransform.TransformPoint(new Vector3(destLeft, 0f, 0f));
            // Half a wide glyph of breathing room, in the row's own units.
            float gap = row.GetPreferredValues("M").x * 0.5f;
            float limit = row.rectTransform.InverseTransformPoint(world).x - gap;
            if (!originals.TryGetValue(row, out var original))
            {
                Prune();
                original = row.overflowMode == TextOverflowModes.Ellipsis
                    ? Vanilla
                    : (row.overflowMode, row.margin.z);
                originals[row] = original;
            }
            row.overflowMode = TextOverflowModes.Ellipsis;
            var margin = row.margin;
            margin.z = Mathf.Max(original.rightMargin, row.rectTransform.rect.xMax - limit);
            row.margin = margin;
        }

        public static void Restore(TMP_Text row)
        {
            if (!originals.TryGetValue(row, out var original))
            {
                if (row.overflowMode != TextOverflowModes.Ellipsis)
                    return;
                original = Vanilla;
            }
            else
            {
                originals.Remove(row);
            }
            row.overflowMode = original.overflow;
            var margin = row.margin;
            margin.z = original.rightMargin;
            row.margin = margin;
        }

        /// <summary>Unload/reload cleanup: put every row this assembly touched
        /// back, since the next load starts with an empty table.</summary>
        public static void RestoreAll()
        {
            foreach (var row in originals.Keys.ToList())
            {
                if (row != null)
                    Restore(row);
            }
            originals.Clear();
        }

        /// <summary>Plates are destroyed with their cars; drop dead keys before
        /// the map can grow past them.</summary>
        private static void Prune()
        {
            if (originals.Count < 64)
                return;
            var dead = originals.Keys.Where(key => key == null).ToList();
            foreach (var key in dead)
                originals.Remove(key);
        }

        /// <summary>Local x where the glyphs start, which leading alignment
        /// hides; NaN when nothing is visible.</summary>
        private static float LeftEdge(TMP_Text text)
        {
            text.ForceMeshUpdate();
            var info = text.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                if (info.characterInfo[i].isVisible)
                    return info.characterInfo[i].bottomLeft.x;
            }
            return float.NaN;
        }
    }

    /// <summary>The HUD renders the same strings in a proportional font, where
    /// the game's space padding no longer lines up as a column, so the
    /// destination is placed by measured geometry instead.</summary>
    internal static class HudDestination
    {
        private const string ObjectName = "WagonDestinationText";

        public static void Update(HUDTrainPlateInfo hud, TrainCarPlatesController controller)
        {
            var dest = PlateDestination.DestinationFor(controller);
            var src = hud.cargoMassJobId;
            var row = hud.cargoType;
            if (src == null || row == null)
                return;
            var tmp = GetOrCreateText(src);
            if (tmp == null)
                return;
            src.text = PlateDestination.JobRowText(
                controller, Main.settings.destinationOnJobRow ? dest : null);
            if (hud.carMassLength != null)
                hud.carMassLength.text = PlateDestination.CarMassLengthText(controller);
            if (string.IsNullOrEmpty(dest) || Main.settings.destinationOnJobRow)
            {
                tmp.text = string.Empty;
                CargoNameClipper.Restore(row);
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
            if (Main.settings.ellipsizeCargoName)
                CargoNameClipper.Apply(row, tmp);
            else
                CargoNameClipper.Restore(row);
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
            HudDestination.Update(__instance, controller);
        }
    }
}
