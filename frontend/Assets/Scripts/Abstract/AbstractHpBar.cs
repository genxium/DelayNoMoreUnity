using UnityEngine;

public class AbstractHpBar : MonoBehaviour {
    protected Vector3 newScaleHolder = new Vector3();
    protected Vector2 newSizeHolder = new Vector2();

    protected static int HP_PER_SECTION = 100;
    protected static float HP_PER_SECTION_F = (float)HP_PER_SECTION;
    protected float DEFAULT_HOLDER_PADDING = 3.0f;

    protected static float DEFAULT_HP100_WIDTH = 32.0f;
    protected static float DEFAULT_HP100_HEIGHT = 5.0f;

    protected static Color[] hpColors = new Color[] {
        new Color(0x77 / 255f, 0xE9 / 255f, 0x35 / 255f),
        new Color(0x25 / 255f, 0x56 / 255f, 0x26 / 255f),
        new Color(0x4f / 255f, 0x57 / 255f, 0x18 / 255f),
        new Color(0x93 / 255f, 0x90 / 255f, 0x25 / 255f),
        new Color(0x8f / 255f, 0xC4 / 255f, 0x62 / 255f),
        new Color(0xf0 / 255f, 0xA6 / 255f, 0x08 / 255f),
        new Color(0x6E / 255f, 0xC2 / 255f, 0xBD / 255f),
    };
}
