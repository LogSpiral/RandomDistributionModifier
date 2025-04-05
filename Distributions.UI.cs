using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace RandomDistributionModifier;

partial class LinearDistribution
{
    public override void AppendElements(UIElement basePanel)
    {
        bool dataLoaded = nodes.Count > 0;
        buttons.Clear();

        UIElement element = new();
        element.Width.Set(0, 1);
        element.Height.Set(0, 1);
        basePanel.Append(element);

        DraggableButton startButton = new();
        startButton.StartOrEnd = true;
        var dimension = element.GetDimensions();
        calculatedDimension = dimension;
        startButton.Top.Set(-16, 0);
        startButton.Left.Set(-16, 0);
        if (dataLoaded)
        {
            startButton.Top.Set(dimension.Height * (2 - nodes[0].Y) * .5f - 16, 0);
        }
        startButton.Width.Set(32, 0);
        startButton.Height.Set(32, 0);
        element.Append(startButton);
        DraggableButton endButton = new();
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
                DraggableButton draggableButton = new();
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
            DraggableButton draggableButton = new();
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
            UpdateNodeData();
        };
        UpdateNodeData();
    }
    static CalculatedStyle calculatedDimension;
    public static void UpdateNodeData()
    {
        nodes.Clear();
        for (int n = 0; n < buttons.Count; n++)
        {

            var btn = buttons[n];
            var result = new Vector2((btn.Left.Pixels + 16) / calculatedDimension.Width, 2 * (1 - (btn.Top.Pixels + 16) / calculatedDimension.Height));
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

        areas = new float[nodes.Count - 1];// 区域累计概率
        for (int i = 0; i < areas.Length; i++)
        {
            areas[i] = (nodes[i].Y + nodes[i + 1].Y) * (nodes[i + 1].X - nodes[i].X) * .5f;
            if (i != 0)
                areas[i] += areas[i - 1];
        }
        float totalArea = areas[^1];// 目前完整的面积
        for (int i = 0; i < areas.Length; i++)
            areas[i] /= totalArea;// 归一化
        TotalArea = totalArea;
    }
}

partial class PowerDistribution
{
    public override void AppendElements(UIElement basePanel)
    {
        UIToggleImage uIToggleImage = new(ModContent.Request<Texture2D>("Terraria/Images/UI/TexturePackButtons", AssetRequestMode.ImmediateLoad), 32, 32, new Point(32, 32), new Point(0, 32));
        uIToggleImage.HAlign = 1f;
        uIToggleImage.VAlign = 0f;
        uIToggleImage.SetState(isRightSide);
        uIToggleImage.OnUpdate += _ =>
        {
            isRightSide = uIToggleImage.IsOn;
        };
        basePanel.Append(uIToggleImage);

        UIImageButton uIImageButton = new(ModContent.Request<Texture2D>("RandomDistributionModifier/ButtonUpDown", AssetRequestMode.ImmediateLoad));
        uIImageButton.OnLeftClick += (evt, elem) =>
        {
            Rectangle r = elem.GetDimensions().ToRectangle();

            if (evt.MousePosition.Y < r.Y + r.Height / 2)
                Power++;
            else
                Power--;

            SoundEngine.PlaySound(SoundID.MenuTick);
        };
        uIImageButton.HAlign = 1f;
        uIImageButton.Top.Set(30, 0);
        basePanel.Append(uIImageButton);
    }

}

