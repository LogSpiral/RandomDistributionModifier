using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using System.Reflection;

namespace RandomDistributionModifier;

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
public partial class SmoothDistribution : DistributionSetter
{
    public static List<Vector3> nodes = [];
    public static List<DraggableButton> buttons = [];
    //public List<DraggableButton> tangents = new();
    public static float[] areas = [];
    public static double[][] coefficients = [];
    public static double[][] integrates = [];
    public static Vector2[] ranges = [];
    public static float TotalArea;

    static float TwoPointHermite(Vector3 pts, Vector3 ptd, float t)
        => MathHelper.Hermite(pts.Y, pts.Z * (ptd.X - pts.X), ptd.Y, ptd.Z * (ptd.X - pts.X), Utils.GetLerpValue(pts.X, ptd.X, t));

    static double[] HermiteCoefficient(double v1, double t1, double v2, double t2)
        => [v1, t1, 3 * v2 - 3 * v1 - 2 * t1 - t2, 2 * v1 - 2 * v2 + t1 + t2]; 

    static double[] TwoPointHermiteCoefficient(Vector3 pts, Vector3 ptd)
        => LinearTransfrom(
            HermiteCoefficient(pts.Y, pts.Z * (ptd.X - pts.X), ptd.Y, ptd.Z * (ptd.X - pts.X))
            , 1 / (ptd.X - pts.X), -pts.X / (ptd.X - pts.X));

    static double[] Derivative(double[] coefficients)
    {
        if (coefficients.Length is 0 or 1)
            return [0];
        int m = coefficients.Length - 1;
        double[] result = new double[m];
        for (int k = 0; k < m; k++)
            result[k] = coefficients[k + 1] * (k + 1);
        return result;
    }

    static double[] Integrate(double[] coefficients, double start)
    {
        if (coefficients.Length is 0)
            return [0];
        int m = coefficients.Length + 1;
        double[] result = new double[m];

        double t = start;
        double c = 0;
        for (int k = 1; k < m; k++)
        {
            result[k] = coefficients[k - 1] / k;
            c += result[k] * t;
            t *= start;
        }
        result[0] = -c;
        return result;
    }

    static double Combination(int n, int m)
    {
        double result = 1;
        for (int k = 0; k < m; k++)
            result *= (n - k) / (k + 1.0);
        return result;
    }

    static double[] LinearTransfrom(double[] coefficients, double k, double c)
    {
        int length = coefficients.Length;
        double[] result = new double[length];
        for (int n = 0; n < length; n++)
            for (int m = 0; m <= n; m++)
                result[m] += Math.Pow(k, m) * Math.Pow(c, n - m) * Combination(n, m) * coefficients[n];
        return result;
    }

    static double Evaluate(double[] coefficients, double x)
    {
        double t = 1;
        double result = 0;
        int m = coefficients.Length;
        for (int k = 0; k < m; k++)
        {
            result += coefficients[k] * t;
            t *= x;
        }
        return result;
    }

    static double NewtonRoot(double[] coefficients, double[] derivatives, double start, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double f = Evaluate(coefficients, start);
            double k = Evaluate(derivatives, start);
            start -= f / k;
        }
        return start;
    }

    // 使用前提：单调递增函数
    static double BinaryRoot(double[] coefficients, double top, double bottom, int count)
    {
        double root = (top + bottom) * .5f;
        for (int n = 0; n < count; n++)
        {
            var value = Evaluate(coefficients, root);
            if (value > 0)
                top = root;
            else
                bottom = root;

            root = (top + bottom) * .5f;
        }
        return root;
    }

    static double CombinedRoot(double[] coefficients, double[] derivatives, int countB, int countN, double up, double bottom)
    {
        double root = BinaryRoot(coefficients, up, bottom, countB);
        return NewtonRoot(coefficients, derivatives, root, countN);
    }

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

        return TwoPointHermite(pts, ptd, t);
    }

    public override double ConvertFromUnified(Func<double> t)
    {
        double randomValue = t();

        int index = 0;
        while (randomValue > areas[index])
            index++;
        if (index > 0)
            randomValue -= areas[index - 1];

        Vector2 range = ranges[index];
        int m = integrates[index].Length;
        double[] expression = new double[m];
        Array.Copy(integrates[index], expression, m);
        expression[0] -= randomValue * TotalArea;

        //var root = BinaryRoot(expression, range.Y, range.X, 10);
        //return root;
        var root = NewtonRoot(expression, coefficients[index], (range.X + range.Y) * .5,3);//CombinedRoot(expression, coefficients[index], 2, 3, range.X, range.Y);
        return root;
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
public partial class LinearDistribution : DistributionSetter
{
    public static List<Vector2> nodes = [];
    public static List<DraggableButton> buttons = [];
    public static float[] areas = [];
    public static float TotalArea;
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


        int index = 0;
        while (randomValue > areas[index])
            index++;
        if (index > 0)
            randomValue -= areas[index - 1];
        float k = (nodes[index + 1].Y - nodes[index].Y) / (nodes[index + 1].X - nodes[index].X) / TotalArea;
        float y = nodes[index].Y / TotalArea;
        if (k != 0)
            return (-y + Math.Sqrt(y * y + 2 * k * randomValue)) / k + nodes[index].X;
        else
            return randomValue / y + nodes[index].X;

        //return t();
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
public partial class PowerDistribution : DistributionSetter//这不是指数分布，但是我姑且这么叫吧（x
{
    static int power = 1;
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
            f *= n;
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
public partial class NormalDistribution : DistributionSetter
{


    public override double ConvertFromUnified(Func<double> t)
    {
        double u = -2 * Math.Log(t.Invoke());
        double v = 2 * Math.PI * t.Invoke();
        return Math.Sqrt(u) * Math.Cos(v) / 6 + .5;
    }

    public override void Load(BinaryReader reader)
    {
    }

    public override float PDF(float t) => 2.39365f * MathF.Exp(-18 * (t - .5f) * (t - .5f));//6 / MathF.Sqrt(MathHelper.TwoPi)

    public override void Save(BinaryWriter writer)
    {
    }
}
