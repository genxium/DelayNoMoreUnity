using System.Collections.Immutable;
using UnityEngine;
using shared;

public class TeamRibbon : MonoBehaviour {

    /*
     Why not alter texture file? Because tex file is associated with anim -- that would be a lot of work for the "cache by species npc anim node impl" too, the potential solution is "2D animation package/Sprite Resolver" but it might pre-load all possible textures which are memory consuming. 
     */
    public static ImmutableDictionary<uint, ImmutableDictionary<int, PaletteSwapSpec>> COLOR_SWAP_RULE = ImmutableDictionary.Create<uint, ImmutableDictionary<int, PaletteSwapSpec>>()
        .Add(Battle.SPECIES_BLADEGIRL, ImmutableDictionary.Create<int, PaletteSwapSpec>().Add(2, new PaletteSwapSpec {
            P1From = new Color(0x00 / 255f, 0x95 / 255f, 0xE9 / 255f),
            P1FromRange = 0f,
            P1To = new Color(0x69/255f, 0x9C/255f, 0x7E/255f),
            P1ToFuzziness = 0.05f
        }))
        .Add(Battle.SPECIES_WITCHGIRL, ImmutableDictionary.Create<int, PaletteSwapSpec>().Add(2, new PaletteSwapSpec {
            P1From = new Color(0x54 / 255f, 0x57 / 255f, 0x83 / 255f),
            P1FromRange = 0.36f,
            P1To = new Color(0x23 / 255f, 0x66 / 255f, 0x66 / 255f),
            P1ToFuzziness = 0.05f
        }))
        .Add(Battle.SPECIES_MAGSWORDGIRL, ImmutableDictionary.Create<int, PaletteSwapSpec>().Add(2, new PaletteSwapSpec {
            P1From = new Color(0xAB / 255f, 0xD5 / 255f, 0xFB / 255f),
            P1FromRange = 0.29f,
            P1To = new Color(0x9F / 255f, 0x3E / 255f, 0x2C / 255f),
            P1ToFuzziness = 0.05f
        }))
        .Add(Battle.SPECIES_BRIGHTWITCH, ImmutableDictionary.Create<int, PaletteSwapSpec>().Add(2, new PaletteSwapSpec {
            P1From = new Color(0x5D / 255f, 0x59 / 255f, 0x76 / 255f),
            P1FromRange = 0.20f,
            P1To = new Color(0xC1 / 255f, 0x92 / 255f, 0xF8 / 255f),
            P1ToFuzziness = 0.05f
        }))
        .Add(Battle.SPECIES_BOUNTYHUNTER, ImmutableDictionary.Create<int, PaletteSwapSpec>().Add(2, new PaletteSwapSpec {
            P1From = new Color(0x49 / 255f, 0x97 / 255f, 0x57 / 255f),
            P1FromRange = 0.15f,
            P1To = new Color(0xD7 / 255f, 0x80 / 255f, 0xC9 / 255f),
            P1ToFuzziness = 0.2f
        }))
        .Add(Battle.SPECIES_LIGHTGUARD_RED, ImmutableDictionary.Create<int, PaletteSwapSpec>().Add(2, new PaletteSwapSpec {
            P1From = new Color(0x91 / 255f, 0x83 / 255f, 0x83 / 255f),
            P1FromRange = 0f,
            P1To = new Color(0x7A / 255f, 0x99 / 255f, 0xFB / 255f),
            P1ToFuzziness = 0.1f
        }).Add(1, new PaletteSwapSpec {
            P1From = new Color(0x91 / 255f, 0x83 / 255f, 0x83 / 255f),
            P1FromRange = 0f,
            P1To = new Color(0xFA / 255f, 0x7B / 255f, 0xC6 / 255f),
            P1ToFuzziness = 0.1f
        }))
        .Add(Battle.SPECIES_HEAVYGUARD_RED, ImmutableDictionary.Create<int, PaletteSwapSpec>().Add(2, new PaletteSwapSpec {
            P1From = new Color(0x91  / 255f, 0x87 / 255f, 0x90 / 255f),
            P1FromRange = 0f,
            P1To = new Color(0x95 / 255f, 0xAB / 255f, 0xDD / 255f),
            P1ToFuzziness = 0.1f
        }).Add(1, new PaletteSwapSpec {
            P1From = new Color(0x91  / 255f, 0x87 / 255f, 0x90 / 255f),
            P1FromRange = 0f,
            P1To = new Color(0xDD / 255f, 0x95 / 255f, 0xB0 / 255f),
            P1ToFuzziness = 0.1f
        }))
        .Add(Battle.SPECIES_RIDERGUARD_RED, ImmutableDictionary.Create<int, PaletteSwapSpec>().Add(2, new PaletteSwapSpec {
            P1From = new Color(0xD2 / 255f, 0xBE / 255f, 0xBE / 255f),
            P1FromRange = 0.3f,
            P1To = new Color(0x95 / 255f, 0xAB / 255f, 0xDD / 255f),
            P1ToFuzziness = 0.05f
        }).Add(1, new PaletteSwapSpec {
            P1From = new Color(0xD2 / 255f, 0xBE / 255f, 0xBE / 255f),
            P1FromRange = 0f,
            P1To = new Color(0xDD / 255f, 0x95 / 255f, 0xB0 / 255f),
            P1ToFuzziness = 0.1f
        }))
        ;

    public int score;

    public static Color team1Color = new Color(0xDD / 255f, 0x95 / 255f, 0xB0 / 255f);
    public static Color team2Color = new Color(0x95 / 255f, 0xAB / 255f, 0xDD / 255f);
    public static Color team3Color = new Color(0xFF / 255f, 0xA5 / 255f, 0x00 / 255f);
    public static Color team4Color = Color.cyan;

    public void setBulletTeamId(int bulletTeamId) {
        var renderer = gameObject.GetComponent<SpriteRenderer>();
        switch (bulletTeamId) {
            case 1:
                renderer.color = team1Color;
                break;
            case 2:
                renderer.color = team2Color;
                break;
            case 3:
                renderer.color = team3Color;
                break;
            case 4:
                renderer.color = team4Color;
                break;
            default:
                break;
        }
    }
}
