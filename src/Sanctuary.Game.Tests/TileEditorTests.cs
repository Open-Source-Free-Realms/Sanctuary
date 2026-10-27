using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sanctuary.Core.Extensions;
using Sanctuary.Game.Authoring;

namespace Sanctuary.Game.Tests;

[TestClass]
public sealed class TileEditorTests
{
    [TestMethod]
    public void Rotate_MatchesHeadingFacing()
    {
        // An offset along a heading's facing, rotated by a, must lie along the facing of heading + a.
        foreach (var heading in new[] { 0f, 0.7f, -2.1f })
        {
            foreach (var angle in new[] { MathF.PI / 2, 0.3f, -1.2f })
            {
                var facing = QuaternionExtensions.FromHeading(heading);
                var expected = QuaternionExtensions.FromHeading(heading + angle);

                var (dx, dz) = TileEditor.Rotate(facing.X, facing.Z, angle);

                Assert.AreEqual(expected.X, dx, 1e-4f);
                Assert.AreEqual(expected.Z, dz, 1e-4f);
            }
        }
    }

    [TestMethod]
    public void Rotate_FullTurnIsIdentity()
    {
        var (dx, dz) = TileEditor.Rotate(3f, -2f, MathF.PI * 2);

        Assert.AreEqual(3f, dx, 1e-4f);
        Assert.AreEqual(-2f, dz, 1e-4f);
    }
}
