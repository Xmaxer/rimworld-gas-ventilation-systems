using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>
/// Replaces the old FloatMenu target-type picker, and is the only UI for the new explicit sensor-to-device
/// linking (see CompIntruderSensor.ToggleLink / ToggleDoorLink): a checklist of every vent on the map that can
/// run in Sensor mode, and a second checklist of every powered door, so one sensor can drive an arbitrary subset
/// of each instead of only "everything in this room". With no vents checked, the sensor falls back to the
/// original room-based behaviour; doors have no such fallback, since a door only ever locks because of an
/// explicit link.
/// </summary>
public sealed class ITab_GasSensor : ITab
{
    private static readonly Vector2 WinSize = new Vector2(320f, 620f);
    private const float RowHeight = 26f;

    private Vector2 ventScroll;
    private Vector2 doorScroll;

    private CompIntruderSensor Sensor => (SelThing as ThingWithComps)?.GetComp<CompIntruderSensor>();

    public ITab_GasSensor()
    {
        size = WinSize;
        labelKey = "GV_GasSensorTab";
    }

    protected override void FillTab()
    {
        CompIntruderSensor sensor = Sensor;
        if (sensor == null)
        {
            return;
        }
        Rect outer = new Rect(0f, 0f, WinSize.x, WinSize.y).ContractedBy(10f);

        float detectsHeight = Text.LineHeight * 2f + RowHeight * CompIntruderSensor.TargetOptions.Length + 8f;
        Listing_Standard top = new Listing_Standard();
        top.Begin(new Rect(outer.x, outer.y, outer.width, detectsHeight));
        Text.Font = GameFont.Medium;
        top.Label("GV_GasSensorTab".Translate());
        Text.Font = GameFont.Small;
        top.GapLine();
        for (int i = 0; i < CompIntruderSensor.TargetOptions.Length; i++)
        {
            SensorTargets flag = CompIntruderSensor.TargetOptions[i];
            bool on = (sensor.Targets & flag) != 0;
            bool prev = on;
            top.CheckboxLabeled(("GV_SensorTarget_" + flag).Translate(), ref on);
            if (on != prev)
            {
                sensor.SetTargets(sensor.Targets ^ flag);
            }
        }
        top.End();

        float headerHeight = Text.LineHeight * 2f + 8f;
        float afterTop = outer.yMax - (outer.y + detectsHeight);
        float halfSection = afterTop / 2f;

        float ventMidY = outer.y + detectsHeight;
        DrawSectionHeader(new Rect(outer.x, ventMidY, outer.width, headerHeight), "GV_GasSensorTriggers",
            sensor.HasExplicitLinks ? "GV_SensorInspectLinked".Translate(sensor.LinkedVents.Count).ToString() : "GV_SensorInspectRoomFallback".Translate().ToString());
        Rect ventScrollOuter = new Rect(outer.x, ventMidY + headerHeight, outer.width, halfSection - headerHeight);
        DrawVentList(sensor, ventScrollOuter);

        float doorMidY = ventMidY + halfSection;
        DrawSectionHeader(new Rect(outer.x, doorMidY, outer.width, headerHeight), "GV_GasSensorLocks",
            sensor.HasExplicitDoorLinks ? "GV_SensorInspectLinkedDoors".Translate(sensor.LinkedDoors.Count).ToString() : "GV_GasSensorNoDoorsLinked".Translate().ToString());
        Rect doorScrollOuter = new Rect(outer.x, doorMidY + headerHeight, outer.width, outer.yMax - (doorMidY + headerHeight));
        DrawDoorList(sensor, doorScrollOuter);
    }

    private static void DrawSectionHeader(Rect rect, string titleKey, string summary)
    {
        Listing_Standard mid = new Listing_Standard();
        mid.Begin(rect);
        mid.GapLine();
        mid.Label(titleKey.Translate());
        mid.Label(summary);
        mid.End();
    }

