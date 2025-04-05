using Humanizer;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria;
using System.Xml.Linq;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent;
using ReLogic.Graphics;

namespace RandomDistributionModifier;
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
        Setter = new PowerDistribution();
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
        if (setter is PowerDistribution)
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
        else if (setter is NormalDistribution)
        {
            max = 1.5f;
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

            var h1 = MathHelper.Max(0, values[i] / max);
            var h2 = MathHelper.Max(0, values[i + 1] / max);

            var vecStart = new Vector2(diemsion.X + diemsion.Width * ts, diemsion.Y + diemsion.Height * (1 - h1 * .5f));
            var vecEnd = new Vector2(diemsion.X + diemsion.Width * te, diemsion.Y + diemsion.Height * (1 - h2 * .5f));

            DrawLine(spriteBatch, vecStart, vecEnd, Color.Red * MathHelper.Clamp(Utils.GetLerpValue(2.5f, 2f, (h1 + h2) * .5f, true), 0, 1), 1);
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
        Vector2 vector = diemsion.Position();
        switch (setter)
        {
            case PowerDistribution power:
                {
                    string maxS = RDMSystem.GetLocalization("max");
                    string minS = RDMSystem.GetLocalization("min");
                    if (PowerDistribution.isRightSide)
                        spriteBatch.DrawString(FontAssets.MouseText.Value, $"X0 ~ U({minS},{maxS})\nX1 ~ U(X0,{maxS})\n.\n.\n.\nX(n) ~ U(X(n-1),{maxS})\n n = {PowerDistribution.Power - 1}", vector + new Vector2(500, 0), Color.White);
                    else
                        spriteBatch.DrawString(FontAssets.MouseText.Value, $"X0 ~ U({minS},{maxS})\nX1 ~ U({minS},X0)\n.\n.\n.\nX(n) ~ U({maxS},X(n-1))\n n = {PowerDistribution.Power - 1}", vector + new Vector2(500, 0), Color.White);


                    return;
                }
            case SmoothDistribution or LinearDistribution:
                {
                    return;
                }
            case NormalDistribution:
                {
                    string miu = RDMSystem.GetLocalization("Miu");
                    string sigma = RDMSystem.GetLocalization("Sigma");
                    string maxS = RDMSystem.GetLocalization("max");
                    string minS = RDMSystem.GetLocalization("min");

                    spriteBatch.DrawString(FontAssets.MouseText.Value, $"{miu} = u = ({maxS}+ {minS}) / 2\n{sigma} = s = ({maxS} - {minS}) / 6", vector + new Vector2(-360 + diemsion.Width, 0), Color.White);

                    spriteBatch.DrawString(FontAssets.MouseText.Value, $"{minS} \n  = u - 3s", vector + new Vector2(0, diemsion.Height - 64), Color.White);
                    spriteBatch.DrawString(FontAssets.MouseText.Value, $"{maxS} \n  = u + 3s", vector + new Vector2(diemsion.Width - 80, diemsion.Height - 64), Color.White);
                    return;
                }
        }
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
        ButtonHandle handle = new();
        handle.Top = this.Top;
        handle.Left.Set(this.Left.Pixels + (end ? -32 : 32), 0);

        handle.Width.Set(24, 0);
        handle.Height.Set(24, 0);
        this.handle = handle;
        return handle;
    }
    public ButtonHandle HandleOn(float k, bool end = false)
    {
        ButtonHandle handle = new();
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
            Vector2 vec = new(handle.Left.Pixels - Left.Pixels, handle.Top.Pixels - Top.Pixels);
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
