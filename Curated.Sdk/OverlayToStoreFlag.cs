namespace Curated.Sdk;

/// <summary>What <see cref="Friends.OpenStoreOverlay"/> should do with the cart.</summary>
public enum OverlayToStoreFlag : byte
{
    /// <summary>Open the store page. No cart action. Allowed for a published app, including one that is not released.</summary>
    None = 0,

    /// <summary>Open the store page and add the app to the cart. Released or preorderable apps only.</summary>
    AddToCart = 1,

    /// <summary>Add the app to the cart and show the cart. Released or preorderable apps only.</summary>
    AddToCartAndShow = 2,
}
