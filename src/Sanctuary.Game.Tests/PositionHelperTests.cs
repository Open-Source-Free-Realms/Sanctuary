using System;
using System.Numerics;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class PositionHelperTests
{
    private const float Tolerance = 0.001f;

    private static readonly Vector4 Target = new(10f, 5f, 20f, 1f);

    [TestMethod]
    public void GetPointNear_StandsBetweenTheTargetAndTheOrigin()
    {
        var point = PositionHelper.GetPointNear(Target, 2f, new Vector4(10f, 0f, 30f, 1f));

        Assert.AreEqual(10f, point.X, Tolerance);
        Assert.AreEqual(22f, point.Z, Tolerance);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetPointNear_KeepsTheDistanceAndTheTargetHeight(bool withOrigin)
    {
        for (var i = 0; i < 20; i++)
        {
            var point = PositionHelper.GetPointNear(Target, 2f, withOrigin ? new Vector4(-3f, 8f, 4f, 1f) : null);

            Assert.AreEqual(2f, MathF.Sqrt((point.X - Target.X) * (point.X - Target.X) + (point.Z - Target.Z) * (point.Z - Target.Z)), Tolerance);
            Assert.AreEqual(Target.Y, point.Y);
            Assert.AreEqual(Target.W, point.W);
        }
    }
}
