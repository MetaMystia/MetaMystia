using System.Collections.Generic;

namespace MetaMystia.ResourceEx.Models;

public class ClothConfig
{
    public int id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public string spritePath { get; set; }
    public string portraitPath { get; set; }
    public CharacterSpriteSetFullConfig pixelFullConfig { get; set; }
    public int izakayaSkinIndex { get; set; } = -1;
    public float izkayaHorizontalOffset { get; set; } = 0f;
    public float notebookHorizontalOffset { get; set; } = 0f;
    public float notebookVerticalOffset { get; set; } = 0f;
    public float notebookUITitleHorizontalOffset { get; set; } = 0f;
    public float notebookUITitleVerticalOffset { get; set; } = 0f;
}

public class CharacterSpriteSetFullConfig
{
    public string name { get; set; }
    public List<string> mainSprite { get; set; }
    public List<string> eyeSprite { get; set; }
    public List<string> hairSprite { get; set; }
    public List<string> backSprite { get; set; }
}
