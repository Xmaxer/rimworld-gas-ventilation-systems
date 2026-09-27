using UnityEngine;
using Verse;

namespace GasVentilation;

[StaticConstructorOnStartup]
public static class GasVentTextures
{
    public static readonly Texture2D VentOff = Load("UI/Commands/GV_VentMode_Off");
    public static readonly Texture2D VentOn = Load("UI/Commands/GV_VentMode_On");
    public static readonly Texture2D VentSensor = Load("UI/Commands/GV_VentMode_Sensor");
    public static readonly Texture2D GasSwatch = Load("UI/Commands/GV_GasSwatch");
    public static readonly Texture2D SensorArmed = Load("UI/Commands/GV_SensorArmed");
    public static readonly Texture2D SensorLinger = Load("UI/Commands/GV_SensorLinger");
    public static readonly Texture2D SensorStopWhenDowned = Load("UI/Commands/GV_SensorStopWhenDowned");
    public static readonly Texture2D Copy = Load("UI/Buttons/Copy");
    public static readonly Texture2D Paste = Load("UI/Buttons/Paste");

    public static readonly Texture2D DeconstructPipes =
        ContentFinder<Texture2D>.Get("UI/Designators/GV_DeconstructGasPipes", false)
        ?? ContentFinder<Texture2D>.Get("UI/Designators/Deconstruct");

    public static Texture2D ForMode(VentMode mode)
    {
        switch (mode)
        {
            case VentMode.On:
                return VentOn;
            case VentMode.Sensor:
                return VentSensor;
            default:
                return VentOff;
        }
    }

    private static Texture2D Load(string path)
    {
        return ContentFinder<Texture2D>.Get(path, false) ?? BaseContent.BadTex;
    }
}
