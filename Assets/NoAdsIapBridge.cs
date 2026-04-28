using System;
using UnityEngine;

#if UNITY_PURCHASING
using UnityEngine.Purchasing;
#endif

public class NoAdsIapBridge
#if UNITY_PURCHASING
    : IDetailedStoreListener
#endif
{
    private readonly string productId;
    private readonly Action onPurchaseSucceeded;
    private readonly Action<string> onPriceUpdated;

#if UNITY_PURCHASING
    private IStoreController storeController;
#endif

    public NoAdsIapBridge(string productId, Action onPurchaseSucceeded, Action<string> onPriceUpdated)
    {
        this.productId = productId;
        this.onPurchaseSucceeded = onPurchaseSucceeded;
        this.onPriceUpdated = onPriceUpdated;
    }

    public void Initialize()
    {
#if UNITY_PURCHASING
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        builder.AddProduct(productId, ProductType.NonConsumable);
        UnityPurchasing.Initialize(this, builder);
#else
        onPriceUpdated?.Invoke("$4.99");
        Debug.Log("Unity IAP bridge is inactive. Install the In-App Purchasing package to enable the No Ads purchase.");
#endif
    }

    public bool IsStoreReady
    {
        get
        {
#if UNITY_PURCHASING
            return storeController != null;
#else
            return false;
#endif
        }
    }

    public void Purchase()
    {
#if UNITY_PURCHASING
        if (storeController == null)
        {
            Debug.LogWarning("Store is not initialized yet.");
            return;
        }

        Product product = storeController.products.WithID(productId);
        if (product == null || !product.availableToPurchase)
        {
            Debug.LogWarning("No Ads product is not available to purchase.");
            return;
        }

        storeController.InitiatePurchase(product);
#else
        Debug.LogWarning("No Ads purchase requested, but Unity IAP is not installed.");
#endif
    }

#if UNITY_PURCHASING
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        storeController = controller;

        Product product = storeController.products.WithID(productId);
        if (product != null)
        {
            if (product.metadata != null && !string.IsNullOrEmpty(product.metadata.localizedPriceString))
                onPriceUpdated?.Invoke(product.metadata.localizedPriceString);

            if (product.hasReceipt)
                onPurchaseSucceeded?.Invoke();
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.LogWarning("Unity IAP initialization failed: " + error);
        onPriceUpdated?.Invoke("$4.99");
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogWarning("Unity IAP initialization failed: " + error + " - " + message);
        onPriceUpdated?.Invoke("$4.99");
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        if (args.purchasedProduct != null && args.purchasedProduct.definition.id == productId)
            onPurchaseSucceeded?.Invoke();

        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogWarning("Purchase failed: " + product.definition.id + " - " + failureReason);
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        Debug.LogWarning("Purchase failed: " + product.definition.id + " - " + failureDescription.message);
    }
#endif
}
