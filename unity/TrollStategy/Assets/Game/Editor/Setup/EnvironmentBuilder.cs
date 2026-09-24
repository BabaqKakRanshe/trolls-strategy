using UnityEngine;
using TrollStrategy.Presentation.Visuals;

namespace TrollStrategy.Editor.Setup
{
    // Kept as the scene builder entry point; the environment has one 3D implementation.
    public static class EnvironmentBuilder
    {
        public static void BuildEnvironment(Transform gridTransform)
        {
            var old = gridTransform.Find("EnvironmentWorld");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var environment = gridTransform.GetComponent<PrimitiveEnvironment>();
            if (environment == null) environment = gridTransform.gameObject.AddComponent<PrimitiveEnvironment>();
            environment.Rebuild();
        }
    }
}
