using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley.Tools;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6
{
    /// <summary>Maps Stardew Valley 1.5.6's <see cref="WateringCan"/> methods to their newer form to avoid breaking older mods.</summary>
    /// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
    public class WateringCanFacade : WateringCan, IRewriteFacade
    {
        /*********
        ** Accessors
        *********/
        public int waterLeft
        {
            get => base.WaterLeft;
            set => base.WaterLeft = value;
        }


        /*********
        ** Private methods
        *********/
        private WateringCanFacade()
        {
            RewriteHelper.ThrowFakeConstructorCalled();
        }
    }
}
