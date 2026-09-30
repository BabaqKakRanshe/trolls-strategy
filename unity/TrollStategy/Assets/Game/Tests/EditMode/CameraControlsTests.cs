using NUnit.Framework;
using TrollStrategy.Presentation.Island;
using TrollStrategy.Presentation.Visuals;
using UnityEngine;

namespace TrollStrategy.Tests
{
    public sealed class CameraControlsTests
    {
        private static readonly Vector2 Screen = new(1920f, 1080f);

        [Test]
        public void PointerInsideTheScreenDoesNotPan()
        {
            Assert.AreEqual(Vector2.zero, IslandCameraRig.EdgeDirection(new Vector2(960f, 540f), Screen, 12f));
            Assert.AreEqual(Vector2.zero, IslandCameraRig.EdgeDirection(new Vector2(13f, 1067f), Screen, 12f));
        }

        [Test]
        public void PointerAtAnEdgePansTowardsIt()
        {
            Assert.AreEqual(new Vector2(-1f, 0f), IslandCameraRig.EdgeDirection(new Vector2(5f, 540f), Screen, 12f));
            Assert.AreEqual(new Vector2(1f, 0f), IslandCameraRig.EdgeDirection(new Vector2(1919f, 540f), Screen, 12f));
            Assert.AreEqual(new Vector2(0f, 1f), IslandCameraRig.EdgeDirection(new Vector2(960f, 1080f), Screen, 12f));
            Assert.AreEqual(new Vector2(0f, -1f), IslandCameraRig.EdgeDirection(new Vector2(960f, 0f), Screen, 12f));
            Assert.AreEqual(new Vector2(1f, -1f), IslandCameraRig.EdgeDirection(new Vector2(1915f, 3f), Screen, 12f));
        }

        [Test]
        public void PointerOutsideTheScreenOrNoBandDoesNotPan()
        {
            Assert.AreEqual(Vector2.zero, IslandCameraRig.EdgeDirection(new Vector2(-40f, 540f), Screen, 12f));
            Assert.AreEqual(Vector2.zero, IslandCameraRig.EdgeDirection(new Vector2(960f, 1300f), Screen, 12f));
            Assert.AreEqual(Vector2.zero, IslandCameraRig.EdgeDirection(new Vector2(5f, 540f), Screen, 0f));
        }

        [Test]
        public void SmallPointerJitterIsStillAClick()
        {
            var pressed = new Vector2(400f, 300f);
            Assert.IsFalse(UIInputUtils.IsDrag(pressed, pressed + new Vector2(3f, 4f)));
            Assert.IsTrue(UIInputUtils.IsDrag(pressed, pressed + new Vector2(UIInputUtils.DragThresholdPixels, 0f)));
        }
    }
}
