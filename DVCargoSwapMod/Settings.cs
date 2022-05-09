using UnityModManagerNet;

namespace DVCargoSwapMod
{
    public class Settings : UnityModManager.ModSettings, IDrawable 
    {

        [Draw(Label = "Load Textures on Demand (requires restart)", Type = DrawType.Toggle)]
        public bool loadOnDemand;

        [Draw(Label = "Enable Logging", Type = DrawType.Toggle)]
        public bool verbose;

        public override void Save(UnityModManager.ModEntry entry)
        {
            Save(this, entry);
        }

        public void OnChange()
        { }

    }
}
