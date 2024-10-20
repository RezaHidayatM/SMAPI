using Microsoft.Xna.Framework;
using StardewModdingAPI.Framework.ModLoading.Framework;
using StardewValley.Objects;

namespace StardewModdingAPI.Framework.ModLoading.Rewriters.StardewValley_1_6
{
    /// <summary>Maps Stardew Valley 1.5.6's <see cref="StorageFurniture"/> methods to their newer form to avoid breaking older mods.</summary>
    /// <remarks>This is public to support SMAPI rewriting and should never be referenced directly by mods. See remarks on <see cref="ReplaceReferencesRewriter"/> for more info.</remarks>
    public class StorageFurnitureFacade : StorageFurniture, IRewriteFacade
    {
        /*********
        ** Public methods
        *********/
        public static StorageFurniture Constructor(int which, Vector2 tile, int initialRotations)
        {
            return new StorageFurniture(which.ToString(), tile, initialRotations);
        }

        public static StorageFurniture Constructor(int which, Vector2 tile)
        {
            return new StorageFurniture(which.ToString(), tile);
        }


        /*********
        ** Private methods
        *********/
        private StorageFurnitureFacade()
        {
            RewriteHelper.ThrowFakeConstructorCalled();
        }
    }
}
