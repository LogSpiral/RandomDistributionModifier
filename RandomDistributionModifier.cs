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
        public static DistributionSetter[] distributionSetters = new DistributionSetter[3];

        public override void Load()
        {
            On_UnifiedRandom.Sample += AnotherRandom;
            On_UnifiedRandom.Next += AnotherRandomInt;
            On_UnifiedRandom.NextBytes += AnotherRandomBytes;
            On_UnifiedRandom.Next_int += AnotherRandomSigleInt;
            On_UnifiedRandom.Next_int_int += AnotherRandomIntInt;

            for (int n = 0; n < 3; n++)
                distributionSetters[n] = n switch
                {
                    0 => new PowerDistrubution(),
                    1 => new LinearDistribution(),
                    2 or _ => new SmoothDistribution()
                };

            var path = $"{Main.SavePath}/Mods/RandomDistributionModifier/config.bin";
            if (File.Exists(path))
            {
                try
                {
                    using FileStream fileStream = new FileStream(path, FileMode.Open);
                    using BinaryReader reader = new BinaryReader(fileStream);
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
                throw new ArgumentOutOfRangeException("maxValue", $"maxValue must be positive.maxValue:{maxValue}");
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
            if (buffer == null)
                throw new ArgumentNullException("buffer");

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
                throw new ArgumentOutOfRangeException("minValue", $"minValue must be less than maxValue,min:{minValue},max:{maxValue}");
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
                using FileStream fileStream = new FileStream(path, FileMode.OpenOrCreate);
                using BinaryWriter writer = new BinaryWriter(fileStream);
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
    public class DraggablePanel : UIPanel
    {
        public bool Dragging;
        public Vector2 Offset;
        public override void LeftMouseDown(UIMouseEvent evt)
        {
            if (evt.Target == this)
            {
                Dragging = true;
                Offset = new Vector2(evt.MousePosition.X - Left.Pixels, evt.MousePosition.Y - Top.Pixels);
            }

            base.LeftMouseDown(evt);

        }
        public override void LeftMouseUp(UIMouseEvent evt)
        {
            Dragging = false;

            base.LeftMouseUp(evt);
        }
        public override void Update(GameTime gameTime)
        {
            if (Dragging)
            {
                Left.Set(Main.mouseX - Offset.X, 0f);
                Top.Set(Main.mouseY - Offset.Y, 0f);
                Recalculate();
            }
            base.Update(gameTime);
        }
    }
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
            state = new UITextPanel<string>("关");
            state.HAlign = 1;
            state.VAlign = 0;
            state.OnLeftClick += (evt, elem) =>
            {
                useModification = !useModification;
                SoundEngine.PlaySound(SoundID.MenuTick);
                state.SetText(useModification ? "开" : "关");
            };
            basePanel.Append(state);
            randType = new UITextPanel<string>("迭代");
            randType.HAlign = 1;
            randType.Top.Set(50, 0);
            randType.OnLeftClick += (evt, elem) =>
            {
                index++;
                index %= 3;
                randType.SetText(index switch
                {
                    0 => "迭代",
                    1 => "折线",
                    2 or _ => "平滑"
                }
                );
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
            Visible = true;
        }
        public void Close()
        {
            SoundEngine.PlaySound(SoundID.MenuClose);
            Visible = false;
        }
    }
    public class DistributionElement : UIPanel
    {
        DistributionSetter setter;
        public DistributionSetter Setter { get => setter; set { setter = value; RefreshElements(); } }
        void RefreshElements()
        {
            Elements.Clear();
            setter.AppendElements(this);
        }
        public override void OnInitialize()
        {
            Setter = new PowerDistrubution();
            PaddingBottom = PaddingLeft = PaddingRight = PaddingTop = 0;

            //RefreshElements();
            base.OnInitialize();
        }
        public static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float width = 4f, bool offset = false, Vector2 drawOffset = default)
        {
            if (offset)
            {
                end += start;
            }

            spriteBatch.Draw(TextureAssets.MagicPixel.Value, (start + end) * .5f + drawOffset, new Rectangle(0, 0, 1, 1), color, (end - start).ToRotation(), new Vector2(.5f, .5f), new Vector2((start - end).Length(), width), 0, 0);
        }
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);
            if (setter == null) return;
            int mCount = 300;
            float[] values = new float[mCount + 1];
            float max = 1f;
            for (int i = 0; i <= mCount; i++)
            {
                values[i] = setter.PDF(i * 1f / mCount);
            }
            if (setter is PowerDistrubution)
            {
                max = 0f;
                for (int i = 0; i <= mCount; i++)
                {
                    var k = values[i];
                    if (k > max)
                        max = k;
                }
                if (max > 20f) max = 20f;
            }

            var diemsion = GetDimensions();
            //for (int i = 0; i <= mCount; i++)
            //{
            //    var t = i * 1f / mCount;
            //    spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(diemsion.X + diemsion.Width * t, diemsion.Y + diemsion.Height * (1 - values[i] / max)), new Rectangle(0, 0, 1, 1), Color.White, 0, new Vector2(.5f), 1f, 0, 0);
            //}
            for (int i = 0; i < mCount; i++)
            {
                var ts = i * 1f / mCount;
                var te = (i + 1f) / mCount;

                var vecStart = new Vector2(diemsion.X + diemsion.Width * ts, diemsion.Y + diemsion.Height * (1 - MathHelper.Clamp(values[i] / max, 0, 2) * .5f));
                var vecEnd = new Vector2(diemsion.X + diemsion.Width * te, diemsion.Y + diemsion.Height * (1 - MathHelper.Clamp(values[i + 1] / max, 0, 2) * .5f));

                DrawLine(spriteBatch, vecStart, vecEnd, Color.Red, 1);
                //spriteBatch.Draw(TextureAssets.MagicPixel.Value, vecStart, new Rectangle(0, 0, 1, 1), Color.White, 0, new Vector2(.5f), 1f, 0, 0);
                //if (i == mCount - 1)
                //    spriteBatch.Draw(TextureAssets.MagicPixel.Value, vecEnd, new Rectangle(0, 0, 1, 1), Color.White, 0, new Vector2(.5f), 1f, 0, 0);

            }


            int[] rand = new int[mCount];
            for (int n = 0; n < 1200; n++)
            {
                //float index = Main.rand.NextFloat(mCount);
                //rand[(int)index]++;
                rand[Main.rand.Next(mCount)]++;
            }
            for (int n = 0; n < mCount; n++)
            {
                DrawLine(spriteBatch, diemsion.ToRectangle().BottomRight() + new Vector2(64 + n, 0), new Vector2(0, -rand[n] * 4), Main.DiscoColor, 1, true);
            }
        }
    }
    public abstract class DistributionSetter
    {
        /// <summary>
        /// 概率密度函数表达式，仅用作绘制，从0到1
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public abstract float PDF(float t);
        /// <summary>
        /// 通过在0到1上均匀分布的值生成一个满足指定分布函数的新值
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public abstract double ConvertFromUnified(Func<double> t);
        /// <summary>
        /// 添加用于魔改概率密度函数的UI控件
        /// </summary>
        /// <param name="basePanel"></param>
        public abstract void AppendElements(UIElement basePanel);

        public abstract void Save(BinaryWriter writer);

        public abstract void Load(BinaryReader reader);
    }
    public class SmoothDistribution : DistributionSetter
    {
        public static List<Vector3> nodes = new();
        public static List<DraggableButton> buttons = new();
        //public List<DraggableButton> tangents = new();
        public override float PDF(float t)
        {
            int count = nodes.Count;
            int index = 1;
            while (index < count && t > nodes[index].X)
            {
                index++;
            }
            if (index >= count)
            {
                Main.NewText("下标异常！");
                return 1;
            }
            Vector3 pts = nodes[index - 1];
            Vector3 ptd = nodes[index];

            return MathHelper.Hermite(pts.Y, pts.Z * (ptd.X - pts.X), ptd.Y, ptd.Z * (ptd.X - pts.X), Utils.GetLerpValue(nodes[index - 1].X, nodes[index].X, t));
            //return MathHelper.Lerp(nodes[index - 1].Y, nodes[index].Y, Utils.GetLerpValue(nodes[index - 1].X, nodes[index].X, t));
        }
        public override double ConvertFromUnified(Func<double> t)
        {
            int max = 100;
            float[] totalArea = new float[max];

            for (int n = 0; n < max; n++)
            {
                totalArea[n] = MathHelper.Clamp(PDF(n * 1f / (max - 1)), 0, 2);
                if (n > 0)
                    totalArea[n] += totalArea[n - 1];
            }
            float sum = totalArea[^1];
            for (int n = 0; n < max; n++)
                totalArea[n] /= sum;
            double randValue = t();
            int counter = 0;
            while (counter < max && randValue > totalArea[counter])
                counter++;

            return (counter + Utils.GetLerpValue(counter == 0 ? 0 : totalArea[counter - 1], totalArea[counter], randValue)) / max;
        }
        public override void AppendElements(UIElement basePanel)
        {
            bool dataLoaded = nodes.Count > 0;
            buttons.Clear();

            UIElement element = new UIElement();
            element.Width.Set(0, 1);
            element.Height.Set(0, 1);
            element.MarginBottom = element.MarginLeft = element.MarginRight = element.MarginTop = 0;

            basePanel.Append(element);

            DraggableButton startButton = new DraggableButton();
            startButton.StartOrEnd = true;
            var dimension = element.GetDimensions();
            startButton.Top.Set(-16, 0);
            startButton.Left.Set(-16, 0);
            if (dataLoaded)
            {
                startButton.Top.Set(dimension.Height * (2 - nodes[0].Y) * .5f - 16, 0);
            }
            startButton.Width.Set(32, 0);
            startButton.Height.Set(32, 0);
            float scaler = MathF.Pow(dimension.Height / dimension.Width, 2);
            if (dataLoaded)
                element.Append(startButton.HandleOn(nodes[0].Z * scaler));
            else
                element.Append(startButton.HandleOn());
            element.Append(startButton);
            DraggableButton endButton = new DraggableButton();
            endButton.StartOrEnd = true;
            endButton.Top.Set(dimension.Height - 16, 0);
            endButton.Left.Set(dimension.Width - 16, 0);
            if (dataLoaded)
            {
                endButton.Top.Set(dimension.Height * (2 - nodes[^1].Y) * .5f - 16, 0);
            }
            endButton.Width.Set(32, 0);
            endButton.Height.Set(32, 0);
            if (dataLoaded)
                element.Append(endButton.HandleOn(nodes[^1].Z * scaler, true));
            else
                element.Append(endButton.HandleOn(true));

            element.Append(endButton);
            buttons.Add(startButton);
            buttons.Add(endButton);

            if (dataLoaded)
            {
                for (int n = 1; n < nodes.Count - 1; n++)
                {
                    DraggableButton draggableButton = new DraggableButton();
                    var dim = element.GetDimensions();
                    draggableButton.Left.Set(dimension.Width * nodes[n].X - 16, 0);
                    draggableButton.Top.Set(dimension.Height * (2 - nodes[n].Y) * .5f - 16, 0);
                    draggableButton.Width.Set(32, 0);
                    draggableButton.Height.Set(32, 0);
                    buttons.Add(draggableButton);
                    element.Append(draggableButton.HandleOn(nodes[n].Z * scaler));
                    element.Append(draggableButton);
                    draggableButton.OnRightClick += (evt, elem) =>
                    {
                        if (evt.Target == draggableButton)
                        {
                            buttons.Remove(draggableButton);
                            draggableButton.Remove();
                            draggableButton.handle.Remove();
                        }
                    };
                }
            }

            element.OnLeftMouseDown += (evt, elem) =>
            {
                if (evt.Target != elem) return;
                DraggableButton draggableButton = new DraggableButton();
                var dim = element.GetDimensions();
                draggableButton.Left.Set(Main.mouseX - dim.X - 16, 0);
                draggableButton.Top.Set(Main.mouseY - dim.Y - 16, 0);
                draggableButton.Width.Set(32, 0);
                draggableButton.Height.Set(32, 0);
                element.Append(draggableButton.HandleOn());
                buttons.Add(draggableButton);
                element.Append(draggableButton);
                draggableButton.OnRightClick += (evt, elem) =>
                {
                    if (evt.Target == draggableButton)
                    {
                        buttons.Remove(draggableButton);
                        draggableButton.Remove();
                        draggableButton.handle.Remove();
                    }
                };
            };
            element.OnUpdate += elem =>
            {
                nodes.Clear();
                for (int n = 0; n < buttons.Count; n++)
                {

                    var btn = buttons[n];
                    var resultvec = new Vector2((btn.Left.Pixels + 16) / dimension.Width, 2 * (1 - (btn.Top.Pixels + 16) / dimension.Height));
                    var tarvec = new Vector2((btn.handle.Left.Pixels + 16) / dimension.Width, 2 * (1 - (btn.handle.Top.Pixels + 16) / dimension.Height));
                    var result = new Vector3(resultvec, (resultvec.Y - tarvec.Y) / (resultvec.X - tarvec.X));

                    if (n > 1)
                    {
                        if (result.X == 0)
                            result.X += 0.001f;
                        if (result.X == 1)
                            result.X -= 0.001f;
                    }
                    nodes.Add(result);
                }
                nodes.Sort((vec1, vec2) => vec1.X > vec2.X ? 1 : -1);
            };
        }

        public override void Save(BinaryWriter writer)
        {
            writer.Write((byte)nodes.Count);
            for (int n = 0; n < nodes.Count; n++)
            {
                Vector3 vec = nodes[n];
                writer.Write(vec.X);
                writer.Write(vec.Y);
                writer.Write(vec.Z);
            }

        }
        public override void Load(BinaryReader reader)
        {
            int count = reader.ReadByte();
            for (int n = 0; n < count; n++)
            {
                nodes.Add(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
            }
        }
    }

    public class LinearDistribution : DistributionSetter
    {
        public static List<Vector2> nodes = new();
        public static List<DraggableButton> buttons = new();
        public override float PDF(float t)
        {
            int count = nodes.Count;
            int index = 1;
            while (index < count && t > nodes[index].X)
            {
                index++;
            }
            if (index >= count)
            {
                Main.NewText("下标异常！");
                return 1;
            }
            return MathHelper.Lerp(nodes[index - 1].Y, nodes[index].Y, Utils.GetLerpValue(nodes[index - 1].X, nodes[index].X, t));
        }
        public override double ConvertFromUnified(Func<double> t)
        {
            double randomValue = t();
            float[] areas = new float[nodes.Count - 1];
            for (int i = 0; i < areas.Length; i++)
            {
                areas[i] = (nodes[i].Y + nodes[i + 1].Y) * (nodes[i + 1].X - nodes[i].X) * .5f;
                if (i != 0)
                    areas[i] += areas[i - 1];
            }
            float totalArea = areas[^1];
            for (int i = 0; i < areas.Length; i++)
                areas[i] /= totalArea;

            int index = 0;
            while (randomValue > areas[index])
                index++;
            if (index > 0)
                randomValue -= areas[index - 1];
            float k = (nodes[index + 1].Y - nodes[index].Y) / (nodes[index + 1].X - nodes[index].X) / totalArea;
            float y = nodes[index].Y / totalArea;
            if (k != 0)
                return (-y + Math.Sqrt(y * y + 2 * k * randomValue)) / k + nodes[index].X;
            else
                return randomValue / y + nodes[index].X;

            //return t();
        }
        public override void AppendElements(UIElement basePanel)
        {
            bool dataLoaded = nodes.Count > 0;
            buttons.Clear();

            UIElement element = new UIElement();
            element.Width.Set(0, 1);
            element.Height.Set(0, 1);
            basePanel.Append(element);

            DraggableButton startButton = new DraggableButton();
            startButton.StartOrEnd = true;
            var dimension = element.GetDimensions();
            startButton.Top.Set(-16, 0);
            startButton.Left.Set(-16, 0);
            if (dataLoaded)
            {
                startButton.Top.Set(dimension.Height * (2 - nodes[0].Y) * .5f - 16, 0);
            }
            startButton.Width.Set(32, 0);
            startButton.Height.Set(32, 0);
            element.Append(startButton);
            DraggableButton endButton = new DraggableButton();
            endButton.StartOrEnd = true;
            endButton.Top.Set(dimension.Height - 16, 0);
            if (dataLoaded)
            {
                endButton.Top.Set(dimension.Height * (2 - nodes[^1].Y) * .5f - 16, 0);
            }
            endButton.Left.Set(dimension.Width - 16, 0);
            endButton.Width.Set(32, 0);
            endButton.Height.Set(32, 0);
            element.Append(endButton);
            buttons.Add(startButton);
            buttons.Add(endButton);
            if (dataLoaded)
            {
                for (int n = 1; n < nodes.Count - 1; n++)
                {
                    DraggableButton draggableButton = new DraggableButton();
                    var dim = element.GetDimensions();
                    draggableButton.Left.Set(dimension.Width * nodes[n].X - 16, 0);
                    draggableButton.Top.Set(dimension.Height * (2 - nodes[n].Y) * .5f - 16, 0);
                    draggableButton.Width.Set(32, 0);
                    draggableButton.Height.Set(32, 0);
                    buttons.Add(draggableButton);
                    element.Append(draggableButton);
                    draggableButton.OnRightClick += (evt, elem) =>
                    {
                        if (evt.Target == draggableButton)
                        {
                            buttons.Remove(draggableButton);
                            draggableButton.Remove();
                        }
                    };
                }
            }
            element.OnLeftMouseDown += (evt, elem) =>
            {
                if (evt.Target != elem) return;
                DraggableButton draggableButton = new DraggableButton();
                var dim = element.GetDimensions();
                //Main.NewText((element.GetDimensions().ToRectangle(), basePanel.GetDimensions().ToRectangle(), basePanel.PaddingTop));
                draggableButton.Left.Set(Main.mouseX - dim.X - 16, 0);
                draggableButton.Top.Set(Main.mouseY - dim.Y - 16, 0);
                draggableButton.Width.Set(32, 0);
                draggableButton.Height.Set(32, 0);
                buttons.Add(draggableButton);
                element.Append(draggableButton);
                draggableButton.OnRightClick += (evt, elem) =>
                {
                    if (evt.Target == draggableButton)
                    {
                        buttons.Remove(draggableButton);
                        draggableButton.Remove();
                    }
                };
            };
            element.OnUpdate += elem =>
            {
                nodes.Clear();
                for (int n = 0; n < buttons.Count; n++)
                {

                    var btn = buttons[n];
                    var result = new Vector2((btn.Left.Pixels + 16) / dimension.Width, 2 * (1 - (btn.Top.Pixels + 16) / dimension.Height));
                    if (n > 1)
                    {
                        if (result.X == 0)
                            result.X += 0.001f;
                        if (result.X == 1)
                            result.X -= 0.001f;
                    }
                    if (n == 0 && result.Y == 0)
                        result.Y += 0.001f;
                    nodes.Add(result);
                }
                nodes.Sort((vec1, vec2) => vec1.X > vec2.X ? 1 : -1);
            };
        }

        public override void Save(BinaryWriter writer)
        {
            writer.Write((byte)nodes.Count);
            for (int n = 0; n < nodes.Count; n++)
            {
                Vector2 vec = nodes[n];
                writer.Write(vec.X);
                writer.Write(vec.Y);
            }

        }
        public override void Load(BinaryReader reader)
        {
            int count = reader.ReadByte();
            for (int n = 0; n < count; n++)
            {
                nodes.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
            }
        }
    }
    public class PowerDistrubution : DistributionSetter//这不是指数分布，但是我姑且这么叫吧（x
    {
        static int power;
        public static int Power
        {
            get => power;
            set => power = Math.Clamp(value, 1, 10);
        }
        public static bool isRightSide;
        public override float PDF(float t)
        {
            if (power == 1) return 1;

            if (isRightSide)
                t = 1 - t;
            t = -MathF.Log(t);
            t = MathF.Pow(t, power - 1);
            int f = 1;
            for (int n = 1; n < power; n++)
            {
                f *= n;
            }
            return t / f;
        }
        public override double ConvertFromUnified(Func<double> t)
        {
            double result = 1;
            for (int n = 0; n < power; n++)
                result *= t();
            if (isRightSide) result = 1 - result;
            return result;
        }
        public override void AppendElements(UIElement basePanel)
        {
            UIToggleImage uIToggleImage = new UIToggleImage(ModContent.Request<Texture2D>("Terraria/Images/UI/TexturePackButtons", AssetRequestMode.ImmediateLoad), 32, 32, new Point(32, 32), new Point(0, 32));
            uIToggleImage.HAlign = 1f;
            uIToggleImage.VAlign = 0f;
            uIToggleImage.SetState(isRightSide);
            uIToggleImage.OnUpdate += _ =>
            {
                isRightSide = uIToggleImage.IsOn;
            };
            basePanel.Append(uIToggleImage);

            UIImageButton uIImageButton = new UIImageButton(ModContent.Request<Texture2D>("RandomDistributionModifier/ButtonUpDown", AssetRequestMode.ImmediateLoad));
            uIImageButton.OnLeftClick += (evt, elem) =>
            {
                Rectangle r = elem.GetDimensions().ToRectangle();

                if (evt.MousePosition.Y < r.Y + r.Height / 2)
                {
                    Power++;
                }
                else
                {
                    Power--;
                }
                SoundEngine.PlaySound(SoundID.MenuTick);
            };
            uIImageButton.HAlign = 1f;
            uIImageButton.Top.Set(30, 0);
            basePanel.Append(uIImageButton);
        }

        public override void Load(BinaryReader reader)
        {
            power = reader.ReadByte();
            isRightSide = reader.ReadBoolean();
        }
        public override void Save(BinaryWriter writer)
        {
            writer.Write((byte)power);
            writer.Write(isRightSide);
        }
    }

    public class DraggableButton : UIElement
    {
        public bool StartOrEnd;
        public bool Dragging;
        public Vector2 Offset;
        public ButtonHandle handle;
        public class ButtonHandle : UIElement
        {
            public bool Dragging;
            public bool UpdateNeeded;
            public Vector2 Offset;
            public override void LeftMouseDown(UIMouseEvent evt)
            {
                if (evt.Target == this)
                {
                    Dragging = true;
                    UpdateNeeded = false;
                    Offset = new Vector2(evt.MousePosition.X - Left.Pixels, evt.MousePosition.Y - Top.Pixels);
                }

                base.LeftMouseDown(evt);

            }
            public override void LeftMouseUp(UIMouseEvent evt)
            {
                Dragging = false;
                UpdateNeeded = true;
                base.LeftMouseUp(evt);
            }
            public override void Update(GameTime gameTime)
            {
                if (Dragging)
                {
                    float x = Main.mouseX - Offset.X;
                    float y = Main.mouseY - Offset.Y;
                    Left.Set(x, 0f);
                    Top.Set(y, 0f);
                    Recalculate();
                }
                else
                {
                    //if (Parent != null && UpdateNeeded)
                    //{
                    //    Vector2 vec = new Vector2(Left.Pixels, Top.Pixels);
                    //    float length = vec.Length();
                    //    vec *= MathHelper.Lerp(1, 32 / length, 0.05f);
                    //    Left.Set(vec.X, 0);
                    //    Top.Set(vec.Y, 0);
                    //    Recalculate();
                    //    if (Math.Abs(length - 32) < 0.1f)
                    //        UpdateNeeded = false;
                    //}
                }
                base.Update(gameTime);
            }
            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                var d = GetDimensions();
                var tex = ModContent.Request<Texture2D>("RandomDistributionModifier/PointButton").Value;
                spriteBatch.Draw(tex, d.Center(), null, Color.White, 0, tex.Size() * .5f, .75f, 0, 0);
                //if (Parent != null)
                //{
                //}
                base.DrawSelf(spriteBatch);
            }
        }
        public ButtonHandle HandleOn(bool end = false)
        {
            ButtonHandle handle = new ButtonHandle();
            handle.Top = this.Top;
            handle.Left.Set(this.Left.Pixels + (end ? -32 : 32), 0);

            handle.Width.Set(24, 0);
            handle.Height.Set(24, 0);
            this.handle = handle;
            return handle;
        }
        public ButtonHandle HandleOn(float k, bool end = false)
        {
            ButtonHandle handle = new ButtonHandle();
            float c = 1 / MathF.Sqrt(1 + k * k);
            float s = -k / MathF.Sqrt(1 + k * k);
            if (end)
            {
                c *= -1;
                s *= -1;
            }
            handle.Top.Set(this.Top.Pixels + 32 * s, 0);
            handle.Left.Set(this.Left.Pixels + 32 * c, 0);

            handle.Width.Set(24, 0);
            handle.Height.Set(24, 0);
            this.handle = handle;
            return handle;
        }
        public override void LeftMouseDown(UIMouseEvent evt)
        {
            if (evt.Target == this)
            {
                Dragging = true;
                Offset = new Vector2(evt.MousePosition.X - Left.Pixels, evt.MousePosition.Y - Top.Pixels);

            }

            base.LeftMouseDown(evt);

        }
        public override void LeftMouseUp(UIMouseEvent evt)
        {
            Dragging = false;
            if (handle != null)
                handle.UpdateNeeded = true;
            base.LeftMouseUp(evt);
        }
        public override void Update(GameTime gameTime)
        {
            if (Dragging)
            {
                float x = Main.mouseX - Offset.X;
                float y = Main.mouseY - Offset.Y;
                if (Parent != null)
                {
                    var dimension = Parent.GetDimensions();
                    var sd = GetDimensions();

                    x = MathHelper.Clamp(x, -sd.Width * .5f, dimension.Width - sd.Width * .5f);
                    y = MathHelper.Clamp(y, -sd.Height * .5f, dimension.Height - sd.Height * .5f);
                }
                if (!StartOrEnd)
                    Left.Set(x, 0f);
                Top.Set(y, 0f);
                Recalculate();
            }
            if (handle != null && handle.UpdateNeeded)
            {
                Vector2 vec = new Vector2(handle.Left.Pixels - Left.Pixels, handle.Top.Pixels - Top.Pixels);
                float length = vec.Length();
                vec *= MathHelper.Lerp(1, 32 / length, 0.1f);
                handle.Left.Set(vec.X + Left.Pixels, 0);
                handle.Top.Set(vec.Y + Top.Pixels, 0);
                handle.Recalculate();
                if (Math.Abs(length - 32) < 0.1f)
                    handle.UpdateNeeded = false;
            }
            base.Update(gameTime);
        }
        public override void OnInitialize()
        {
            base.OnInitialize();
        }
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            var d = GetDimensions().ToRectangle();
            var tex = ModContent.Request<Texture2D>("RandomDistributionModifier/PointButton").Value;
            spriteBatch.Draw(tex, d.Center() - tex.Size() * .5f, Color.White);
            if (handle != null)
            {
                DistributionElement.DrawLine(spriteBatch, d.Center(), handle.GetDimensions().Center(), Color.White);

            }
            //spriteBatch.Draw(TextureAssets.MagicPixel.Value, d.Center(), new Rectangle(0, 0, 1, 1), Color.Cyan, 0, new Vector2(.5f), 4, 0, 0);
            base.DrawSelf(spriteBatch);
        }
    }
}
