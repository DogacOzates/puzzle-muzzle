using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_PURCHASING
using UnityEngine.Purchasing;
#endif

public class NoAdsIapBridge
#if UNITY_PURCHASING
    : IStoreListener
#endif
{
    private readonly string noAdsProductId;
    private readonly Action onNoAdsPurchased;
    private readonly Action<string> onNoAdsPriceUpdated;

    // consumable hint packs: productId → (hintAmount, priceCallback)
    private readonly Dictionary<string, (int amount, Action<int> onGranted, Action<string> onPrice)> hintPacks
        = new Dictionary<string, (int, Action<int>, Action<string>)>();

#if UNITY_PURCHASING
    private IStoreController storeController;
    private IExtensionProvider extensionProvider;
#endif

    public NoAdsIapBridge(string noAdsProductId, Action onNoAdsPurchased, Action<string> onNoAdsPriceUpdated)
    {
        this.noAdsProductId     = noAdsProductId;
        this.onNoAdsPurchased    = onNoAdsPurchased;
        this.onNoAdsPriceUpdated = onNoAdsPriceUpdated;
    }

    /// <summary>Register a consumable hint-pack product before calling Initialize().</summary>
    public void AddHintPack(string productId, int hintAmount, Action<int> onGranted, Action<string> onPriceUpdated)
    {
        hintPacks[productId] = (hintAmount, onGranted, onPriceUpdated);
    }

    public void Initialize()
    {
#if UNITY_PURCHASING
        LastInitError = null;
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        builder.AddProduct(noAdsProductId, ProductType.NonConsumable);
        foreach (var kvp in hintPacks)
            builder.AddProduct(kvp.Key, ProductType.Consumable);
        UnityPurchasing.Initialize(this, builder);
#else
        onNoAdsPriceUpdated?.Invoke("$9.99");
        foreach (var kvp in hintPacks)
            kvp.Value.onPrice?.Invoke(DefaultHintPackFallback(kvp.Value.amount));
        Debug.Log("Unity IAP bridge is inactive. Install the In-App Purchasing package to enable purchases.");
#endif
    }

    private static string DefaultHintPackFallback(int amount)
    {
        if (amount <= 5)  return "$0.99";
        if (amount <= 20) return "$2.99";
        return "$5.99";
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

    public string LastInitError { get; private set; } = "UNITY_PURCHASING not defined";

    // Purchase the No Ads non-consumable
    public void Purchase()
    {
#if UNITY_PURCHASING
        InitiatePurchase(noAdsProductId);
#else
        Debug.LogWarning("No Ads purchase requested, but Unity IAP is not installed.");
#endif
    }

    // Purchase a hint pack consumable
    public void PurchaseHintPack(string productId)
    {
#if UNITY_PURCHASING
        if (!hintPacks.ContainsKey(productId))
        {
            Debug.LogWarning("Unknown hint pack product: " + productId);
            return;
        }
        InitiatePurchase(productId);
#else
        if (hintPacks.TryGetValue(productId, out var pack))
            pack.onGranted?.Invoke(pack.amount);
        else
            Debug.LogWarning("Unknown hint pack product: " + productId);
#endif
    }

#if UNITY_PURCHASING
    private void InitiatePurchase(string productId)
    {
        if (storeController == null)
        {
            Debug.LogWarning("Store is not initialized yet.");
            return;
        }
        var product = storeController.products.WithID(productId);
        if (product == null || !product.availableToPurchase)
        {
            Debug.LogWarning("Product not available: " + productId);
            return;
        }
        storeController.InitiatePurchase(product);
    }
#endif

    public void RestorePurchases(Action<bool, string> onComplete)
    {
#if UNITY_PURCHASING
        if (extensionProvider == null)
        {
            onComplete?.Invoke(false, "Store not initialized");
            return;
        }
        var apple = extensionProvider.GetExtension<IAppleExtensions>();
        apple.RestoreTransactions((result, error) => onComplete?.Invoke(result, error));
#else
        onComplete?.Invoke(false, "IAP not available");
#endif
    }

#if UNITY_PURCHASING
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        storeController = controller;
        extensionProvider = extensions;

        // NoAds
        var noAdsProduct = storeController.products.WithID(noAdsProductId);
        if (noAdsProduct != null)
        {
            if (noAdsProduct.metadata != null && !string.IsNullOrEmpty(noAdsProduct.metadata.localizedPriceString))
                onNoAdsPriceUpdated?.Invoke(noAdsProduct.metadata.localizedPriceString);
            if (noAdsProduct.hasReceipt)
                onNoAdsPurchased?.Invoke();
        }

        // Hint packs — update prices from store
        foreach (var kvp in hintPacks)
        {
            var p = storeController.products.WithID(kvp.Key);
            if (p?.metadata != null && !string.IsNullOrEmpty(p.metadata.localizedPriceString))
                kvp.Value.onPrice?.Invoke(p.metadata.localizedPriceString);
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        LastInitError = error.ToString();
        Debug.LogWarning("Unity IAP initialization failed: " + error);
        onNoAdsPriceUpdated?.Invoke("$9.99");
        foreach (var kvp in hintPacks)
            kvp.Value.onPrice?.Invoke(DefaultHintPackFallback(kvp.Value.amount));
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        LastInitError = error + ": " + message;
        Debug.LogWarning("Unity IAP initialization failed: " + error + " - " + message);
        onNoAdsPriceUpdated?.Invoke("$9.99");
        foreach (var kvp in hintPacks)
            kvp.Value.onPrice?.Invoke(DefaultHintPackFallback(kvp.Value.amount));
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        string id = args.purchasedProduct?.definition?.id;
        if (id == null) return PurchaseProcessingResult.Complete;

        if (id == noAdsProductId)
        {
            onNoAdsPurchased?.Invoke();
        }
        else if (hintPacks.TryGetValue(id, out var pack))
        {
            pack.onGranted?.Invoke(pack.amount);
        }

        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogWarning("Purchase failed: " + product.definition.id + " - " + failureReason);
    }
#endif
}
