using Microsoft.Build.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using rail;
using ReLogic.Content;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Terraria.UI.Chat;
using Terraria.Utilities;
using XPT.Core.Audio.MP3Sharp.Decoding;
namespace RandomDistributionModifier
{

    public class RandomDistributionModifier : Mod
    {
        public const int DistributionCount = 4;
        public static DistributionSetter[] distributionSetters = new DistributionSetter[DistributionCount];

        public override void Load()
        {
            On_UnifiedRandom.Sample += AnotherRandom;
            On_UnifiedRandom.Next += AnotherRandomInt;
            On_UnifiedRandom.NextBytes += AnotherRandomBytes;
            On_UnifiedRandom.Next_int += AnotherRandomSigleInt;
            On_UnifiedRandom.Next_int_int += AnotherRandomIntInt;

            for (int n = 0; n < DistributionCount; n++)
                distributionSetters[n] = n switch
                {
                    0 => new PowerDistribution(),
                    1 => new LinearDistribution(),
                    2 => new SmoothDistribution(),
                    3 or _ => new NormalDistribution()
                };

            var path = $"{Main.SavePath}/Mods/RandomDistributionModifier/config.bin";
            if (File.Exists(path))
            {
                try
                {
                    using FileStream fileStream = new(path, FileMode.Open);
                    using BinaryReader reader = new(fileStream);
                    foreach (var setter in distributionSetters)
                        setter.Load(reader);
                }
                catch
                {
                    File.Delete(path);
                }
            }
            base.Load();
        }

