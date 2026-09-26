namespace GasVentilation.Core;

/// <summary>Notified when the density band of any channel in a cell changes (including appear/disappear).</summary>
public interface IGasFieldListener
{
    void OnBandsChanged(int cellIndex, byte oldBands, byte newBands);
}