partial class SmoothDistribution
{
    public override void AppendElements(UIElement basePanel)
    {
        bool dataLoaded = nodes.Count > 0;
        buttons.Clear();

        UIElement element = new();
        element.Width.Set(0, 1);
        element.Height.Set(0, 1);
        element.MarginBottom = element.MarginLeft = element.MarginRight = element.MarginTop = 0;

        basePanel.Append(element);

        DraggableButton startButton = new();
        startButton.StartOrEnd = true;
        var dimension = element.GetDimensions();
        calculatedDimension = dimension;
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
        DraggableButton endButton = new();
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
                DraggableButton draggableButton = new();
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
            DraggableButton draggableButton = new();
            var dim = element.GetDimensions();
            draggableButton.Left.Set(Main.mouseX - dim.X - 16, 0);
            draggableButton.Top.Set(Main.mouseY - dim.Y - 16, 0);
            draggableButton.Width.Set(32, 0);
            draggableButton.Height.Set(32, 0);
            var handle = draggableButton.HandleOn();
            element.Append(handle);
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
        element.OnUpdate += delegate
        {
            UpdateNodeData();
        };
        UpdateNodeData();
    }

    static CalculatedStyle calculatedDimension;
    public static void UpdateNodeData()
    {
        #region 处理顶点数据
        nodes.Clear();
        for (int n = 0; n < buttons.Count; n++)
        {

            var btn = buttons[n];
            var resultvec = new Vector2((btn.Left.Pixels + 16) / calculatedDimension.Width, 2 * (1 - (btn.Top.Pixels + 16) / calculatedDimension.Height));
            var tarvec = new Vector2((btn.handle.Left.Pixels + 16) / calculatedDimension.Width, 2 * (1 - (btn.handle.Top.Pixels + 16) / calculatedDimension.Height));
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
        #endregion

        #region 计算面积并分区
        List<double[]> coefficientList = [];
        List<double[]> intergalList = [];
        List<Vector2> rangeList = [];
        List<float> areaList = [];
        int m = nodes.Count - 1;
        for (int n = 0; n < m; n++)
        {
            var pts = nodes[n];
            var ptd = nodes[n + 1];

            var coefficient = TwoPointHermiteCoefficient(pts, ptd);
            var derivative = Derivative(coefficient);
            var integrate = Integrate(coefficient, pts.X);
            var root1 = NewtonRoot(coefficient, derivative, pts.X, 3);
            var root2 = NewtonRoot(coefficient, derivative, ptd.X, 3);
            var f1 = Evaluate(coefficient, root1);
            var f2 = Evaluate(coefficient, root2);
            if (root1 > pts.X && root1 < ptd.X && root2 > pts.X && root2 < ptd.X && Math.Abs(f1) < 0.05 && Math.Abs(f2) < 0.05)
            {
                if (root2 < root1)
                    Utils.Swap(ref root1, ref root2);
                areaList.Add((float)Evaluate(integrate, root1));
                areaList.Add(0);
                double f = Evaluate(integrate, root2);
                areaList.Add((float)Evaluate(integrate, ptd.X) - (float)f);

                intergalList.Add(integrate);
                intergalList.Add(integrate);
                int u = integrate.Length;
                var i2 = new double[u];
                Array.Copy(integrate, i2, u);
                i2[0] -= f;
                intergalList.Add(i2);
                coefficientList.Add(coefficient);
                coefficientList.Add(coefficient);
                coefficientList.Add(coefficient);

                rangeList.Add(new(pts.X, (float)root1));
                rangeList.Add(new((float)root1, (float)root2));
                rangeList.Add(new((float)root2, ptd.X));
            }
            else
            {
                coefficientList.Add(coefficient);
                intergalList.Add(integrate);
                rangeList.Add(new(pts.X, ptd.X));
                areaList.Add((float)Evaluate(integrate, ptd.X));
            }
        }
        coefficients = [.. coefficientList];
        integrates = [.. intergalList];
        ranges = [.. rangeList];
        areas = [.. areaList];
        #endregion

        #region 累积并归一化
        m = areaList.Count;
        areas = new float[m];
        for (int n = 0; n < m; n++)
        {
            areas[n] = areaList[n];
            if (n > 0)
                areas[n] += areas[n - 1];
        }
        TotalArea = areas[^1];
        for (int n = 0; n < m; n++)
            areas[n] /= TotalArea;
        #endregion
    }

}

partial class NormalDistribution
{
    public override void AppendElements(UIElement basePanel)
    {

    }
}
