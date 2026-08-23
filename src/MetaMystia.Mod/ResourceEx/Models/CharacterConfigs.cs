using System.Collections.Generic;

namespace MetaMystia.ResourceEx.Models;

public class CharacterConfig
{
    public int id { get; set; }
    public string name { get; set; }
    public string label { get; set; }
    public List<string> descriptions { get; set; }
    public string type { get; set; }
    public List<PortraitConfig> portraits { get; set; }
    public int? faceInNoteBook { get; set; }
    public GuestConfig guest { get; set; }
    public CharacterSpriteSetCompactConfig characterSpriteSetCompact { get; set; }
    public KizunaEventConfig kizuna { get; set; }
    public SpawnMarkerConfig spawnMarker { get; set; }
    public bool hideInAlbum { get; set; }
    public bool isParticular { get; set; }
    public bool isCollabCharacter { get; set; }
}

public class SpawnMarkerConfig
{
    public string mapLabel { get; set; } = "BeastForest";
    public float x { get; set; } = 0f;
    public float y { get; set; } = 0f;
    public DayScene.Input.DayScenePlayerInputGenerator.CharacterRotation rotation { get; set; } = DayScene.Input.DayScenePlayerInputGenerator.CharacterRotation.Down;
}

public class KizunaEventConfig
{
    public string lv1UpgradePrerequisiteEvent { get; set; }
    public string lv2UpgradePrerequisiteEvent { get; set; }
    public string lv3UpgradePrerequisiteEvent { get; set; }
    public string lv4UpgradePrerequisiteEvent { get; set; }

    public List<string> lv1Welcome { get; set; }
    public List<string> lv2Welcome { get; set; }
    public List<string> lv3Welcome { get; set; }
    public List<string> lv4Welcome { get; set; }
    public List<string> lv5Welcome { get; set; }

    public List<string> lv1ChatData { get; set; }
    public List<string> lv2ChatData { get; set; }
    public List<string> lv3ChatData { get; set; }
    public List<string> lv4ChatData { get; set; }
    public List<string> lv5ChatData { get; set; }

    public List<string> lv2InviteSucceed { get; set; }
    public List<string> lv2InviteFailed { get; set; }
    public List<string> lv3InviteSucceed { get; set; }
    public List<string> lv3InviteFailed { get; set; }
    public List<string> lv4InviteSucceed { get; set; }
    public List<string> lv4InviteFailed { get; set; }
    public List<string> lv5InviteSucceed { get; set; }

    public List<string> lv3RequestIngerdient { get; set; } // ignore typo
    public List<string> lv4RequestIngerdient { get; set; } // ignore typo
    public List<string> lv5RequestIngerdient { get; set; } // ignore typo
    public List<string> lv4RequestBeverage { get; set; }
    public List<string> lv5RequestBeverage { get; set; }
    public List<string> lv5Commision { get; set; }
    public List<string> lv5CommisionFinish { get; set; }
    public string commisionAreaLabel { get; set; } // ignore typo
}

public class GuestConfig
{
    public int fundRangeLower { get; set; }
    public int fundRangeUpper { get; set; }
    public List<string> evaluation { get; set; }
    public List<string> conversation { get; set; }
    public List<RequestConfig> foodRequests { get; set; }
    public List<RequestConfig> bevRequests { get; set; }
    public List<int> hateFoodTag { get; set; }
    public List<WeightedTagConfig> likeFoodTag { get; set; }
    public List<WeightedTagConfig> likeBevTag { get; set; }
    public List<SpawnConfig> spawn { get; set; }
}

public class SpawnConfig
{
    public int izakayaId { get; set; }
    public float relativeProb { get; set; }
    public bool onlySpawnAfterUnlocking { get; set; }
    public bool onlySpawnWhenPlaceBeRecorded { get; set; }
}

public class RequestConfig
{
    public int tagId { get; set; }
    public string request { get; set; }
    public bool enable { get; set; } = true;
}

public class WeightedTagConfig
{
    public int tagId { get; set; }
    public int weight { get; set; }
}

public class CharacterSpriteSetCompactConfig
{
    public string name { get; set; }
    public List<string> mainSprite { get; set; }
    public List<string> eyeSprite { get; set; }
}

public class PortraitConfig
{
    public int pid { get; set; }
    public string path { get; set; }
}
