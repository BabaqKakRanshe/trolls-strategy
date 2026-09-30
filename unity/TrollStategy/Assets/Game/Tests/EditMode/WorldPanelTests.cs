using NUnit.Framework;
using TrollStrategy.Presentation.WorldUi;
using UnityEngine;

namespace TrollStrategy.Tests
{
    public sealed class WorldPanelTests
    {
        private Camera _camera;
        private WorldPanel _panel;

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.fieldOfView = 45f;
            _panel = WorldPanel.Create("Label", null, 25);
            _panel.AddLabel("world-label");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_panel.gameObject);
            Object.DestroyImmediate(_camera.gameObject);
        }

        [Test]
        public void ViewHeightGrowsWithDepthAlongTheView()
        {
            float expected = 2f * 10f * Mathf.Tan(22.5f * Mathf.Deg2Rad);
            Assert.AreEqual(expected, WorldPanel.ViewHeight(_camera, new Vector3(0f, 0f, 10f)), 1e-4f);
            Assert.AreEqual(expected, WorldPanel.ViewHeight(_camera, new Vector3(3f, -2f, 10f)), 1e-4f);
            Assert.AreEqual(0f, WorldPanel.ViewHeight(_camera, new Vector3(0f, 0f, -5f)));

            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
            Assert.AreEqual(10f, WorldPanel.ViewHeight(_camera, new Vector3(0f, 0f, 40f)), 1e-4f);
        }

        [Test]
        public void TextKeepsItsReadableSizeOnlyOnceItWouldShrinkBelowIt()
        {
            // a 24px name spans 13.6 m of screen height at the closest colony zoom and 62 m at the farthest
            Assert.AreEqual(1f, WorldPanel.HoldFor(13.6f, 24f));
            float hold = WorldPanel.HoldFor(62f, 24f);
            float onScreen = 24f * hold / WorldPanel.PixelsPerUnit * WorldPanel.ReferenceScreenHeight / 62f;
            Assert.AreEqual(WorldPanel.ReadableTextSize, onScreen, 1e-3f);
            Assert.Greater(hold, 4f);
            // a big label holds later than a small one
            Assert.Less(WorldPanel.HoldFor(62f, 64f), hold);
        }

        [Test]
        public void NothingToMeasureKeepsTheWorldSize()
        {
            Assert.AreEqual(1f, WorldPanel.HoldFor(62f, 0f));
            Assert.AreEqual(1f, WorldPanel.HoldFor(0f, 24f));
            Assert.AreEqual(1f, WorldPanel.HoldFor(float.NaN, 24f));
        }

        [Test]
        public void ContentIsFarWhereAWorldUnitGetsSmallOnScreen()
        {
            Assert.IsFalse(WorldPanel.IsFar(30f));
            Assert.IsTrue(WorldPanel.IsFar(40f));

            _panel.transform.position = new Vector3(0f, 0f, 16f);
            _panel.Measure(_camera);
            Assert.IsFalse(_panel.Far);
            Assert.IsFalse(_panel.Content.ClassListContains(WorldPanel.FarClass));

            _panel.transform.position = new Vector3(0f, 0f, 75f);
            _panel.Measure(_camera);
            Assert.IsTrue(_panel.Far);
            Assert.IsTrue(_panel.Content.ClassListContains(WorldPanel.FarClass));

            _panel.transform.position = new Vector3(0f, 0f, 16f);
            _panel.Measure(_camera);
            Assert.IsFalse(_panel.Content.ClassListContains(WorldPanel.FarClass));
        }

        [Test]
        public void ReadablePanelOwnsItsScaleAndOthersLeaveItToTheOwner()
        {
            _panel.transform.position = new Vector3(0f, 0f, 75f);
            _panel.Measure(_camera);
            _panel.Scale = .5f;
            Assert.AreEqual(.5f * _panel.Hold, _panel.transform.localScale.x, 1e-5f);

            _panel.KeepReadable = false;
            _panel.transform.localScale = Vector3.one * 2f;
            _panel.Scale = .25f;
            _panel.Measure(_camera);
            Assert.AreEqual(2f, _panel.transform.localScale.x, 1e-5f);
        }
    }
}
