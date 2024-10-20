using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6
{
    /// <summary>Maps Stardew Valley 1.5.6's <see cref="TemporaryAnimatedSprite"/> methods to their newer form to avoid breaking older mods.</summary>
    /// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
    public class TemporaryAnimatedSpriteFacade : TemporaryAnimatedSprite, IRewriteFacade
    {
        /*********
        ** Accessors
        *********/
        public new float id
        {
            get => base.id;
            set => base.id = (int)value;
        }


        /*********
        ** Private methods
        *********/
        private TemporaryAnimatedSpriteFacade()
        {
            RewriteHelper.ThrowFakeConstructorCalled();
        }
    }
}
