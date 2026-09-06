using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.Profile;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

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

    internal static bool TryResolveGift(Mailbox mailbox, int index, out DialogPackage dialog)
    {
        var gift = mailbox.Gifts[index];
        dialog = null;
        string error = null;
        if (gift == null)
            error = "礼物条目为空";
        else if (!gift.itemId.HasValue)
            error = "缺少 itemId";
        else if (!DataBaseCore.Items.ContainsKey(gift.itemId.Value))
            error = $"Item 未注册: {gift.itemId.Value}";
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
