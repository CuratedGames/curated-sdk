namespace Curated.Unity
{
    /// <summary>What <see cref="CuratedUnity.OpenStoreOverlay"/> should do with the cart. Values match <see cref="Curated.Sdk.OverlayToStoreFlag"/>.</summary>
    public enum OverlayToStoreFlag : byte
    {
        /// <summary>Open the store page. No cart action. Allowed for a published app, including one that is not released.</summary>
        None = 0,

        /// <summary>Open the store page and add the app to the cart. Released or preorderable apps only.</summary>
        AddToCart = 1,

        /// <summary>Add the app to the cart and show the cart. Released or preorderable apps only.</summary>
        AddToCartAndShow = 2,
    }
}
