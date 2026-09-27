using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace GasVentilation;

/// <summary>
/// Replaces the old FloatMenu target-type picker, and is the only UI for the new explicit sensor-to-vent
/// linking (see CompIntruderSensor.ToggleLink): a checklist of every vent on the map that can run in Sensor
/// mode, so one sensor can drive an arbitrary subset instead of only "everything in this room". With no vents
/// checked, the sensor falls back to the original room-based behaviour.
/// </summary>
public sealed class ITab_GasSensor : ITab
{
    private static readonly Vector2 WinSize = new Vector2(320f, 480f);
    private const float RowHeight = 26f;

    private Vector2 ventScroll;

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

        float midY = outer.y + detectsHeight;
        float midHeight = Text.LineHeight * 2f + 8f;
        Listing_Standard mid = new Listing_Standard();
        mid.Begin(new Rect(outer.x, midY, outer.width, midHeight));
        mid.GapLine();
        mid.Label("GV_GasSensorTriggers".Translate());
        mid.Label(sensor.HasExplicitLinks
            ? "GV_SensorInspectLinked".Translate(sensor.LinkedVents.Count)
            : "GV_SensorInspectRoomFallback".Translate());
        mid.End();

        Rect scrollOuter = new Rect(outer.x, midY + midHeight, outer.width, outer.yMax - (midY + midHeight));
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
            bool linked = sensor.IsLinkedTo(vent);
            bool prev = linked;
            string label = $"{vent.parent.LabelCap} ({vent.parent.Position.x}, {vent.parent.Position.z}) -- {("GV_VentMode_" + vent.Mode).Translate()}";
            Widgets.CheckboxLabeled(rowRect, label, ref linked);
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
}
