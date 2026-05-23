public interface IComboRule
{
    void Evaluate(
        BinSnapshot snapshot,
        FlushResolution resolution);
}