    private void DrawVentList(CompIntruderSensor sensor, Rect scrollOuter)
    {
        List<CompGasVentController> vents = AllVents(sensor.parent.Map);
        Rect viewRect = new Rect(0f, 0f, scrollOuter.width - 16f, Mathf.Max(vents.Count * RowHeight, scrollOuter.height));
        Widgets.BeginScrollView(scrollOuter, ref ventScroll, viewRect);
        Listing_Standard list = new Listing_Standard();
        list.Begin(viewRect);
        if (vents.Count == 0)
        {
            list.Label("GV_GasSensorNoVents".Translate());
        }
        for (int i = 0; i < vents.Count; i++)
        {
            CompGasVentController vent = vents[i];
            Rect rowRect = list.GetRect(RowHeight);
            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
                TargetHighlighter.Highlight(vent.parent, arrow: true, colonistBar: false, circleOverlay: true);
            }

            // The name jumps the camera to the vent (a look, not a toggle); only the checkbox itself links it.
            const float checkboxSize = 24f;
            Rect checkboxRect = new Rect(rowRect.xMax - checkboxSize, rowRect.y + (rowRect.height - checkboxSize) / 2f, checkboxSize, checkboxSize);
            Rect labelRect = new Rect(rowRect.x, rowRect.y, rowRect.width - checkboxSize - 4f, rowRect.height);
            TextAnchor prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            string label = $"{vent.parent.LabelCap} -- {("GV_VentMode_" + vent.Mode).Translate()}";
            Widgets.Label(labelRect, label);
            Text.Anchor = prevAnchor;
            if (Widgets.ButtonInvisible(labelRect))
            {
                CameraJumper.TryJumpAndSelect(vent.parent);
            }

            bool linked = sensor.IsLinkedTo(vent);
            bool prev = linked;
            Widgets.Checkbox(new Vector2(checkboxRect.x, checkboxRect.y), ref linked, checkboxSize);
            if (linked != prev)
            {
                sensor.ToggleLink(vent.parent);
                if (linked && vent.Mode != VentMode.Sensor)
                {
                    vent.SetMode(VentMode.Sensor);
                    Messages.Message("GV_SensorAutoSwitchedVentMode".Translate(vent.parent.LabelCap), vent.parent, MessageTypeDefOf.CautionInput);
                }
            }
        }
        list.End();
        Widgets.EndScrollView();
    }

    private void DrawDoorList(CompIntruderSensor sensor, Rect scrollOuter)
    {
        List<CompDoorLock> doors = AllDoors(sensor.parent.Map);
        Rect viewRect = new Rect(0f, 0f, scrollOuter.width - 16f, Mathf.Max(doors.Count * RowHeight, scrollOuter.height));
        Widgets.BeginScrollView(scrollOuter, ref doorScroll, viewRect);
        Listing_Standard list = new Listing_Standard();
        list.Begin(viewRect);
        if (doors.Count == 0)
        {
            list.Label("GV_GasSensorNoDoors".Translate());
        }
        for (int i = 0; i < doors.Count; i++)
        {
            CompDoorLock door = doors[i];
            Rect rowRect = list.GetRect(RowHeight);
            if (Mouse.IsOver(rowRect))
            {
                Widgets.DrawHighlight(rowRect);
                TargetHighlighter.Highlight(door.parent, arrow: true, colonistBar: false, circleOverlay: true);
            }

            const float checkboxSize = 24f;
            Rect checkboxRect = new Rect(rowRect.xMax - checkboxSize, rowRect.y + (rowRect.height - checkboxSize) / 2f, checkboxSize, checkboxSize);
            Rect labelRect = new Rect(rowRect.x, rowRect.y, rowRect.width - checkboxSize - 4f, rowRect.height);
            TextAnchor prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            string label = $"{door.parent.LabelCap} ({door.parent.Position.x}, {door.parent.Position.z})";
            Widgets.Label(labelRect, label);
            Text.Anchor = prevAnchor;
            if (Widgets.ButtonInvisible(labelRect))
            {
                CameraJumper.TryJumpAndSelect(door.parent);
            }

            bool linked = sensor.IsLinkedToDoor(door.parent);
            bool prev = linked;
            Widgets.Checkbox(new Vector2(checkboxRect.x, checkboxRect.y), ref linked, checkboxSize);
            if (linked != prev)
            {
                sensor.ToggleDoorLink(door.parent);
            }
        }
        list.End();
        Widgets.EndScrollView();
    }

    private static List<CompGasVentController> AllVents(Map map)
    {
        List<CompGasVentController> result = new List<CompGasVentController>();
        GasDeviceRegistry devices = VentGasGrid.For(map)?.Devices;
        if (devices == null)
        {
            return result;
        }
        for (int i = 0; i < devices.Controllers.Count; i++)
        {
            result.Add(devices.Controllers[i]);
        }
        return result;
    }

    private static List<CompDoorLock> AllDoors(Map map)
    {
        List<CompDoorLock> result = new List<CompDoorLock>();
        GasDeviceRegistry devices = VentGasGrid.For(map)?.Devices;
        if (devices == null)
        {
            return result;
        }
        for (int i = 0; i < devices.DoorLocks.Count; i++)
        {
            result.Add(devices.DoorLocks[i]);
        }
        return result;
    }
}
