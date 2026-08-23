using System.Collections.Generic;

using Common.DialogUtility;

namespace MetaMystia.ResourceEx.Models;

public class DialogConfig
{
    public int characterId { get; set; }
    public SpeakerIdentity.Identity characterType { get; set; }
    public int pid { get; set; }
    public Position position { get; set; }
    public DialogActionConfig[] actions { get; set; }
    public string text { get; set; }
}

public class DialogPackageConfig
{
    public string name { get; set; }
    public List<DialogConfig> dialogList { get; set; }

    public int Count => dialogList?.Count ?? 0;

    public DialogConfig this[int index] => dialogList[index];
}

public class DialogBranchOptionConfig
{
    public string text { get; set; }
    /// <summary>
    /// One-based dialog number; Count + 1 means finish this dialog package.
    /// </summary>
    public int jump { get; set; }
    public int? price { get; set; }
}

public class DialogActionConfig
{
    public ActionType actionType { get; set; }

    /// <summary>
    /// For CG/BG actions: relative path to sprite image (e.g. "assets/CG/painting.png").
    /// Prefer a full rex URI in ResourceEx JSON config.
    /// </summary>
    public string sprite { get; set; }

    /// <summary>
    /// For Sound actions: relative path or rex URI to a WAV asset.
    /// </summary>
    public string sound { get; set; }

    /// <summary>
    /// For Branch actions: option text, target dialog index, and optional price.
    /// Jump values are one-based dialog numbers; dialogList.Count + 1 means finish this dialog package.
    /// </summary>
    public List<DialogBranchOptionConfig> options { get; set; }

    /// <summary>
    /// For Goto actions: one-based dialog number; dialogList.Count + 1 means finish this dialog package.
    /// </summary>
    public int? index { get; set; }

    /// <summary>
    /// For End actions: optional native dialog exit code. Normal dialog menus can leave this as 0.
    /// </summary>
    public int? exitCode { get; set; }

    public bool shouldSet { get; set; } = true;
}
