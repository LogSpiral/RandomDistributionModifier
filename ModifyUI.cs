using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.UI;
using static RandomDistributionModifier.RDMSystem;
namespace RandomDistributionModifier;

public class RandomDistributionModifierUI : UIState
{

    public DraggablePanel basePanel;
    public bool useModification;
    public UITextPanel<string> state;
    public UITextPanel<string> randType;
    public int index;
    public DistributionElement distributionElement;
    public static bool Visible;
    public override void OnInitialize()
    {
        useModification = false;
        basePanel = new DraggablePanel();
        basePanel.Left.Set(600, 0);
        basePanel.Top.Set(400, 0);
        basePanel.Width.Set(800, 0);
        basePanel.Height.Set(500, 0);

        Append(basePanel);
        state = new UITextPanel<string>(GetLocalization("Off"));
        state.HAlign = 1;
        state.VAlign = 0;
        state.OnLeftClick += (evt, elem) =>
        {
            useModification = !useModification;
            SoundEngine.PlaySound(SoundID.MenuTick);
            state.SetText(useModification ? GetLocalization("On") : GetLocalization("Off"));
        };
        basePanel.Append(state);
        randType = new UITextPanel<string>(GetLocalization("Power"));
        randType.HAlign = 1;
        randType.Top.Set(50, 0);
        randType.OnLeftClick += (evt, elem) =>
        {
            index++;
            index %= 4;
            randType.SetText(GetLocalization(index switch
            {
                0 => "Power",
                1 => "Linear",
                2 => "Smooth",
                3 or _ => "Normal",
            }
            ));
            SoundEngine.PlaySound(SoundID.MenuTick);
            distributionElement.Setter = RandomDistributionModifier.distributionSetters[index];
        };
        basePanel.Append(randType);
        distributionElement = new DistributionElement();
        distributionElement.Width.Set(-20, 1);
        distributionElement.Height.Set(-100, 1);
        distributionElement.VAlign = 1;
        distributionElement.HAlign = .5f;

        basePanel.Append(distributionElement);
        base.OnInitialize();
    }
    public void Open()
    {
        SoundEngine.PlaySound(SoundID.MenuOpen);
        state.SetText(useModification ? GetLocalization("On") : GetLocalization("Off"));
        randType.SetText(GetLocalization(index switch
        {
            0 => "Power",
            1 => "Linear",
            2 => "Smooth",
            3 or _ => "Normal",
        }
        ));
        Recalculate();
        Visible = true;
    }
    public void Close()
    {
        SoundEngine.PlaySound(SoundID.MenuClose);
        Visible = false;
    }
}
