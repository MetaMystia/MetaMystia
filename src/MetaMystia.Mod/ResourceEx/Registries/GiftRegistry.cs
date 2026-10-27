using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.Core.Collections.DaySceneUtility.Collections;
using GameData.Profile;
using GameData.RunTime.Common;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

using static GameData.Core.Collections.DaySceneUtility.Collections.Product;

namespace MetaMystia.ResourceEx.Registries;

[AutoLog]
public static partial class GiftRegistry
{
    private static readonly List<Mailbox> _mailboxes = new();
    internal static IReadOnlyList<Mailbox> Mailboxes => _mailboxes;

    internal sealed class Mailbox
    {
        public LoadedResourcePackage Package { get; }
        public IReadOnlyList<GiftConfig> Gifts => Package.Config.gifts;
        public bool[] Available { get; }

        public Mailbox(LoadedResourcePackage package)
        {
            Package = package;
            Available = new bool[Gifts.Count];
        }
    }

    internal static void Merge(LoadedResourcePackage package)
    {
        if (package.Config?.gifts?.Count > 0)
            _mailboxes.Add(new Mailbox(package));
    }

    internal static void ValidateAllGifts()
    {
        foreach (var mailbox in _mailboxes)
            for (int i = 0; i < mailbox.Gifts.Count; i++)
                TryResolveGift(mailbox, i, out _);
    }

    internal static Product GetProduct(GiftConfig gift) => new()
    {
        productType = gift.productType ?? ProductType.Item,
        productId = gift.productId ?? gift.itemId.Value,
        productAmount = gift.productAmount ?? 1,
    };

    private static bool IsRegistered(Product product) => product.productType switch
    {
        ProductType.Item => DataBaseCore.Items.ContainsKey(product.productId),
        ProductType.Recipe => DataBaseCore.Recipes.ContainsKey(product.productId),
        ProductType.Food => DataBaseCore.Foods.ContainsKey(product.productId),
        ProductType.Beverage => DataBaseCore.Beverages.ContainsKey(product.productId),
        ProductType.Ingredient => DataBaseCore.Ingredients.ContainsKey(product.productId),
        _ => false,
    };

    internal static bool TryResolveGift(Mailbox mailbox, int index, out DialogPackage dialog)
    {
        var gift = mailbox.Gifts[index];
        dialog = null;
        string error = null;
        if (gift == null)
            error = "礼物条目为空";
        else if (!(gift.productId ?? gift.itemId).HasValue)
            error = "缺少 productId";
        else if (gift.productAmount < 1 || gift.productAmount > RunTimeStorage.MAX_OBJECT_COUNT)
            error = $"productAmount 必须在 1 到 {RunTimeStorage.MAX_OBJECT_COUNT} 之间: {gift.productAmount}";
        else if (gift.productType is not (null or ProductType.Item or ProductType.Recipe or ProductType.Food or ProductType.Beverage or ProductType.Ingredient))
            error = $"不支持的 productType: {gift.productType}";
        else if (!IsRegistered(GetProduct(gift)))
            error = $"{gift.productType ?? ProductType.Item} 未注册: {gift.productId ?? gift.itemId}";
        else if (string.IsNullOrWhiteSpace(gift.title))
            error = "缺少 title";
        else if (string.IsNullOrWhiteSpace(gift.dialogPackageName))
            error = "缺少 dialogPackageName";
        else if (DialogRegistry.GetDialogPackage(gift.dialogPackageName)?.Count > 0)
        {
            dialog = DialogRegistry.GetBuiltDialogPackage(gift.dialogPackageName);
            if (dialog == null || dialog.dialogMeta == null || dialog.dialogMeta.Length == 0)
                error = $"对话包未构建或为空: {gift.dialogPackageName}";
        }
        else
            error = $"对话包不存在或为空: {gift.dialogPackageName}";

        mailbox.Available[index] = error == null;
        if (error != null)
            Log.Warning($"[{mailbox.Package.PackageName}] gifts[{index}] ({gift?.title}): {error}");
        return error == null;
    }
}
