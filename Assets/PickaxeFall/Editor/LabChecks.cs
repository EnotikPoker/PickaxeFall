namespace PickaxeFall.Editor
{
    public static class LabChecks
    {
        [UnityEditor.MenuItem("PickaxeFall/Balance Lab/Validate")]
        public static void Validate() => BalanceLab.LabValidation.Validate();
    }
}