        private int AnotherRandomSigleInt(On_UnifiedRandom.orig_Next_int orig, UnifiedRandom self, int maxValue)
        {
            var ui = RDMSystem.instance.randomDistributionModifierUI;
            if (!ui.useModification)
                return orig.Invoke(self, maxValue);
            if (maxValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxValue), $"maxValue must be positive.maxValue:{maxValue}");
            }
            return (int)self.NextFloat(maxValue);
        }

        private double AnotherRandom(On_UnifiedRandom.orig_Sample orig, UnifiedRandom self)
        {
            var ui = RDMSystem.instance.randomDistributionModifierUI;
            if (ui.useModification)
                return Math.Clamp(ui.distributionElement.Setter.ConvertFromUnified(() => orig.Invoke(self)), 0, 0.99999);
            return orig.Invoke(self);
        }

        private int AnotherRandomInt(On_UnifiedRandom.orig_Next orig, UnifiedRandom self)
        {
            var ui = RDMSystem.instance.randomDistributionModifierUI;
            if (ui.useModification)
            {
                return (int)(int.MaxValue * self.NextDouble());
            }
            return orig.Invoke(self);
        }


        private void AnotherRandomBytes(On_UnifiedRandom.orig_NextBytes orig, UnifiedRandom self, byte[] buffer)
        {
            var ui = RDMSystem.instance.randomDistributionModifierUI;
            if (!ui.useModification)
            {
                orig.Invoke(self, buffer);
                return;
            }
            ArgumentNullException.ThrowIfNull(buffer);

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)(self.Next() % 256);
            }
        }
        private int AnotherRandomIntInt(On_UnifiedRandom.orig_Next_int_int orig, UnifiedRandom self, int minValue, int maxValue)
        {
            var ui = RDMSystem.instance.randomDistributionModifierUI;
            if (!ui.useModification || true)
                return orig.Invoke(self, minValue, maxValue);
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException(nameof(minValue), $"minValue must be less than maxValue,min:{minValue},max:{maxValue}");
            long diff = maxValue - minValue;
            return (int)(diff * self.NextDouble()) + minValue;
        }
        //private double AnotherRandom(On_UnifiedRandom.orig_Sample orig, UnifiedRandom self)
        //{
        //    double t = orig.Invoke(self);
        //    if (!RandomModifierConfig.Instance.useModification) goto label;
        //    t = Math.Clamp(t, 0, 1);
        //    t = RandomModifierConfig.Instance.distribution.setter.ConvertFromAVG(t);
        //label:
        //    return t;
        //}

        public override void Unload()
        {
            On_UnifiedRandom.Sample -= AnotherRandom;
            On_UnifiedRandom.Next -= AnotherRandomInt;
            On_UnifiedRandom.NextBytes -= AnotherRandomBytes;
            On_UnifiedRandom.Next_int -= AnotherRandomSigleInt;
            On_UnifiedRandom.Next_int_int -= AnotherRandomIntInt;
            base.Unload();
        }
    }
    public class RDMSystem : ModSystem
    {
        public static string GetLocalization(string suffix) => Language.GetTextValue($"Mods.RandomDistributionModifier.Misc.{suffix}");

        public override void Load()
        {
            if (Main.netMode != NetmodeID.Server)
            {
                instance = this;
                randomDistributionModifierUI = new RandomDistributionModifierUI();
                userInterface = new UserInterface();
                randomDistributionModifierUI.Activate();
                userInterface.SetState(randomDistributionModifierUI);
                ShowScreenProjectorKeybind = KeybindLoader.RegisterKeybind(Mod, "ModifyRandom", "F");
            }
        }

        public static RDMSystem instance;
        public RandomDistributionModifierUI randomDistributionModifierUI;
        public UserInterface userInterface;
        public static ModKeybind ShowScreenProjectorKeybind { get; private set; }
        public override void UpdateUI(GameTime gameTime)
        {
            if (RandomDistributionModifierUI.Visible)
            {
                userInterface?.Update(gameTime);
            }
            base.UpdateUI(gameTime);
        }
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            //寻找一个名字为Vanilla: Mouse Text的绘制层，也就是绘制鼠标字体的那一层，并且返回那一层的索引
            int MouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            //寻找到索引时
            if (MouseTextIndex != -1)
            {
                //往绘制层集合插入一个成员，第一个参数是插入的地方的索引，第二个参数是绘制层
                layers.Insert(MouseTextIndex, new LegacyGameInterfaceLayer(
                   //这里是绘制层的名字
                   "RandomDistributionModifier:RandomDistributionModifierUI",
                   //这里是匿名方法
                   delegate
                   {
                       //当Visible开启时（当UI开启时）
                       if (RandomDistributionModifierUI.Visible)
                           //绘制UI（运行exampleUI的Draw方法）
                           randomDistributionModifierUI.Draw(Main.spriteBatch);
                       return true;
                   },
                   //这里是绘制层的类型
                   InterfaceScaleType.UI)
               );
            }
            base.ModifyInterfaceLayers(layers);
        }
        public override void SaveWorldData(TagCompound tag)
        {
            var path = $"{Main.SavePath}/Mods/RandomDistributionModifier/config.bin";
            try
            {
                var dirPath = Path.GetDirectoryName(path);
                if (!Directory.Exists(dirPath))
                    Directory.CreateDirectory(dirPath);
                using FileStream fileStream = new(path, FileMode.OpenOrCreate);
                using BinaryWriter writer = new(fileStream);
                foreach (var setter in RandomDistributionModifier.distributionSetters)
                    setter.Save(writer);
            }
            catch (Exception ex)
            {
                Main.NewText(ex.Message);
                //File.Delete(path);
            }
            base.SaveWorldData(tag);
        }
        public override void LoadWorldData(TagCompound tag)
        {
            base.LoadWorldData(tag);
        }
    }
    public class RDMPlayer : ModPlayer
    {
        RandomDistributionModifierUI modifierUI;
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (modifierUI == null) modifierUI = RDMSystem.instance.randomDistributionModifierUI;
            if (RDMSystem.ShowScreenProjectorKeybind.JustPressed)
            {
                if (RandomDistributionModifierUI.Visible)
                    modifierUI.Close();
                else
                    modifierUI.Open();
            }

            /*int l = 25;
            int m = 100;
            int[] array = new int[l];
            for (int n = 0; n < m; n++)
                array[Main.rand.Next(2, 2 + l) - 2]++;
            string msg = "";
            foreach (var num in array)
                msg += num + " ， ";
            Main.NewText(msg);*/

            //Main.NewText(Main.rand.Next());
            base.ProcessTriggers(triggersSet);
        }
    }
}
